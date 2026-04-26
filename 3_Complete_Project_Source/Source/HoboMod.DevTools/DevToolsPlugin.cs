using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using System.IO;
using HoboModPlugin;
using HoboModPlugin.Framework;
using HoboModPlugin.Features;

namespace HoboMod.DevTools
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class DevToolsPlugin : BasePlugin
    {
        public const string PLUGIN_GUID = "com.hobomod.devtools";
        public const string PLUGIN_NAME = "HoboMod.DevTools";
        public const string PLUGIN_VERSION = "1.0.0";

        internal static new ManualLogSource Log;

        public override void Load()
        {
            Log = base.Log;
            var pluginPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var rootPath = Path.GetDirectoryName(pluginPath);
            
            // Initialize SceneDumper with this mod's path
            SceneDumper.Initialize(Log, rootPath);
            
            AddComponent<DevToolsUpdater>();
            Log.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded!");
        }
    }

    public class DevToolsUpdater : MonoBehaviour
    {
        public DevToolsUpdater(System.IntPtr ptr) : base(ptr) { }

        void Start()
        {
            // Log dev hotkeys from all mods (once)
            LogModDevHotkeys();
        }

        private void Update()
        {
            // Process dev hotkeys from ALL mods
            ProcessModDevHotkeys();
        }

        private bool _devHotkeysLogged = false;

        /// <summary>
        /// Log all dev hotkeys from loaded mods at startup
        /// </summary>
        private void LogModDevHotkeys()
        {
            if (_devHotkeysLogged) return;
            _devHotkeysLogged = true;
            
            // Plugin.Framework is internal static, generally accessible if internals visible
            // But DevTools is a separate assembly. 
            // Step 1 check: Is Plugin.Framework accessible? 
            // It is 'internal static'. We might need to use reflection or check InternalsVisibleTo.
            // For now, assuming it works or we fix accessibility.
            
            if (Plugin.Framework?.ModLoader?.LoadedMods == null) return;

            foreach (var mod in Plugin.Framework.ModLoader.LoadedMods)
            {
                if (mod.DevHotkeys == null || mod.DevHotkeys.Count == 0) continue;
                
                DevToolsPlugin.Log.LogInfo($"=== {mod.Name.ToUpper()} DEV HOTKEYS ===");
                foreach (var hotkey in mod.DevHotkeys)
                {
                    DevToolsPlugin.Log.LogInfo($"{hotkey.Key} - {hotkey.Action}: {hotkey.ItemId}");
                }
            }
        }
        
        /// <summary>
        /// Process dev hotkeys from all loaded mods
        /// </summary>
        private void ProcessModDevHotkeys()
        {
            if (Plugin.Framework?.ModLoader?.LoadedMods == null) return;

            foreach (var mod in Plugin.Framework.ModLoader.LoadedMods)
            {
                if (mod.DevHotkeys == null) continue;
                
                foreach (var hotkey in mod.DevHotkeys)
                {
                    KeyCode keyCode = ParseKeyCode(hotkey.Key);
                    if (keyCode != KeyCode.None && Input.GetKeyDown(keyCode))
                    {
                        switch (hotkey.Action?.ToLower())
                        {
                            case "spawn_item":
                                string fullItemId = $"{mod.Id}:{hotkey.ItemId}";
                                DevToolsPlugin.Log.LogInfo($"[{hotkey.Key}] Spawning mod item: {fullItemId}");
                                DebugTools.SpawnModItem(fullItemId);
                                break;
                            case "spawn_vanilla":
                                if (uint.TryParse(hotkey.ItemId, out uint vanillaId))
                                {
                                    DevToolsPlugin.Log.LogInfo($"[{hotkey.Key}] Spawning vanilla item ID: {vanillaId}");
                                    DebugTools.SpawnVanillaItem(vanillaId);
                                }
                                break;
                            case "explore_items":
                                DevToolsPlugin.Log.LogInfo($"[{hotkey.Key}] Exploring item database");
                                DebugTools.ExploreItemDatabase();
                                break;
                            case "search_items":
                                DevToolsPlugin.Log.LogInfo($"[{hotkey.Key}] Searching items by name");
                                DebugTools.SearchItemsByName();
                                break;
                            case "dump_items":
                                DevToolsPlugin.Log.LogInfo($"[{hotkey.Key}] Dumping items to file");
                                DebugTools.DumpAllItemsToFile();
                                break;
                            case "dump_stats":
                                DevToolsPlugin.Log.LogInfo($"[{hotkey.Key}] Dumping character stats");
                                DebugTools.DumpCharacterStats();
                                break;
                            case "start_quest":
                                string questId = $"{mod.Id}:{hotkey.ItemId}";
                                DevToolsPlugin.Log.LogInfo($"[{hotkey.Key}] Starting quest: {questId}");
                                DebugTools.TryStartTestQuest(questId);
                                break;
                            
                            
                            default:
                                DevToolsPlugin.Log.LogWarning($"Unknown hotkey action: {hotkey.Action}");
                                break;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Parse string key name to KeyCode enum
        /// </summary>
        private KeyCode ParseKeyCode(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return KeyCode.None;
            if (System.Enum.TryParse<KeyCode>(keyName, true, out var result))
                return result;
            return KeyCode.None;
        }


    }
}
