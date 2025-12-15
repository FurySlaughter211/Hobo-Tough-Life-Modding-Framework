using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using HoboModPlugin.Framework;

namespace HoboModPlugin
{
    /// <summary>
    /// HoboModFramework - A modding framework for Hobo: Tough Life
    /// Enables custom items, recipes, quests, and more via JSON mods.
    /// 
    /// Place mods in: BepInEx/plugins/HoboMods/[YourModName]/
    /// See MODDING_GUIDE.md for documentation.
    /// </summary>
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public const string PLUGIN_GUID = "com.hobomod.framework";
        public const string PLUGIN_NAME = "HoboModFramework";
        public const string PLUGIN_VERSION = "1.0.0";

        internal static new ManualLogSource Log;
        private Harmony _harmony;
        
        // Framework components
        internal static FrameworkManager Framework;

        public override void Load()
        {
            Log = base.Log;
            
            // Initialize framework
            var pluginPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Framework = new FrameworkManager(Log, pluginPath);
            
            // Discover mods early (before game databases load)
            Framework.DiscoverMods();
            
            // Apply Harmony patches
            _harmony = new Harmony(PLUGIN_GUID);
            _harmony.PatchAll();
            
            // Register update component for recipe unlocking
            AddComponent<ModUpdater>();
            
            Log.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded successfully!");
            Log.LogInfo($"Mods loaded: {Framework.ModLoader.LoadedMods.Count}");
        }
    }
    
    /// <summary>
    /// MonoBehaviour for framework updates (recipe unlocking, etc.)
    /// </summary>
    public class ModUpdater : MonoBehaviour
    {
        void Update()
        {
            // Framework handles content injection automatically via patches
            // This component exists for any future per-frame needs
        }
    }
}
