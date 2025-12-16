using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using Game;
using Core.Strings;

namespace HoboModPlugin.Framework
{
    /// <summary>
    /// Registry for custom items - handles loading from JSON and injection into game
    /// </summary>
    public class ItemRegistry
    {
        private readonly ManualLogSource _log;
        private readonly Dictionary<string, RegisteredItem> _items = new();
        private readonly Dictionary<uint, string> _idToStringId = new();
        
        private uint _nextItemId = 60000;
        
        public ItemRegistry(ManualLogSource log)
        {
            _log = log;
        }
        
        /// <summary>
        /// Load all items from a mod's items folder
        /// </summary>
        public void LoadItemsFromMod(ModManifest mod)
        {
            var itemsPath = Path.Combine(mod.FolderPath, "items");
            if (!Directory.Exists(itemsPath)) return;
            
            var itemFiles = Directory.GetFiles(itemsPath, "*.json");
            _log.LogInfo($"  Loading {itemFiles.Length} item(s) from {mod.Name}");
            
            foreach (var itemFile in itemFiles)
            {
                try
                {
                    var json = File.ReadAllText(itemFile);
                    var definition = Newtonsoft.Json.JsonConvert.DeserializeObject<ItemDefinition>(json);
                    
                    if (definition == null) continue;
                    
                    RegisterItem(mod, definition);
                }
                catch (Exception ex)
                {
                    _log.LogError($"    Failed to load {Path.GetFileName(itemFile)}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Register an item definition
        /// </summary>
        private void RegisterItem(ModManifest mod, ItemDefinition definition)
        {
            var numericId = _nextItemId++;
            var fullId = $"{mod.Id}:{definition.Id}";
            
            var registered = new RegisteredItem
            {
                FullId = fullId,
                NumericId = numericId,
                Mod = mod,
                Definition = definition,
                LocalizationKey = (uint)(990000 + numericId)
            };
            
            _items[fullId] = registered;
            _idToStringId[numericId] = fullId;
            
            _log.LogInfo($"    Registered: {definition.Id} -> ID {numericId}");
        }
        
        /// <summary>
        /// Inject all registered items into the game's ItemDatabase
        /// </summary>
        public void InjectAllItems()
        {
            var items = ItemDatabase.items;
            if (items == null || items.Count == 0)
            {
                _log.LogWarning("ItemDatabase not ready");
                return;
            }
            
            _log.LogInfo($"=== ItemRegistry: Injecting {_items.Count} items ===");
            
            foreach (var entry in _items)
            {
                InjectItem(entry.Value);
            }
        }
        
        private void InjectItem(RegisteredItem registered)
        {
            try
            {
                var definition = registered.Definition;
                var items = ItemDatabase.items;
                
                // Skip if already injected
                if (items.ContainsKey(registered.NumericId)) return;
                
                // Find source item to clone
                if (!items.ContainsKey(definition.BaseItem))
                {
                    _log.LogWarning($"    Base item {definition.BaseItem} not found for {definition.Id}");
                    return;
                }
                
                var sourceItem = items[definition.BaseItem];
                var cloned = sourceItem.Clone();
                
                // Configure basic properties
                cloned.id = registered.NumericId;
                cloned.titleKey = registered.LocalizationKey;
                cloned.descriptionKey = registered.LocalizationKey + 1;
                cloned.price = definition.Price;
                
                // Load and assign icon
                var icon = LoadIcon(registered);
                if (icon != null)
                {
                    cloned.icon = icon;
                }
                
                // Handle Consumable items - clear base effects (shallow copy issue)
                var consumable = cloned.TryCast<Consumable>();
                if (consumable != null && definition.Type == "consumable")
                {
                    
                    // CRITICAL FIX: BaseItem.Clone() likely does a shallow copy of lists.
                    // Clearing the list on the clone wipes the ORIGINAL item's effects!
                    // Instead, assign NEW empty lists to decouple from the original.
                    
                    if (consumable.changes != null)
                    {
                        consumable.changes = new Il2CppSystem.Collections.Generic.List<Consumable.Change>();
                    }
                    
                    if (consumable.buffChanges != null)
                    {
                        consumable.buffChanges = new Il2CppSystem.Collections.Generic.List<Consumable.BuffChange>();
                    }
                    
                    if (consumable.parameterChanges != null)
                    {
                        consumable.parameterChanges = new Il2CppSystem.Collections.Generic.List<Consumable.ParameterChange>();
                    }
                }
                
                // Handle Gear items (clothing)
                var gear = cloned.TryCast<Gear>();
                if (gear != null && definition.Type == "gear")
                {
                    
                    // Set category
                    gear.category = ParseGearCategory(definition.Category);
                    
                    // Set resistances
                    gear.warmResistance = definition.WarmResistance;
                    gear.wetResistance = definition.WetResistance;
                    gear.durabilityResistance = definition.DurabilityResistance;
                    
                    // Clear base item's parameterChanges (shallow copy issue)
                    gear.parameterChanges = new Il2CppSystem.Collections.Generic.List<Gear.ParameterChange>();
                    
                    // Add stat bonuses from JSON
                    if (definition.Stats != null && definition.Stats.Count > 0)
                    {
                        foreach (var stat in definition.Stats)
                        {
                            var paramType = ParseStatType(stat.Type);
                            if (paramType.HasValue)
                            {
                                var change = new Gear.ParameterChange();
                                change.influencedParameterType = paramType.Value;
                                change.value = stat.Value;
                                change.finalValue = stat.Value;
                                gear.parameterChanges.Add(change);
                                
                                _log.LogInfo($"    Added stat: {stat.Type} = {stat.Value}");
                            }
                        }
                    }
                }
                
                // Handle Weapon items
                var weapon = cloned.TryCast<Weapon>();
                if (weapon != null && definition.Type == "weapon")
                {
                    // Set weapon stats
                    weapon.attack = definition.Attack;
                    weapon.defense = definition.Defense;
                    weapon.criticalChance = definition.CriticalChance;
                    weapon.maxDurability = definition.MaxDurability;
                    weapon.actualDurability = definition.MaxDurability;
                }
                
                // Inject localization
                InjectLocalization(registered);
                
                // Add to database
                items[registered.NumericId] = cloned;
                registered.GameItem = cloned;
                
                _log.LogInfo($"    Injected: {definition.Id} (ID: {registered.NumericId})");
            }
            catch (Exception ex)
            {
                _log.LogError($"    Failed to inject {registered.Definition.Id}: {ex.Message}");
            }
        }
        
        private Sprite LoadIcon(RegisteredItem registered)
        {
            if (string.IsNullOrEmpty(registered.Definition.Icon)) return null;
            
            var iconPath = Path.Combine(registered.Mod.FolderPath, registered.Definition.Icon);
            if (!File.Exists(iconPath)) return null;
            
            try
            {
                var imageData = File.ReadAllBytes(iconPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, imageData)) return null;
                
                return Sprite.Create(texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
            catch
            {
                return null;
            }
        }
        
        private void InjectLocalization(RegisteredItem registered)
        {
            try
            {
                var stringsItems = StringsManager.strings_items;
                if (stringsItems?.translatedInt == null) return;
                
                var dict = stringsItems.translatedInt;
                var def = registered.Definition;
                
                // Resolve localization key or use literal
                var name = ResolveLocalizedString(registered, def.Name);
                var desc = ResolveLocalizedString(registered, def.Description);
                
                dict[registered.LocalizationKey] = name;
                dict[registered.LocalizationKey + 1] = desc;
            }
            catch { }
        }
        
        private string ResolveLocalizedString(RegisteredItem registered, string key)
        {
            // If starts with @, look up in mod's localization
            if (key.StartsWith("@"))
            {
                var locKey = key.Substring(1);
                var mod = registered.Mod;
                
                // Load and cache localization if not already cached
                if (mod.LocalizationCache == null)
                {
                    mod.LocalizationCache = LoadModLocalization(mod.FolderPath);
                }
                
                // Look up in cache
                if (mod.LocalizationCache.TryGetValue(locKey, out var value))
                {
                    return value;
                }
                
                return locKey; // Fallback to key itself
            }
            
            return key; // Literal string
        }
        
        /// <summary>
        /// Load localization file for a mod (cached after first load)
        /// </summary>
        private Dictionary<string, string> LoadModLocalization(string modFolderPath)
        {
            var locPath = Path.Combine(modFolderPath, "localization", "en.json");
            
            if (File.Exists(locPath))
            {
                try
                {
                    var json = File.ReadAllText(locPath);
                    var loc = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    return loc ?? new Dictionary<string, string>();
                }
                catch 
                {
                    _log.LogWarning($"Failed to load localization: {locPath}");
                }
            }
            
            return new Dictionary<string, string>(); // Empty cache if no file
        }
        
        /// <summary>
        /// Get registered item by string ID
        /// </summary>
        public RegisteredItem GetItem(string fullId)
        {
            return _items.TryGetValue(fullId, out var item) ? item : null;
        }
        
        /// <summary>
        /// Get registered item by numeric ID
        /// </summary>
        public RegisteredItem GetItemByNumericId(uint id)
        {
            if (_idToStringId.TryGetValue(id, out var fullId))
            {
                return GetItem(fullId);
            }
            return null;
        }
        
        /// <summary>
        /// Parse gear category string to enum
        /// </summary>
        private Gear.ECategory ParseGearCategory(string category)
        {
            return category?.ToLower() switch
            {
                "hat" => Gear.ECategory.Hat,
                "jacket" => Gear.ECategory.Jacket,
                "trousers" => Gear.ECategory.Trousers,
                "shoes" => Gear.ECategory.Shoes,
                _ => Gear.ECategory.Jacket  // Default to jacket
            };
        }
        
        /// <summary>
        /// Parse stat type string to BaseParameter.Type
        /// </summary>
        private BaseParameter.Type? ParseStatType(string statType)
        {
            return statType?.ToLower() switch
            {
                // Core stats
                "health" => BaseParameter.Type.Health,
                "food" => BaseParameter.Type.Food,
                "morale" => BaseParameter.Type.Morale,
                "freshness" or "odour" => BaseParameter.Type.Freshness,
                "warm" or "warmth" => BaseParameter.Type.Warm,
                "wet" or "dryness" => BaseParameter.Type.Wet,
                
                // Status effects
                "illness" => BaseParameter.Type.Illness,
                "toxicity" => BaseParameter.Type.Toxicity,
                "alcohol" => BaseParameter.Type.Alcohol,
                "greatneed" => BaseParameter.Type.Greatneed,
                "smell" => BaseParameter.Type.Smell,
                
                // Resistances
                "smellresistance" => BaseParameter.Type.SmellResistance,
                "wetresistance" => BaseParameter.Type.WetResistance,
                "warmresistance" => BaseParameter.Type.WarmResistance,
                "toxicityresistance" => BaseParameter.Type.ToxicityResistance,
                "immunity" => BaseParameter.Type.Immunity,
                
                // Combat stats
                "attack" => BaseParameter.Type.Attack,
                "defense" => BaseParameter.Type.Defense,
                "charism" or "charisma" => BaseParameter.Type.Charism,
                
                // Capacity and energy
                "capacity" => BaseParameter.Type.Capacity,
                "stamina" => BaseParameter.Type.Stamina,
                "gearsmell" => BaseParameter.Type.GearSmell,
                "grit" or "energy" => BaseParameter.Type.Grit,
                "gritmax" => BaseParameter.Type.GritMax,
                "courage" => BaseParameter.Type.Courage,
                "couragemax" => BaseParameter.Type.CourageMax,
                
                _ => null
            };
        }
        
        /// <summary>
        /// Check if a numeric ID belongs to a mod item
        /// </summary>
        public bool IsModItem(uint id) => _idToStringId.ContainsKey(id);
    }
    
    /// <summary>
    /// Runtime registered item data
    /// </summary>
    public class RegisteredItem
    {
        public string FullId { get; set; } = "";
        public uint NumericId { get; set; }
        public uint LocalizationKey { get; set; }
        public ModManifest Mod { get; set; }
        public ItemDefinition Definition { get; set; }
        public BaseItem GameItem { get; set; }
    }
}
