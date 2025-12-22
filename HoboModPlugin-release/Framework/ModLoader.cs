using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using BepInEx.Logging;
using Newtonsoft.Json;

namespace HoboModPlugin.Framework
{
    /// <summary>
    /// Discovers and loads mod folders from HoboMods directory
    /// </summary>
    public class ModLoader
    {
        private readonly ManualLogSource _log;
        private readonly string _modsPath;
        private readonly List<ModManifest> _loadedMods = new();
        
        public IReadOnlyList<ModManifest> LoadedMods => _loadedMods;
        
        /// <summary>
        /// Check if a mod with the given ID is loaded
        /// </summary>
        public bool IsModLoaded(string modId)
        {
            return _loadedMods.Any(m => m.Id.Equals(modId, StringComparison.OrdinalIgnoreCase));
        }
        
        public ModLoader(ManualLogSource log, string pluginPath)
        {
            _log = log;
            // Mods folder next to plugins folder
            _modsPath = Path.Combine(Path.GetDirectoryName(pluginPath) ?? "", "HoboMods");
        }
        
        /// <summary>
        /// Discover and load all mods from HoboMods folder
        /// </summary>
        public void DiscoverMods()
        {
            _log.LogInfo($"=== ModLoader: Discovering mods in {_modsPath} ===");
            
            if (!Directory.Exists(_modsPath))
            {
                Directory.CreateDirectory(_modsPath);
                _log.LogInfo("  Created HoboMods folder");
                CreateExampleMod();
                return;
            }
            
            var modFolders = Directory.GetDirectories(_modsPath);
            _log.LogInfo($"  Found {modFolders.Length} mod folder(s)");
            
            foreach (var modFolder in modFolders)
            {
                LoadMod(modFolder);
            }
            
            _log.LogInfo($"=== ModLoader: {_loadedMods.Count} mod(s) loaded ===");
        }
        
