using System.Collections.Generic;
using BepInEx.Logging;
using Game;
using HarmonyLib;
using UI;
using UnityEngine;

namespace HoboModPlugin.Framework
{
    // Manages inner-layer clothing slots. Activated only if a mod opts in via enableLayering in mod.json.
    [HarmonyPatch]
    public static class LayeringManager
    {
        private static ManualLogSource _log;
        private static bool _isActive = false;
        private static bool _isInnerMode = false;
        private static bool _allowInnerSearch = false;

        // One inner item per gear category
        private static readonly Dictionary<Gear.ECategory, Gear> _innerLayer = new();

        public static bool IsActive => _isActive;
        public static bool IsInnerMode => _isInnerMode;

        // Called from FrameworkManager constructor — registers patches but keeps them inert
        public static void Initialize(ManualLogSource log)
        {
            _log = log;
        }

        // Called from FrameworkManager.DiscoverMods() when a mod declares enableLayering: true
        public static void Activate()
        {
            if (_isActive) return;
            _isActive = true;
            _log?.LogInfo("[Layering] Layered clothing system activated.");
        }

        // --- Inner layer API ---

        // Helper to swap native Character fields with our inner layer items
        private static void SwapGearFields(Gear.ECategory cat, Gear swapIn, out Gear originalOut)
        {
            originalOut = null;
            var character = Character.Instance;
            if (character == null) return;

            if (cat == Gear.ECategory.Jacket)
            {
                originalOut = character.jacket;
                character.jacket = swapIn;
            }
            else if (cat == Gear.ECategory.Trousers)
            {
                originalOut = character.trousers;
                character.trousers = swapIn;
            }
            else if (cat == Gear.ECategory.Shoes)
            {
                originalOut = character.shoes;
                character.shoes = swapIn;
            }
            else if (cat == Gear.ECategory.Hat)
            {
                originalOut = character.hat;
                character.hat = swapIn;
            }
        }

        public static void SetInnerGear(Gear gear)
        {
            if (gear == null || Character.Instance == null) return;

            var cat = gear.category;
            _innerLayer.TryGetValue(cat, out var existingInner);

            _allowInnerSearch = true;
            try
            {
                // 1. Temporarily place existing inner gear in native field
                SwapGearFields(cat, existingInner, out var outerItem);

                // 2. Call vanilla SetGear (handles inventory removal, weight, SFX, stats)
                Character.Instance.SetGear(gear);

                // 3. Save the newly equipped item from native field as our inner gear
                SwapGearFields(cat, outerItem, out var newInner);
                _innerLayer[cat] = newInner;
            }
            finally
            {
                _allowInnerSearch = false;
            }

            _log?.LogInfo($"[Layering] Inner {cat} equipped: {gear.title}");
        }

        public static void RemoveInnerGear(Gear.ECategory category)
        {
            if (Character.Instance == null) return;
            if (!_innerLayer.TryGetValue(category, out var gear) || gear == null) return;

            _allowInnerSearch = true;
            try
            {
                // 1. Temporarily place inner gear in native field
                SwapGearFields(category, gear, out var outerItem);

                // 2. Call vanilla DeleteGear (handles inventory return, weight, stats)
                Character.Instance.DeleteGear(gear, true, false);

                // 3. Clear our inner layer slot and restore outer gear
                SwapGearFields(category, outerItem, out _);
                _innerLayer.Remove(category);
            }
            finally
            {
                _allowInnerSearch = false;
            }

            _log?.LogInfo($"[Layering] Inner {category} removed.");
        }

        public static Gear GetInnerGear(Gear.ECategory category)
        {
            _innerLayer.TryGetValue(category, out var gear);
            return gear;
        }

        public static void ToggleMode()
        {
            _isInnerMode = !_isInnerMode;
            _log?.LogInfo($"[Layering] Mode: {(_isInnerMode ? "Inner" : "Outer")}");
        }

        // Refreshes the gear table display after any state change
        private static void RefreshGearTable()
        {
            var table = Object.FindObjectOfType<GUISheetInventoryGearTable>();
            table?.Load();

            var invTable = Object.FindObjectOfType<GUISheetScrollItemTableInventory>();
            invTable?.Load();
        }

