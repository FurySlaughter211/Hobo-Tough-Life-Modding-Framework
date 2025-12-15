using BepInEx.Logging;
using UnityEngine;
using Game;

namespace HoboModPlugin.Framework
{
    /// <summary>
    /// Main framework manager - coordinates all framework components
    /// </summary>
    public class FrameworkManager
    {
        private readonly ManualLogSource _log;
        private readonly string _pluginPath;
        
        public ModLoader ModLoader { get; private set; }
        public ItemRegistry ItemRegistry { get; private set; }
        public RecipeRegistry RecipeRegistry { get; private set; }
        public EffectHandler EffectHandler { get; private set; }
        public QuestRegistry QuestRegistry { get; private set; }
        
        private bool _initialized = false;
        
        public FrameworkManager(ManualLogSource log, string pluginPath)
        {
            _log = log;
            _pluginPath = pluginPath;
            
            // Initialize components
            ModLoader = new ModLoader(log, pluginPath);
            ItemRegistry = new ItemRegistry(log);
            RecipeRegistry = new RecipeRegistry(log, ItemRegistry);
            EffectHandler = new EffectHandler(log, ItemRegistry);
            QuestRegistry = new QuestRegistry(log);
            
            // Initialize patches with references
            FrameworkPatches.Initialize(log, ItemRegistry, EffectHandler, RecipeRegistry);
            FrameworkUtils.Initialize(log);
        }
        
        /// <summary>
        /// Discover all mods (call early on startup)
        /// </summary>
        public void DiscoverMods()
        {
            _log.LogInfo("!!! VERIFYING UPDATE: FRAMEWORK MANAGER LOADED !!!");
            _log.LogInfo("=== HoboModFramework: Discovering Mods ===");
            ModLoader.DiscoverMods();
            
            // Load content definitions from each mod
            foreach (var mod in ModLoader.LoadedMods)
            {
                ItemRegistry.LoadItemsFromMod(mod);
                RecipeRegistry.LoadRecipesFromMod(mod);
                QuestRegistry.LoadQuestsFromMod(mod);
            }
        }
        
        /// <summary>
        /// Inject all content into game (call when game databases are ready)
        /// Uses database content check instead of flag to handle re-injection on world changes
        /// </summary>
        public void InjectContent()
        {
            // FIX: Check if content actually exists in databases instead of using static flag
            // This handles re-injection when databases are reloaded (e.g., switching worlds)
            try
            {
                var recipes = RecipeDatabase.recipes;
                if (recipes != null && recipes.ContainsKey(51000))
                {
                    _log.LogInfo("=== HoboModFramework: Content already exists in database, skipping ===");
                    return;
                }
            }
            catch (System.Exception ex)
            {
                _log.LogWarning($"Could not check database state: {ex.Message}");
            }
            
            _log.LogInfo("=== HoboModFramework: Injecting Content (Items/Recipes) ===");
            ItemRegistry.InjectAllItems();
            RecipeRegistry.InjectAllRecipes();
            _log.LogInfo("=== HoboModFramework: Content Injection Complete ===");
        }

        public void InjectQuests()
        {
            _log.LogInfo("=== HoboModFramework: Injecting Quests ===");
            QuestRegistry.InjectAllQuests();
        }
        
        /// <summary>
        /// Unlock pending recipes for player (call when crafting UI opens)
        /// </summary>
        public void UnlockRecipes()
        {
            var characters = Object.FindObjectsOfType<Character>();
            if (characters != null && characters.Length > 0)
            {
                RecipeRegistry.UnlockPendingRecipes(characters[0]);
            }
        }
        
        public bool IsInitialized => _initialized;
    }
}