        private void LoadMod(string modFolder)
        {
            var modJsonPath = Path.Combine(modFolder, "mod.json");
            
            if (!File.Exists(modJsonPath))
            {
                _log.LogWarning($"  Skipping {Path.GetFileName(modFolder)}: no mod.json");
                return;
            }
            
            try
            {
                var json = File.ReadAllText(modJsonPath);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(json);
                
                if (manifest == null)
                {
                    _log.LogWarning($"  Failed to parse mod.json in {Path.GetFileName(modFolder)}");
                    return;
                }
                
                manifest.FolderPath = modFolder;
                _loadedMods.Add(manifest);
                
                _log.LogInfo($"  Loaded: {manifest.Name} v{manifest.Version} by {manifest.Author}");
            }
            catch (Exception ex)
            {
                _log.LogError($"  Error loading {Path.GetFileName(modFolder)}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Create example mod structure for new users
        /// </summary>
        private void CreateExampleMod()
        {
            var examplePath = Path.Combine(_modsPath, "_ExampleMod");
            Directory.CreateDirectory(examplePath);
            Directory.CreateDirectory(Path.Combine(examplePath, "items"));
            Directory.CreateDirectory(Path.Combine(examplePath, "recipes"));
            Directory.CreateDirectory(Path.Combine(examplePath, "assets", "icons"));
            Directory.CreateDirectory(Path.Combine(examplePath, "localization"));
            
            // Create example mod.json
            var manifest = new ModManifest
            {
                Id = "example_mod",
                Name = "Example Mod",
                Version = "1.0.0",
                Author = "YourName",
                Description = "An example mod to get you started",
                GameVersion = "1.0"
            };
            
            File.WriteAllText(
                Path.Combine(examplePath, "mod.json"),
                JsonConvert.SerializeObject(manifest, Formatting.Indented)
            );
            
            // Create example item
            var exampleItem = new ItemDefinition
            {
                Id = "example_potion",
                BaseItem = 1,
                Type = "consumable",
                Name = "@example_potion.name",
                Description = "@example_potion.desc",
                Icon = "assets/icons/example_potion.png",
                Price = 100,
                Effects = new List<EffectDefinition>
                {
                    new() { Stat = "health", Value = "50" },
                    new() { Stat = "food", Value = "25" }
                }
            };
            
            File.WriteAllText(
                Path.Combine(examplePath, "items", "example_potion.json"),
                JsonConvert.SerializeObject(exampleItem, Formatting.Indented)
            );
            
            // Create example localization
            var localization = new Dictionary<string, string>
            {
                ["example_potion.name"] = "Example Potion",
                ["example_potion.desc"] = "A simple healing potion"
            };
            
            File.WriteAllText(
                Path.Combine(examplePath, "localization", "en.json"),
                JsonConvert.SerializeObject(localization, Formatting.Indented)
            );
            
            _log.LogInfo("  Created _ExampleMod template");
        }
    }
    
    /// <summary>
    /// Mod manifest from mod.json
    /// </summary>
    public class ModManifest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";
        
        [JsonProperty("name")]
        public string Name { get; set; } = "";
        
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";
        
        [JsonProperty("author")]
        public string Author { get; set; } = "Unknown";
        
        [JsonProperty("description")]
        public string Description { get; set; } = "";
        
        [JsonProperty("gameVersion")]
        public string GameVersion { get; set; } = "";
        
        [JsonProperty("dependencies")]
        public List<string> Dependencies { get; set; } = new();
        
        // Runtime
        [JsonIgnore]
        public string FolderPath { get; set; } = "";
        
        // Cached localization dictionary (prevents reading file every time)
        [JsonIgnore]
        public Dictionary<string, string> LocalizationCache { get; set; } = null;
        
        // Dev hotkeys defined in mod.json
        [JsonProperty("devHotkeys")]
        public List<DevHotkeyDefinition> DevHotkeys { get; set; } = new();
    }
    
    /// <summary>
    /// Hotkey definition for mod development/testing
    /// Allows mods to define their own hotkeys in mod.json
    /// </summary>
    public class DevHotkeyDefinition
    {
        [JsonProperty("key")]
        public string Key { get; set; } = "";  // "Insert", "F6", etc.
        
        [JsonProperty("action")]
        public string Action { get; set; } = "";  // "spawn_item", "explore_items", etc.
        
        [JsonProperty("itemId")]  
        public string ItemId { get; set; } = "";  // Item ID (optional for some actions)
    }
    
    /// <summary>
    /// Item definition from items/*.json
    /// </summary>
    public class ItemDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";
        
        [JsonProperty("baseItem")]
        public uint BaseItem { get; set; } = 1;
        
        [JsonProperty("type")]
        public string Type { get; set; } = "consumable";
        
        [JsonProperty("name")]
        public string Name { get; set; } = "";
        
        [JsonProperty("description")]
        public string Description { get; set; } = "";
        
        [JsonProperty("icon")]
        public string Icon { get; set; } = "";
        
        [JsonProperty("price")]
        public int Price { get; set; } = 100;
        
        [JsonProperty("weight")]
        public float Weight { get; set; } = 0.1f;
        
        [JsonProperty("effects")]
        public List<EffectDefinition> Effects { get; set; } = new();
        
        // === Extended Item Properties (from DEV) ===
        
        [JsonProperty("rareColor")]
        public int RareColor { get; set; } = 0;  // Item rarity color (0=common)
        
        [JsonProperty("sellable")]
        public bool Sellable { get; set; } = true;  // Can be sold to vendors
        
        [JsonProperty("notForFire")]
        public bool NotForFire { get; set; } = false;  // Cannot be used as fire fuel
        
        [JsonProperty("isStockable")]
        public bool IsStockable { get; set; } = true;  // Can stack in inventory
        
        [JsonProperty("actualStockCount")]
        public int ActualStockCount { get; set; } = 1;  // Default stack size
        
        [JsonProperty("soundType")]
        public int SoundType { get; set; } = 0;  // Sound when used/picked up
        
        [JsonProperty("firerate")]
        public int Firerate { get; set; } = 0;  // Burn duration as fuel
        
        // === Reference Item Icon ===
        
        [JsonProperty("referenceItemId")]
        public uint ReferenceItemId { get; set; } = 0;  // Copy icon from existing game item
        
        // === Modify Existing Items ===
        
        [JsonProperty("isModify")]
        public bool IsModify { get; set; } = false;  // Patch existing item instead of creating new
        
        [JsonProperty("targetItemId")]
        public uint TargetItemId { get; set; } = 0;  // ID of vanilla item to modify
        
        // Gear-specific properties
        [JsonProperty("category")]
        public string Category { get; set; } = "";  // "hat", "jacket", "trousers", "shoes"
        
        [JsonProperty("warmResistance")]
        public int WarmResistance { get; set; } = 0;
        
        [JsonProperty("wetResistance")]
        public int WetResistance { get; set; } = 0;
        
        [JsonProperty("durabilityResistance")]
        public int DurabilityResistance { get; set; } = 100;
        
        // Weapon-specific properties
        [JsonProperty("attack")]
        public int Attack { get; set; } = 0;
        
        [JsonProperty("defense")]
        public int Defense { get; set; } = 0;
        
        [JsonProperty("criticalChance")]
        public int CriticalChance { get; set; } = 0;
        
        [JsonProperty("maxDurability")]
        public int MaxDurability { get; set; } = 100;
        
        // Gear stat bonuses (applies when equipped)
        [JsonProperty("stats")]
        public List<StatDefinition> Stats { get; set; } = new();
        
        // Bag-specific properties
        [JsonProperty("bagCapacity")]
        public float BagCapacity { get; set; } = 0f;
    }
    
    /// <summary>
    /// Stat bonus definition for gear items
    /// </summary>
    public class StatDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";
        
        [JsonProperty("value")]
        public int Value { get; set; } = 0;
    }
    
    /// <summary>
    /// Effect definition for consumables
    /// </summary>
    public class EffectDefinition
    {
        [JsonProperty("stat")]
        public string Stat { get; set; } = "";
        
        // Alias: Support "type" in JSON as fallback for "stat"
        [JsonProperty("type")]
        public string Type { set => Stat = string.IsNullOrEmpty(Stat) ? value : Stat; }
        
        [JsonProperty("value")]
        public string Value { get; set; } = "0";
    }
    
    /// <summary>
    /// Recipe definition from recipes/*.json
    /// </summary>
    public class RecipeDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";
        
        [JsonProperty("result")]
        public string Result { get; set; } = "";
        
        [JsonProperty("resultCount")]
        public int ResultCount { get; set; } = 1;
        
        [JsonProperty("name")]
        public string Name { get; set; } = "";
        
        [JsonProperty("bench")]
        public string Bench { get; set; } = "none";
        
        [JsonProperty("skillRequired")]
        public int SkillRequired { get; set; } = 0;
        
        [JsonProperty("autoUnlock")]
        public bool AutoUnlock { get; set; } = true;
        
        [JsonProperty("ingredients")]
        public List<IngredientDefinition> Ingredients { get; set; } = new();
    }
    
    /// <summary>
    /// Ingredient for recipes
    /// </summary>
    public class IngredientDefinition
    {
        [JsonProperty("item")]
        public uint Item { get; set; }
        
        [JsonProperty("count")]
        public int Count { get; set; } = 1;
    }
}