        // --- Patch: L key toggles inner/outer mode ---

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GUISheetInventoryGearTable), "OnUpdate")]
        public static void GearTable_OnUpdate_Postfix()
        {
            if (!_isActive) return;

            if (Input.GetKeyDown(KeyCode.L))
            {
                ToggleMode();
                RefreshGearTable();
            }
        }

        // --- Patch: after vanilla Load() fills slots, swap to inner items when in inner mode ---

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GUISheetInventoryGearTable), "Load")]
        public static void GearTable_Load_Postfix(GUISheetInventoryGearTable __instance)
        {
            if (!_isActive || !_isInnerMode) return;

            try
            {
                var slots = __instance.GetComponentsInChildren<SlotInventoryGear>();
                if (slots == null || slots.Length < 4) return;

                // Slot indices 0-3 match Hat/Jacket/Trousers/Shoes (same order as vanilla Load)
                var categories = new[]
                {
                    Gear.ECategory.Hat,
                    Gear.ECategory.Jacket,
                    Gear.ECategory.Trousers,
                    Gear.ECategory.Shoes
                };

                for (int i = 0; i < categories.Length; i++)
                {
                    var slot = slots[i];
                    if (slot == null) continue;

                    _innerLayer.TryGetValue(categories[i], out var inner);
                    slot.item = inner; // null here correctly shows an empty slot
                }
            }
            catch (System.Exception ex)
            {
                _log?.LogError($"[Layering] Load postfix error: {ex.Message}");
            }
        }

        // --- Patch: route equip to inner layer when in inner mode ---
        // Only intercepts Gear items. Weapons/companions/bags pass through normally.

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SlotInventory), "ActionAfterPressA")]
        public static bool SlotInventory_ActionAfterPressA_Prefix(SlotInventory __instance)
        {
            if (!_isActive || !_isInnerMode) return true;

            try
            {
                var gear = __instance.item?.TryCast<Gear>();
                if (gear == null) return true; // not gear, let vanilla handle it

                SetInnerGear(gear);
                RefreshGearTable();
                return false; // skip vanilla equip
            }
            catch (System.Exception ex)
            {
                _log?.LogError($"[Layering] Equip intercept error: {ex.Message}");
                return true;
            }
        }

        // --- Patch: route unequip to inner layer when in inner mode ---

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SlotInventoryGear), "ActionAfterPressA")]
        public static bool SlotInventoryGear_ActionAfterPressA_Prefix(SlotInventoryGear __instance)
        {
            if (!_isActive || !_isInnerMode) return true;

            try
            {
                var gear = __instance.item?.TryCast<Gear>();
                if (gear == null) return true;

                RemoveInnerGear(gear.category);
                RefreshGearTable();
                return false; // skip vanilla unequip
            }
            catch (System.Exception ex)
            {
                _log?.LogError($"[Layering] Unequip intercept error: {ex.Message}");
                return true;
            }
        }

        // --- Patch: include inner layer items in gear degradation ---
        // Replaces ApplyGearDegradation so inner items are part of the random decay pick.

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "ApplyGearDegradation")]
        public static bool Character_ApplyGearDegradation_Prefix(Character __instance)
        {
            // If no inner items are tracked there's nothing extra to do, let vanilla run
            if (!_isActive || _innerLayer.Count == 0) return true;

            try
            {
                var config = GameConfiguration.Instance;
                if (config == null || Game.GameConfiguration.goldenConfig == null || Game.GameConfiguration.goldenConfig.character == null) return true;

                // Build candidate list — replicates vanilla logic without type-unsafe bag cast
                var candidates = new Il2CppSystem.Collections.Generic.List<Gear>();

                if (__instance.jacket != null && __instance.jacket.actualOdor < Gear.SMELL_POTENTIAL)
                    candidates.Add(__instance.jacket);
                if (__instance.trousers != null && __instance.trousers.actualOdor < Gear.SMELL_POTENTIAL)
                    candidates.Add(__instance.trousers);
                if (__instance.shoes != null && __instance.shoes.actualOdor < Gear.SMELL_POTENTIAL)
                    candidates.Add(__instance.shoes);

                // Add inner layer items on top
                foreach (var kvp in _innerLayer)
                {
                    if (kvp.Value != null && kvp.Value.actualOdor < Gear.SMELL_POTENTIAL)
                        candidates.Add(kvp.Value);
                }

                if (candidates.Count == 0) return false;

                int idx = UnityEngine.Random.Range(0, candidates.Count);
                candidates[idx].ChangeSmell(Game.GameConfiguration.goldenConfig.character.gearChangeSmell_Point);

                return false; // replaced vanilla entirely
            }
            catch (System.Exception ex)
            {
                _log?.LogError($"[Layering] Degradation patch error: {ex.Message}");
                return true; // fall back to vanilla on error
            }
        }

        // --- Patch: hide inner items from GetGearFromInventory when _allowInnerSearch is false ---

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetGearFromInventory")]
        public static bool Character_GetGearFromInventory_Prefix(ref Gear __result, Gear item, bool isEquiped, out int index)
        {
            index = -1;
            if (!_isActive) return true;

            // If the game searches for an equipped item, but we are not executing an inner layer swap,
            // hide the inner items from the search so the vanilla search only finds outer items.
            if (isEquiped && !_allowInnerSearch && item != null)
            {
                foreach (var kvp in _innerLayer)
                {
                    if (kvp.Value == item)
                    {
                        __result = null;
                        index = -1;
                        return false; // Skip vanilla search and return null
                    }
                }
            }

            return true; // Run vanilla search
        }

        // --- Patch: clear session state when a new Character is initialized ---

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "OnAwake")]
        public static void Character_OnAwake_Postfix()
        {
            _innerLayer.Clear();
            _isInnerMode = false;
            _log?.LogInfo("[Layering] Session state cleared for new Character.");
        }
    }
}
