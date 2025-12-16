using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Game;
using Core.Strings;
using System.IO;
using Il2CppSystem.Collections.Generic;

namespace HoboModPlugin.Features
{
    /// <summary>
    /// Custom item creation with custom icon, localization, AND custom effects!
    /// </summary>
    public static class CustomItemMods
    {
        public const uint CUSTOM_ITEM_ID = 60001;
        public const string CUSTOM_ITEM_NAME = "Super Potion";
        public const string CUSTOM_ITEM_DESC = "A magical potion that restores all stats to full!";
        public const string ICON_FILENAME = "super_potion_icon.png";
        
        // Custom localization keys
        public const uint CUSTOM_TITLE_KEY = 999001;
        public const uint CUSTOM_DESC_KEY = 999002;
        
        private static bool _itemInjected = false;
        private static bool _localizationInjected = false;
        private static Sprite _customIcon = null;
        
        /// <summary>
        /// Inject custom text into the localization system
        /// </summary>
        public static void InjectLocalization()
        {
            if (_localizationInjected) return;
            
            try
            {
                var stringsItems = StringsManager.strings_items;
                if (stringsItems == null) return;
                
                var dict = stringsItems.translatedInt;
                if (dict == null) return;
                
                dict[CUSTOM_TITLE_KEY] = CUSTOM_ITEM_NAME;
                dict[CUSTOM_DESC_KEY] = CUSTOM_ITEM_DESC;
                
                Plugin.Log.LogInfo($"  Localization: {CUSTOM_TITLE_KEY}->'{CUSTOM_ITEM_NAME}'");
                _localizationInjected = true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"  Localization error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load custom icon from Assets folder
        /// </summary>
        private static Sprite LoadCustomIcon()
        {
            try
            {
                var pluginPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var pluginDir = Path.GetDirectoryName(pluginPath);
                var iconPath = Path.Combine(pluginDir, "Assets", ICON_FILENAME);
                
                if (!File.Exists(iconPath))
                    iconPath = Path.Combine(pluginDir, ICON_FILENAME);
                
                if (!File.Exists(iconPath)) return null;
                
                byte[] imageData = File.ReadAllBytes(iconPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, imageData)) return null;
                
                Plugin.Log.LogInfo($"  Icon loaded: {texture.width}x{texture.height}");
                
                return Sprite.Create(texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
            catch { return null; }
        }
        
        /// <summary>
        /// Set custom effects by MODIFYING EXISTING Change objects IN PLACE
        /// This avoids list assignment issues with IL2CPP
        /// </summary>
        private static void SetCustomEffects(Consumable consumable)
        {
            try
            {
                Plugin.Log.LogInfo("  === Setting Custom Effects ===");
                
                var changes = consumable.changes;
                if (changes == null)
                {
                    Plugin.Log.LogWarning("    changes list is null!");
                    return;
                }
                
                Plugin.Log.LogInfo($"    Original count: {changes.Count}");
                
                // Log original values
                for (int i = 0; i < changes.Count; i++)
                {
                    var c = changes[i];
                    Plugin.Log.LogInfo($"    ORIGINAL [{i}]: {c.influence} = {c.normalValue}");
                }
                
                // Modify FIRST item in place to be Health +100
                if (changes.Count > 0)
                {
                    var firstChange = changes[0];
                    Plugin.Log.LogInfo($"    Modifying [0] from {firstChange.influence}={firstChange.normalValue}");
                    firstChange.influence = Consumable.ParameterType.Health;
                    firstChange.typeChange = Consumable.TypeChanges.Normal;
                    firstChange.normalValue = 100;
                    firstChange.isAddictedValue = 100;
                    Plugin.Log.LogInfo($"    Modified [0] to Health=100");
                }
                
                // Add MORE effects
                var foodChange = new Consumable.Change();
                foodChange.influence = Consumable.ParameterType.Food;
                foodChange.typeChange = Consumable.TypeChanges.Normal;
                foodChange.normalValue = 100;
                foodChange.isAddictedValue = 100;
                changes.Add(foodChange);
                Plugin.Log.LogInfo("    Added: Food +100");
                
                var moraleChange = new Consumable.Change();
                moraleChange.influence = Consumable.ParameterType.Morale;
                moraleChange.typeChange = Consumable.TypeChanges.Normal;
                moraleChange.normalValue = 100;
                moraleChange.isAddictedValue = 100;
                changes.Add(moraleChange);
                Plugin.Log.LogInfo("    Added: Morale +100");
                
                var freshnessChange = new Consumable.Change();
                freshnessChange.influence = Consumable.ParameterType.Freshness;
                freshnessChange.typeChange = Consumable.TypeChanges.Normal;
                freshnessChange.normalValue = 100;
                freshnessChange.isAddictedValue = 100;
                changes.Add(freshnessChange);
                Plugin.Log.LogInfo("    Added: Freshness +100");
                
                var warmChange = new Consumable.Change();
                warmChange.influence = Consumable.ParameterType.Warm;
                warmChange.typeChange = Consumable.TypeChanges.Normal;
                warmChange.normalValue = 100;
                warmChange.isAddictedValue = 100;
                changes.Add(warmChange);
                Plugin.Log.LogInfo("    Added: Warm +100");
                
                var staminaChange = new Consumable.Change();
                staminaChange.influence = Consumable.ParameterType.Stamina;
                staminaChange.typeChange = Consumable.TypeChanges.Normal;
                staminaChange.normalValue = 100;
                staminaChange.isAddictedValue = 100;
                changes.Add(staminaChange);
                Plugin.Log.LogInfo("    Added: Stamina +100");
                
                // Verify
                Plugin.Log.LogInfo($"    Final count: {changes.Count}");
                for (int i = 0; i < changes.Count; i++)
                {
                    var c = changes[i];
                    Plugin.Log.LogInfo($"    FINAL [{i}]: {c.influence} = {c.normalValue}");
                }
                
                Plugin.Log.LogInfo("  === Custom Effects Applied! ===");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"  Effects error: {ex.Message}");
                Plugin.Log.LogError($"  Stack: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Inject a custom item into the ItemDatabase
        /// </summary>
        public static void InjectCustomItem()
        {
            if (_itemInjected) return;
            
            try
            {
                var items = ItemDatabase.items;
                if (items == null || items.Count == 0) return;
                
                if (items.ContainsKey(CUSTOM_ITEM_ID))
                {
                    _itemInjected = true;
                    return;
                }
                
                Plugin.Log.LogInfo("=== Creating Custom Item ===");
                
                InjectLocalization();
                _customIcon = LoadCustomIcon();
                
                // Find a consumable to clone
                BaseItem sourceItem = null;
                uint[] consumableIds = { 1, 2, 3, 4, 5 };
                foreach (var id in consumableIds)
                {
                    if (items.ContainsKey(id) && items[id] != null)
                    {
                        sourceItem = items[id];
                        Plugin.Log.LogInfo($"  Source item ID: {id}");
                        break;
                    }
                }
                
                if (sourceItem == null)
                {
                    foreach (var entry in items)
                    {
                        if (entry.Value != null) { sourceItem = entry.Value; break; }
                    }
                }
                
                if (sourceItem == null) return;
                
                var cloned = sourceItem.Clone();
                if (cloned == null) return;
                
                // Set basic properties
                cloned.id = CUSTOM_ITEM_ID;
                cloned.titleKey = CUSTOM_TITLE_KEY;
                cloned.descriptionKey = CUSTOM_DESC_KEY;
                cloned.price = 500;
                
                // Set custom icon
                if (_customIcon != null)
                {
                    cloned.icon = _customIcon;
                    Plugin.Log.LogInfo("  Custom icon assigned!");
                }
                
                // Add to database FIRST
                items[CUSTOM_ITEM_ID] = cloned;
                Plugin.Log.LogInfo($"  Added to database: ID={cloned.id}");
                
                // THEN modify effects on the stored item
                var storedItem = items[CUSTOM_ITEM_ID];
                var consumable = storedItem.TryCast<Consumable>();
                if (consumable != null)
                {
                    SetCustomEffects(consumable);
                }
                else
                {
                    Plugin.Log.LogWarning("  Could not cast to Consumable");
                }
                
                Plugin.Log.LogInfo("=== Custom Item Injected! ===");
                
                _itemInjected = true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Custom item error: {ex.Message}");
            }
        }
        
        public static bool IsInjected => _itemInjected;
        public static Sprite CustomIcon => _customIcon;
    }
}
