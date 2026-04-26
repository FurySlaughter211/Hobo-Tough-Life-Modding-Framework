using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace HoboModFramework.Framework
{
    public class HoboExplorer : MonoBehaviour
    {
        private HoboExplorer() { }

        private string _commandFilePath;
        private float _checkTimer = 0f;
        private const float CheckInterval = 0.5f;

        // Cached references — resolved once on first use
        private bool _bridgeResolved = false;
        private bool _bridgeFailed = false;
        private object _consoleControllerInstance = null;
        private MethodInfo _evaluateMethod = null;

        void Awake()
        {
            _commandFilePath = Path.Combine(Paths.PluginPath, "HoboExplorer_Command.txt");
            HoboModPlugin.Plugin.Log.LogInfo($"[HoboExplorer] Bridge Active. Watching: {_commandFilePath}");
        }

        void Update()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer < CheckInterval) return;
            _checkTimer = 0f;

            if (!File.Exists(_commandFilePath)) return;

            try
            {
                string codeToRun = File.ReadAllText(_commandFilePath);
                File.Delete(_commandFilePath);

                if (!string.IsNullOrWhiteSpace(codeToRun))
                {
                    HoboModPlugin.Plugin.Log.LogInfo($"[HoboExplorer] Processing script ({codeToRun.Length} chars)...");
                    ExecuteViaConsoleController(codeToRun);
                }
            }
            catch (Exception ex)
            {
                HoboModPlugin.Plugin.Log.LogError($"[HoboExplorer] Failed to process command file: {ex.Message}");
            }
        }

        private void ResolveBridge()
        {
            if (_bridgeResolved || _bridgeFailed) return;

            try
            {
                // ── Verified assembly name from live memory audit ──
                const string asmName = "UnityExplorer.BIE.Unity.IL2CPP.CoreCLR";
                Assembly ueAssembly = null;

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == asmName)
                    {
                        ueAssembly = asm;
                        break;
                    }
                }

                if (ueAssembly == null)
                {
                    HoboModPlugin.Plugin.Log.LogError($"[HoboExplorer] Could not find assembly: {asmName}");
                    _bridgeFailed = true;
                    return;
                }

                HoboModPlugin.Plugin.Log.LogInfo($"[HoboExplorer] Found assembly: {asmName}");

                // ── Load all types with fault tolerance ──
                Type[] types;
                try
                {
                    types = ueAssembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    int loaded = 0;
                    var filtered = new System.Collections.Generic.List<Type>();
                    foreach (var t in e.Types) { if (t != null) { filtered.Add(t); loaded++; } }
                    types = filtered.ToArray();
                    HoboModPlugin.Plugin.Log.LogWarning($"[HoboExplorer] Partial type load: {loaded} types recovered.");
                }

                // ── Step 1: Get ConsoleController type ──
                Type controllerType = null;
                foreach (var t in types)
                    if (t.FullName == "UnityExplorer.CSConsole.ConsoleController") { controllerType = t; break; }

                if (controllerType == null)
                {
                    HoboModPlugin.Plugin.Log.LogError("[HoboExplorer] Could not find ConsoleController type.");
                    _bridgeFailed = true;
                    return;
                }

                // ── Step 2: Get _instance (verified ACTIVE by audit) ──
                var instanceProp = controllerType.GetProperty("_instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var instanceField = controllerType.GetField("<_instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                _consoleControllerInstance = instanceProp?.GetValue(null) ?? instanceField?.GetValue(null);

                if (_consoleControllerInstance == null)
                {
                    HoboModPlugin.Plugin.Log.LogError("[HoboExplorer] ConsoleController._instance is null. Is the UE console panel open?");
                    _bridgeFailed = true;
                    return;
                }

                HoboModPlugin.Plugin.Log.LogInfo($"[HoboExplorer] Got ConsoleController instance: {_consoleControllerInstance.GetType().Name}");

                // ── Step 3: Find Evaluate(string input, bool supressLog) ──
                // Signature proven via live memory audit and hypothesis test (double-file method).
                _evaluateMethod = controllerType.GetMethod(
                    "Evaluate",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string), typeof(bool) },
                    null);

                if (_evaluateMethod == null)
                {
                    HoboModPlugin.Plugin.Log.LogError("[HoboExplorer] Could not find Evaluate(string, bool) on ConsoleController.");
                    _bridgeFailed = true;
                    return;
                }

                HoboModPlugin.Plugin.Log.LogInfo($"[HoboExplorer] Found execution method: {_evaluateMethod.Name}(string, bool)");

                _bridgeResolved = true;
                HoboModPlugin.Plugin.Log.LogInfo("[HoboExplorer] Bridge fully resolved and ready.");
            }
            catch (Exception e)
            {
                HoboModPlugin.Plugin.Log.LogError($"[HoboExplorer] Bridge resolution exception: {e.Message}\n{e.StackTrace}");
                _bridgeFailed = true;
            }
        }

        private void ExecuteViaConsoleController(string script)
        {
            // Re-attempt resolution each time if previously failed (UE may load later)
            if (_bridgeFailed) _bridgeFailed = false;

            ResolveBridge();

            if (!_bridgeResolved)
            {
                HoboModPlugin.Plugin.Log.LogWarning("[HoboExplorer] Bridge not ready. Script dropped.");
                return;
            }

            try
            {
                // supressLog=false so UnityExplorer still shows output in its console
                _evaluateMethod.Invoke(_consoleControllerInstance, new object[] { script, false });
                HoboModPlugin.Plugin.Log.LogInfo($"[HoboExplorer] Script executed via ConsoleController.{_evaluateMethod.Name}");
            }
            catch (TargetInvocationException tie)
            {
                HoboModPlugin.Plugin.Log.LogError($"[HoboExplorer] Execution error: {tie.InnerException?.Message ?? tie.Message}");
            }
            catch (Exception e)
            {
                HoboModPlugin.Plugin.Log.LogError($"[HoboExplorer] Unexpected error: {e.Message}");
            }
        }
    }
}
