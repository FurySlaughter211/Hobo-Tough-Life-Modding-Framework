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
    /// Main plugin entry point for Hobo: Tough Life modifications.
    /// Uses BepInEx 6 for IL2CPP games.
    /// 
    /// DEBUG HOTKEYS (Requires EnableDebugMode = true in config):
    /// F1 - Toggle Persistent God Mode
    /// F2 - Save Position
    /// F3 - Teleport to Saved Position
    /// F5 - Explore Item Database
    /// F6 - Get Player Info
    /// F7 - Try to add an item to inventory
    /// F8 - List all skills and buffs
    /// F12 - One-time God Mode
    /// 
    /// MODDING HOTKEYS (Always active):
    /// Insert - Spawn test mod item (Shrek Hoodie)
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
                "Enable debug/cheat hotkeys (F1-F12). Set to true for development. Default: false"
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
            
            if (EnableDebugMode.Value)
            {
                Log.LogInfo("DEBUG MODE ENABLED - Cheat hotkeys active");
                Log.LogInfo("F1-God | F2-Save | F3-TP | F12-GodOnce");
            }
            else
            {
                Log.LogInfo("Debug mode disabled (enable in config for cheats)");
            }
        }
    }
    
    /// <summary>
    /// MonoBehaviour that runs Update every frame to handle hotkeys.
    /// Routes to modular features in Features/ folder.
    /// </summary>
    public class ModUpdater : MonoBehaviour
    {
        void Update()
        {
            // ============================================
            // DEBUG/CHEAT HOTKEYS - Only if enabled in config
            // ============================================
            if (Plugin.EnableDebugMode.Value)
            {
                // Apply persistent god mode if enabled
                CheatMods.ApplyIfEnabled();
                
                // F1 - Toggle Persistent God Mode
                if (Input.GetKeyDown(KeyCode.F1))
                {
                    CheatMods.TogglePersistentGodMode();
                }
                
                // F2 - Save Position
                if (Input.GetKeyDown(KeyCode.F2))
                {
                    CheatMods.SavePosition();
                }
                
                // F3 - Teleport to Saved Position
                if (Input.GetKeyDown(KeyCode.F3))
                {
                    CheatMods.TeleportToSavedPosition();
                }
                
                // F5 - Explore Item Database
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    DebugTools.ExploreItemDatabase();
                }
                
                // F6 - Get Player Info
                if (Input.GetKeyDown(KeyCode.F6))
                {
                    DebugTools.GetPlayerInfo();
                }
                
                // F7 - Add Item to Inventory
                if (Input.GetKeyDown(KeyCode.F7))
                {
                    DebugTools.TryAddItem();
                }
                
                // F8 - List Skills and Buffs
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    DebugTools.ListSkillsAndBuffs();
                }
                
                // F9 - Show Climate/Weather Info
                if (Input.GetKeyDown(KeyCode.F9))
                {
                    DebugTools.ShowClimateInfo();
                }
                
                // F10 - Start Test Quest
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    DebugTools.TryStartTestQuest();
                }
                
                // F11 - Explore Gear Items
                if (Input.GetKeyDown(KeyCode.F11))
                {
                    DebugTools.ExploreGearItems();
                }
                
                // F12 - One-time God Mode
                if (Input.GetKeyDown(KeyCode.F12))
                {
                    Plugin.Log.LogInfo("=== F12: GOD MODE ===");
                    CheatMods.ActivateGodMode();
                }
                
                // Handle Winter Climate Mod inputs (Numpad4 to toggle)
                WinterClimateMod.HandleInput();
            }
            
            // ============================================
            // MOD TESTING HOTKEYS - Always active for mod development
            // ============================================
            
            // Insert - Spawn Shrek Hoodie for testing
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                Plugin.Log.LogInfo("Insert key pressed - spawning test mod item...");
                DebugTools.SpawnModItem("shrek_mod:shrek_hoodie");
            }
            
            // F11 - List all Gear items (for finding valid baseItem IDs)
            if (Input.GetKeyDown(KeyCode.F11))
            {
                DebugTools.ExploreGearItems();
            }
        }
    }
}
