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
                
                // Clear base item's changes/effects for consumables
                // This prevents the base item's effects from showing in tooltip
                // Note: Use TryCast for IL2CPP compatibility instead of 'is' pattern
                _log.LogInfo($"    Cloned type: {cloned.GetType().Name}");
                var consumable = cloned.TryCast<Consumable>();
                if (consumable != null)
                {
                    _log.LogInfo($"    Cast to Consumable successful, clearing changes...");
                    
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

                    _log.LogInfo($"    Replaced base item effect lists with new empty lists");
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
                var locPath = Path.Combine(registered.Mod.FolderPath, "localization", "en.json");
                
                if (File.Exists(locPath))
                {
                    try
                    {
                        var json = File.ReadAllText(locPath);
                        var loc = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (loc != null && loc.TryGetValue(locKey, out var value))
                        {
                            return value;
                        }
                    }
                    catch { }
                }
                
                return locKey; // Fallback to key itself
            }
            
            return key; // Literal string
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
