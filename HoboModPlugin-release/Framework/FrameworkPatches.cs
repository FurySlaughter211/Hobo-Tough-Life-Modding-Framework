using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Game;
using UI;
using TMPro;
using Core.Strings;
using System.Collections.Generic;

namespace HoboModPlugin.Framework
{
    /// <summary>
    /// Central Harmony patches for the framework
    /// Handles effect application for all mod items and UI fixes
    /// </summary>
    [HarmonyPatch]
    public static class FrameworkPatches
    {
        private static EffectHandler _effectHandler;
        private static ItemRegistry _itemRegistry;
        private static RecipeRegistry _recipeRegistry;
        private static ManualLogSource _log;
        
        public static void Initialize(ManualLogSource log, ItemRegistry itemRegistry, EffectHandler effectHandler, RecipeRegistry recipeRegistry)
        {
            _log = log;
            _itemRegistry = itemRegistry;
            _effectHandler = effectHandler;
            _recipeRegistry = recipeRegistry;
        }
        
        /// <summary>
        /// Intercept Consumable.Use() for all mod items
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Consumable), "Use")]
        public static bool Consumable_Use_Prefix(Consumable __instance)
        {
            // Check if this is a mod item
            if (_itemRegistry == null || !_itemRegistry.IsModItem(__instance.id))
            {
                return true; // Not a mod item, let original run
            }
            
            try
            {
                // BUG #5 FIX: Use FindObjectsOfType - PlayerManager.GetComponent returns NULL in IL2CPP
                var characters = UnityEngine.Object.FindObjectsOfType<Character>();
                if (characters == null || characters.Length == 0)
                {
                    _log?.LogWarning("No Character found in scene");
                    return true;
                }
                
                var character = characters[0];
                if (character == null)
                {
                    _log?.LogWarning("Character[0] is null");
                    return true;
                }
                
                // Apply effects from definition
                _log?.LogInfo($"Applying mod effects for item {__instance.id}");
                _effectHandler?.ApplyItemEffects(__instance.id, character);
                
                // Let original run to handle item removal
                return true;
            }
            catch (System.Exception ex)
            {
                _log?.LogError($"FrameworkPatches error: {ex.Message}");
                return true;
            }
        }
        
        /// <summary>
        /// Hook into crafting UI to unlock framework recipes
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GUISheetScrollListFilterCrafting), "Load")]
        public static void CraftingUI_Load_Postfix()
        {
            try
            {
                if (_recipeRegistry == null || _recipeRegistry.Count == 0) return;
                
                var characters = Object.FindObjectsOfType<Character>();
                if (characters == null || characters.Length == 0) return;
                
                _recipeRegistry.UnlockPendingRecipes(characters[0]);
            }
            catch (System.Exception ex)
            {
                _log?.LogError($"Recipe unlock error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Override tooltip display for mod items to show correct effects
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GUISheetItemDetailContent), "ShowItem")]
        public static void ShowItem_Postfix(GUISheetItemDetailContent __instance, BaseItem _item)
        {
            try
            {
                if (_itemRegistry == null || _item == null) return;
                if (!_itemRegistry.IsModItem(_item.id)) return;
                
                var registered = _itemRegistry.GetItemByNumericId(_item.id);
                if (registered == null || registered.Definition?.Effects == null) return;
                
                // Get the consumable parameters array
                var consumableParams = __instance.parametersConsumable;
                if (consumableParams == null || consumableParams.Length == 0) return;
                
                var effects = registered.Definition.Effects;
                
                // First, hide all parameter slots
                for (int i = 0; i < consumableParams.Length; i++)
                {
                    var param = consumableParams[i];
                    if (param != null)
                    {
                        param.SetActivity(false);
                    }
                }
                
                // Now show our custom effects
                int slotIndex = 0;
                foreach (var effect in effects)
                {
                    if (slotIndex >= consumableParams.Length) break;
                    
                    var param = consumableParams[slotIndex];
                    if (param == null) continue;
                    
                    // Get the stat name and value
                    string statName = GetStatDisplayName(effect.Stat);
                    string valueText = FormatEffectValue(effect.Value);
                    
                    // Set the values using the string overload
                    param.SetValues(statName, valueText, null);
                    param.SetActivity(true);
                    
                    slotIndex++;
                }
                
                _log?.LogInfo($"[Tooltip] Updated display for {registered.Definition.Id} with {slotIndex} effects");
            }
            catch (System.Exception ex)
            {
                _log?.LogError($"ShowItem patch error: {ex.Message}");
            }
        }
        
        private static string GetStatDisplayName(string stat)
        {
            return stat?.ToLowerInvariant() switch
            {
                "health" => "Health",
                "food" => "Food",
                "morale" => "Morale",
                "freshness" => "Freshness",
                "warmth" => "Warmth",
                "stamina" => "Stamina",
                "illness" => "Illness",
                "toxicity" => "Toxicity",
                "wet" => "Wetness",
                _ => stat ?? "Unknown"
            };
        }
        
        private static string FormatEffectValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            if (value.ToLower() == "max") return "MAX";
            if (value == "0") return "0";
            
            if (value.StartsWith("+") || value.StartsWith("-"))
            {
                return value;
            }
            
            // Try to parse as number
            if (float.TryParse(value, out float num))
            {
                return num >= 0 ? $"+{num}" : num.ToString();
            }
            
            return value;
        }
        // Quest injection via GetFMQ_Map - intercept when game looks up our quest
        [HarmonyPatch(typeof(HBT_QuestDatabase), "GetFMQ_Map")]
        public static class QuestDatabase_GetFMQ_Map_Patch
        {
            public static bool Prefix(string questID, ref FMQ_Map __result, ref bool isOK)
            {
                // Check if this is one of our custom quests
                var quest = Plugin.Framework.QuestRegistry.GetQuestByGameId(questID);
                if (quest != null)
                {
                    _log.LogInfo($"=== GetFMQ_Map INTERCEPTED: {questID} ===");
                    __result = quest;
                    isOK = true;
                    return false; // Skip original
                }
                return true; // Continue to original
            }
        }

        // NOTE: ItemInfo_MeetCondition_Debug removed for performance - was logging every condition check

        // OnMakeAction handler - handles content re-injection and mod quest completion
        [HarmonyPatch(typeof(HBT_QuestManager), "OnMakeAction")]
        public static class OnMakeAction_Handler
        {
            public static void Prefix(FMQ_ActionWait.TypeOfActionWait type, uint _itemID, int _reciveCountitem, string _key)
            {
                // Re-inject content when world loads (Type.All with 0s is sent on world load)
                if (type == FMQ_ActionWait.TypeOfActionWait.All && _itemID == 0 && _reciveCountitem == 0)
                {
                    TryReinjectContent();
                }
                // NOTE: Verbose quest logging removed for performance
            }
            
            // POSTFIX: Manually complete mod quest nodes since native evaluation doesn't work
            public static void Postfix(FMQ_ActionWait.TypeOfActionWait type, uint _itemID, int _reciveCountitem, string _key)
            {
                // Only handle Item type
                if (type != FMQ_ActionWait.TypeOfActionWait.Item || _itemID == 0)
                    return;
                    
                try
                {
                    var quests = HBT_QuestManager.quests;
                    if (quests == null) return;
                    
                    for (int q = 0; q < quests.Count; q++)
                    {
                        var questInfo = quests[q];
                        if (questInfo == null || questInfo.myQuest == null) continue;
                        
                        var questID = questInfo.myQuest.QuestID;
                        
                        // Only handle mod quests (contain ":")
                        if (!questID.Contains(":")) continue;
                        
                        var inProgress = questInfo.inProgressNodes;
                        if (inProgress == null || inProgress.Count == 0) continue;
                        
                        // Check each in-progress node
                        for (int i = 0; i < inProgress.Count; i++)
                        {
                            var node = inProgress[i];
                            if (node?.aW == null) continue;
                            
                            // Check if this node listens for our item
                            if (node.aW.action != FMQ_ActionWait.TypeOfActionWait.Item) continue;
                            
                            var itemIds = node.aW.itemIDs;
                            if (itemIds == null) continue;
                            
                            bool matchesItem = false;
                            for (int j = 0; j < itemIds.Count; j++)
                            {
                                if (itemIds[j] == _itemID)
                                {
                                    matchesItem = true;
                                    break;
                                }
                            }
                            
                            if (!matchesItem) continue;
                            
                            _log.LogInfo($"=== MOD QUEST NODE MATCH for item {_itemID} ===");
                            _log.LogInfo($"    Quest: {questID}");
                            
                            // Get the node ID
                            string nodeID = null;
                            try { nodeID = node.id; } catch { }
                            if (string.IsNullOrEmpty(nodeID))
                            {
                                try { nodeID = node.aW.questNodeID; } catch { }
                            }
                            
                            if (string.IsNullOrEmpty(nodeID))
                            {
                                _log.LogWarning("    Could not determine node ID!");
                                continue;
                            }
                            
                            _log.LogInfo($"    Node ID: {nodeID}");
                            
                            // Check player inventory for required item count
                            var characters = UnityEngine.Object.FindObjectsOfType<Character>();
                            if (characters == null || characters.Length == 0) continue;
                            
                            int itemCount = characters[0].GetCountOfItemFromInventory(_itemID);
                            _log.LogInfo($"    Player has {itemCount} of item {_itemID}");
                            
                            // Check comparator (assume >=1 for now)
                            int requiredCount = 1;
                            var comparator = node.aW.myItemInfos?[0]?.comparator;
                            if (!string.IsNullOrEmpty(comparator))
                            {
                                // Parse ">=X" format
                                if (comparator.StartsWith(">="))
                                {
                                    int.TryParse(comparator.Substring(2), out requiredCount);
                                }
                            }
                            
                            _log.LogInfo($"    Required: {requiredCount} (comparator: {comparator})");
                            
                            if (itemCount >= requiredCount)
                            {
                                _log.LogInfo($"    CONDITION MET! Completing node...");
                                
                                // Complete the node manually!
                                questInfo.Final_NodeToDone(nodeID, true, 1, true);
                                
                                _log.LogInfo($"    Node completed successfully!");
                            }
                            else
                            {
                                _log.LogInfo($"    Condition not met yet ({itemCount} < {requiredCount})");
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    _log.LogError($"Error in mod quest completion handler: {ex.Message}\n{ex.StackTrace}");
                }
            }
            
            /// <summary>
            /// Re-inject mod content if it's missing from the databases
            /// Called when OnMakeAction(All,0,0) fires, which happens on world load
            /// </summary>
            private static void TryReinjectContent()
            {
                try
                {
                    // Check if recipes need re-injection (database reloaded without recreating object)
                    var recipes = RecipeDatabase.recipes;
                    if (recipes != null && !recipes.ContainsKey(51000))
                    {
                        _log.LogInfo("=== DETECTED: Mod recipes missing, re-injecting content ===");
                        
                        if (Plugin.Framework != null)
                        {
                            Plugin.Framework.InjectContent();
                            _log.LogInfo("=== Content re-injection complete ===");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    _log.LogError($"Error in TryReinjectContent: {ex.Message}");
                }
            }
        }



        // Translation bypass - modded quests use defaultText instead of loading from XML
        [HarmonyPatch(typeof(HBT_QuestTranslationManager), "GetTextQuestTitle")]
        public static class QuestTranslation_GetTextQuestTitle_Patch
        {
            public static bool Prefix(string questID, string defaultText, ref string __result)
            {
                // Modded quests have format "mod_id:quest_id"
                if (questID.Contains(":"))
                {
                    __result = defaultText ?? questID;
                    return false; // Skip original, use defaultText
                }
                return true; // Continue to original for vanilla quests
            }
        }

        // CRITICAL: Intercept translation loading to bypass file-based system
        // The game tries to load from: Assets/HoboThor/Quests/_translatedjson_/en/{questID}.json
        // For mod quests with ":" in the ID, this fails (invalid filename)
        [HarmonyPatch(typeof(HBT_QuestTranslationManager), "GetTranslationOfQuest")]
        public static class QuestTranslation_GetTranslationOfQuest_Patch
        {
            public static bool Prefix(string questID, ref HBT_QuestTranslationManager.QuestOneTranslation __result)
            {
                // Check if this is a mod quest
                if (!questID.Contains(":"))
                {
                    return true; // Vanilla quest, let original handle it
                }
                
                // Mod quest detected
                
                try
                {
                    // Get our quest definition
                    var registered = Plugin.Framework.QuestRegistry.GetRegisteredQuest(questID);
                    if (registered == null || registered.Definition == null)
                    {
                        __result = null;
                        return false;
                    }
                    
                    var def = registered.Definition;
                    
                    // Create QuestOneTranslation
                    var translation = new HBT_QuestTranslationManager.QuestOneTranslation();
                    translation.questID = questID;
                    
                    // Create TranslatedStrings with our text
                    var strings = new TranslatedStrings();
                    var keys = new Il2CppSystem.Collections.Generic.List<string>();
                    var values = new Il2CppSystem.Collections.Generic.List<string>();
                    var dict = new Il2CppSystem.Collections.Generic.Dictionary<string, string>();
                    
                    // Add title
                    keys.Add("title");
                    values.Add(def.Title ?? questID);
                    dict.Add("title", def.Title ?? questID);
                    
                    // Add all stage descriptions
                    foreach (var stage in def.Stages)
                    {
                        var nodeKey = stage.Id;
                        var nodeText = stage.Description ?? $"Stage: {stage.Id}";
                        
                        keys.Add(nodeKey);
                        values.Add(nodeText);
                        dict.Add(nodeKey, nodeText);
                        

                    }
                    
                    strings.keys = keys;
                    strings.values = values;
                    strings.translated = dict;
                    
                    translation.translatedStrings = strings;
                    

                    
                    __result = translation;
                    return false; // Skip original
                }
                catch (System.Exception ex)
                {
                    _log.LogError($"    Error creating translation: {ex.Message}\n{ex.StackTrace}");
                    __result = null;
                    return false;
                }
            }
        }

        [HarmonyPatch(typeof(HBT_QuestTranslationManager), "GetTextQuestNode", new System.Type[] { typeof(string), typeof(string) })]
        public static class QuestTranslation_GetTextQuestNode_Patch
        {
            public static bool Prefix(string questID, string nodeID, ref string __result)
            {
                try
                {
                    // Modded quests have format "mod_id:quest_id"
                    if (questID.Contains(":"))
                    {
                        // Try to find the actual description from our registry
                        var quest = Plugin.Framework.QuestRegistry.GetRegisteredQuest(questID);
                        if (quest != null)
                        {
                            // Simplest approach: Use the Definition stages
                            if (quest.Definition != null && quest.Definition.Stages != null)
                            {
                                var stage = quest.Definition.Stages.Find(s => s.Id == nodeID);
                                if (stage != null && !string.IsNullOrEmpty(stage.Description))
                                {
                                    __result = stage.Description;
                                    return false;
                                }
                            }
                        }
                        
                        // Fallback
                        __result = $"Quest stage: {nodeID}";
                        return false;
                    }
                }
                catch (System.Exception ex)
                {
                    _log.LogError($"Error in QuestTranslation patch: {ex.Message}");
                    // Fallback on error
                    if (questID.Contains(":"))
                    {
                        __result = $"Error: {nodeID}";
                        return false;
                    }
                }
                return true;
            }
        }
        
        // Correct patch for GetTextQuestNode which takes FMQ_Node
        [HarmonyPatch(typeof(HBT_QuestTranslationManager), "GetTextQuestNode", new System.Type[] { typeof(FMQ_Node) })]
        public static class QuestTranslation_GetTextQuestNode_Node_Patch
        {
            public static bool Prefix(FMQ_Node node, ref string __result)
            {
                try
                {
                    if (node == null) return true;
                    
                    string questID = null;
                    string nodeID = null;
                    
                    // Try to read node.id directly
                    try { nodeID = node.id; } catch { }
                    
                    // From ActionWait
                    if (node.aW != null)
                    {
                        try { questID = node.aW.questID; } catch { }
                        if (string.IsNullOrEmpty(nodeID))
                        {
                            try { nodeID = node.aW.questNodeID; } catch { }
                        }
                    }
                    
                    // From QuestMap
                    if (string.IsNullOrEmpty(questID) && node.questMap != null)
                    {
                        try { questID = node.questMap.QuestID; } catch { }
                    }
                    
                    // Only handle mod quests
                    if (!string.IsNullOrEmpty(questID) && questID.Contains(":"))
                    {
                        var quest = Plugin.Framework.QuestRegistry.GetRegisteredQuest(questID);
                        if (quest != null && quest.Definition != null)
                        {
                            // Try to find specific stage
                            if (!string.IsNullOrEmpty(nodeID))
                            {
                                var stage = quest.Definition.Stages.Find(s => s.Id == nodeID);
                                if (stage != null && !string.IsNullOrEmpty(stage.Description))
                                {
                                    __result = stage.Description;
                                    return false;
                                }
                            }
                            
                            // Fallback to first stage
                            if (quest.Definition.Stages.Count > 0)
                            {
                                var firstStage = quest.Definition.Stages[0];
                                if (!string.IsNullOrEmpty(firstStage.Description))
                                {
                                    __result = firstStage.Description;
                                    return false;
                                }
                            }
                            
                            __result = "Quest Stage (No Description)";
                            return false;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    _log.LogError($"Error in GetTextQuestNode_Node patch: {ex.Message}");
                }
                return true;
            }
        }
        
        // ==================== Custom Bag Patches ====================
        
        /// <summary>
        /// Patch Bag.Clone to handle custom bags properly
        /// Custom bags return the instance directly to avoid shallow copy issues
        /// </summary>
        [HarmonyPatch(typeof(Bag), nameof(Bag.Clone), new System.Type[] { typeof(bool) })]
        public static class Bag_Clone_Patch
        {
            static bool Prefix(Bag __instance, bool wantSame, ref BaseItem __result)
            {
                if (ItemRegistry.IsCustomBag(__instance.id))
                {
                    // For custom bags, return the instance directly and mark as unequipped
                    // This mimics the EA version behavior and prevents clone issues
                    __result = __instance;
                    __instance.isEquiped = false;
                    
                    _log?.LogDebug($"[BagPatch] Custom bag {__instance.id} clone handled");
                    return false; // Skip original
                }
                return true; // Run original for vanilla bags
            }
        }
        
        /// <summary>
        /// Patch Bag.Load to skip loading for custom bags
        /// Custom bags don't need the vanilla Load() behavior
        /// </summary>
        [HarmonyPatch(typeof(Bag), nameof(Bag.Load))]
        public static class Bag_Load_Patch
        {
            static bool Prefix(Bag __instance)
            {
                if (ItemRegistry.IsCustomBag(__instance.id))
                {
                    // Skip Load for custom bags (EA version Load was empty)
                    _log?.LogDebug($"[BagPatch] Custom bag {__instance.id} load skipped");
                    return false;
                }
                return true;
            }
        }
    }
}
