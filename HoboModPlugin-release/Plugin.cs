using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using Game;
using HoboModPlugin.Features;
using HoboModPlugin.Framework;

namespace HoboModPlugin
{
    /// <summary>
    /// HoboModFramework - Pure modding enabler for Hobo: Tough Life.
    /// Uses BepInEx 6 for IL2CPP games.
    /// 
    /// CORE PRINCIPLE: The framework does NOTHING on its own.
    /// All features (items, recipes, hotkeys, etc.) come from mods.
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
        
        // Configuration
        internal static ConfigEntry<bool> EnableDebugMode;

        public override void Load()
        {
            Log = base.Log;
            
            // Configuration - Debug mode OFF by default for players
            EnableDebugMode = Config.Bind(
                "Debug", 
                "EnableDebugMode", 
                false, 
                "Legacy setting - debug hotkeys now require debug_mod to be installed"
            );
            
            // Initialize framework
            var pluginPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Framework = new FrameworkManager(Log, pluginPath);
            
            // Discover mods early (before game databases load)
            Framework.DiscoverMods();
            
            // Apply Harmony patches (from Patches/ folder AND Framework/)
            _harmony = new Harmony(PLUGIN_GUID);
            _harmony.PatchAll();
            
            // Register our Update method to run every frame
            AddComponent<ModUpdater>();
            
            Log.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded successfully!");
            Log.LogInfo($"Mods discovered: {Framework.ModLoader.LoadedMods.Count}");
            
            // Debug mode config is legacy - kept for backwards compatibility
            // Actual debug features now require debug_mod to be loaded
        }
    }
    
    /// <summary>
    /// MonoBehaviour that runs Update every frame to handle hotkeys.
    /// Framework does nothing on its own - all features come from mods.
    /// </summary>
    public class ModUpdater : MonoBehaviour
    {
        private static bool _devHotkeysLogged = false;
        
        void Start()
        {
            // Log dev hotkeys from all mods (once)
            LogModDevHotkeys();
        }
        
        void Update()
        {
            // Process dev hotkeys from ALL mods
            // Framework does nothing on its own - all features come from mods
            ProcessModDevHotkeys();
        }
        
        /// <summary>
        /// Log all dev hotkeys from loaded mods at startup
        /// </summary>
        private void LogModDevHotkeys()
        {
            if (_devHotkeysLogged) return;
            _devHotkeysLogged = true;
            
            foreach (var mod in Plugin.Framework.ModLoader.LoadedMods)
            {
                if (mod.DevHotkeys == null || mod.DevHotkeys.Count == 0) continue;
                
                Plugin.Log.LogInfo($"=== {mod.Name.ToUpper()} DEV HOTKEYS ===");
                foreach (var hotkey in mod.DevHotkeys)
                {
                    Plugin.Log.LogInfo($"{hotkey.Key} - {hotkey.Action}: {hotkey.ItemId}");
                }
            }
        }
        
        /// <summary>
        /// Process dev hotkeys from all loaded mods
        /// NOTE: Logic is inlined to avoid IL2CPP issue with custom class parameters
        /// </summary>
        private void ProcessModDevHotkeys()
        {
            foreach (var mod in Plugin.Framework.ModLoader.LoadedMods)
            {
                if (mod.DevHotkeys == null) continue;
                
                foreach (var hotkey in mod.DevHotkeys)
                {
                    KeyCode keyCode = ParseKeyCode(hotkey.Key);
                    if (keyCode != KeyCode.None && Input.GetKeyDown(keyCode))
                    {
                        // Inlined action execution (IL2CPP can't handle DevHotkeyDefinition as parameter)
                        switch (hotkey.Action?.ToLower())
                        {
                            case "spawn_item":
                                string fullItemId = $"{mod.Id}:{hotkey.ItemId}";
                                Plugin.Log.LogInfo($"[{hotkey.Key}] Spawning mod item: {fullItemId}");
                                DebugTools.SpawnModItem(fullItemId);
                                break;
                            case "spawn_vanilla":
                                if (uint.TryParse(hotkey.ItemId, out uint vanillaId))
                                {
                                    Plugin.Log.LogInfo($"[{hotkey.Key}] Spawning vanilla item ID: {vanillaId}");
                                    DebugTools.SpawnVanillaItem(vanillaId);
                                }
                                else
                                {
                                    Plugin.Log.LogWarning($"Invalid vanilla item ID: {hotkey.ItemId}");
                                }
                                break;
                            case "explore_items":
                                Plugin.Log.LogInfo($"[{hotkey.Key}] Exploring item database");
                                DebugTools.ExploreItemDatabase();
                                break;
                            case "search_items":
                                Plugin.Log.LogInfo($"[{hotkey.Key}] Searching items by name");
                                DebugTools.SearchItemsByName();
                                break;
                            case "dump_items":
                                Plugin.Log.LogInfo($"[{hotkey.Key}] Dumping items to file");
                                DebugTools.DumpAllItemsToFile();
                                break;
                            default:
                                Plugin.Log.LogWarning($"Unknown hotkey action: {hotkey.Action}");
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
