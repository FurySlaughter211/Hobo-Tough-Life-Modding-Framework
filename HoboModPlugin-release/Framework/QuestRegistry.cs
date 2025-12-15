using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using Game;
using Newtonsoft.Json;
using Il2CppInterop.Runtime;

namespace HoboModPlugin.Framework
{
    public class QuestRegistry
    {
        private readonly ManualLogSource _log;
        private readonly Dictionary<string, RegisteredQuest> _quests = new();

        public QuestRegistry(ManualLogSource log)
        {
            _log = log;
        }

        public void LoadQuestsFromMod(ModManifest mod)
        {
            var questsPath = Path.Combine(mod.FolderPath, "quests");
            _log.LogInfo($"  Checking for quests in: {questsPath}");
            if (!Directory.Exists(questsPath)) 
            {
                 _log.LogInfo("  No quests folder found.");
                 return;
            }

            var questFiles = Directory.GetFiles(questsPath, "*.json");
            _log.LogInfo($"  Loading {questFiles.Length} quest(s) from {mod.Name}");

            foreach (var file in questFiles)
            {
                try
                {
                    _log.LogInfo($"    Parsing quest file: {file}");
                    var json = File.ReadAllText(file);
                    var definition = JsonConvert.DeserializeObject<QuestDefinition>(json);
                    
                    if (definition == null) 
                    {
                        _log.LogWarning($"    Failed to deserialize quest: {file}");
                        continue;
                    }

                    var fullId = $"{mod.Id}:{definition.Id}";
                    _quests[fullId] = new RegisteredQuest
                    {
                        FullId = fullId,
                        Mod = mod,
                        Definition = definition
                    };
                    
                    _log.LogInfo($"    Registered quest: {definition.Id} ({fullId})");
                }
                catch (Exception ex)
                {
                    _log.LogError($"    Failed to load quest {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        public void InjectAllQuests()
        {
            _log.LogInfo($"=== Injecting {_quests.Count} Quests ===");

            foreach (var entry in _quests)
            {
                BuildQuestObject(entry.Value);
            }
        }

        /// <summary>
        /// Get a quest FMQ_Map by its game ID (for GetFMQ_Map interception)
        /// </summary>
        public FMQ_Map GetQuestByGameId(string gameId)
        {
            if (_quests.TryGetValue(gameId, out var registered))
            {
                if (registered.BuiltQuest == null)
                {
                    BuildQuestObject(registered);
                }
                return registered.BuiltQuest;
            }
            return null;
        }

        /// <summary>
        /// Get the full RegisteredQuest object (including definition)
        /// </summary>
        public RegisteredQuest GetRegisteredQuest(string gameId)
        {
            if (_quests.TryGetValue(gameId, out var registered))
            {
                return registered;
            }
            return null;
        }

        private void BuildQuestObject(RegisteredQuest registered)
        {
            try
            {
                var def = registered.Definition;
                
                // Create Quest Object
                var questMap = new FMQ_Map(); 
                
                // Set Properties using Utils
                FrameworkUtils.SetIl2CppField(questMap, "questID", registered.FullId);
                FrameworkUtils.SetIl2CppField(questMap, "questTitle", def.Title ?? registered.FullId);
                FrameworkUtils.SetIl2CppField(questMap, "isPermanent", def.IsPermanent);
                
                // Create storage for Nodes
                var qNodes = new Il2CppSystem.Collections.Generic.List<FMQ_Node>();
                var allNodes = new Il2CppSystem.Collections.Generic.Dictionary<string, FMQ_Node>();
                
                FrameworkUtils.SetIl2CppField(questMap, "qnodes", qNodes);
                FrameworkUtils.SetIl2CppField(questMap, "allNodes", allNodes);
                
                // Create all Nodes
                var stageToIndex = new Dictionary<string, int>();
                var nodes = new List<FMQ_Node>();
                
                int index = 0;
                foreach (var stage in def.Stages)
                {
                    var node = new FMQ_Node();
                    // Set node ID and map reference - CRITICAL for text lookup
                    FrameworkUtils.SetIl2CppField(node, "id", stage.Id);
                    FrameworkUtils.SetIl2CppField(node, "questMap", questMap);
                    
                    if (stage.Type == "action")
                    {
                        SetupActionNode(node, stage.Action);
                    }
                    else if (stage.Type == "dialogue")
                    {
                        SetupDialogueNode(node, stage.Dialogue, questMap);
                    }
                    else if (stage.Type == "wait")
                    {
                        SetupWaitNode(node, stage.Wait, stage.Description, questMap, stage.Id);
                    }
                    else
                    {
                        FrameworkUtils.SetIl2CppField(node, "action", FMQ_Node.ETypeAction.ActionNow);
                    }
                    
                    qNodes.Add(node);
                    allNodes.Add(stage.Id, node);
                    nodes.Add(node);
                    
                    stageToIndex[stage.Id] = index;
                    index++;
                }
                
                if (nodes.Count > 0)
                {
                    FrameworkUtils.SetIl2CppField(questMap, "firstNode", nodes[0]);
                    FrameworkUtils.SetIl2CppField(questMap, "firstIndx", 0);
                }
                
                // Link Next Stages
                for (int i = 0; i < def.Stages.Count; i++)
                {
                    var stage = def.Stages[i];
                    var node = nodes[i];
                    
                    var nextList = new Il2CppSystem.Collections.Generic.List<int>();
                    
                    foreach (var nextId in stage.NextStages)
                    {
                        if (stageToIndex.TryGetValue(nextId, out int nextIdx))
                        {
                            nextList.Add(nextIdx);
                        }
                    }
                    
                    FrameworkUtils.SetIl2CppField(node, "nextIndxs", nextList);
                }

                // Cache the built quest
                registered.BuiltQuest = questMap;
                _log.LogInfo($"    Built Quest: {registered.FullId} with {nodes.Count} stages");
            }
            catch (Exception ex)
            {
                _log.LogError($"    Failed to build quest {registered.FullId}: {ex.Message}");
            }
        }
        
        private void SetupActionNode(FMQ_Node node, ActionDefinition actionDef)
        {
            FrameworkUtils.SetIl2CppField(node, "action", FMQ_Node.ETypeAction.ActionNow);
            
            var actionNow = new FMQ_ActionNow();
            
            var typeEnum = ParseActionType(actionDef.Type);
            FrameworkUtils.SetIl2CppField(actionNow, "action", typeEnum);
            
            var pars = new Il2CppSystem.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(actionDef.Param))
            {
                pars.Add(actionDef.Param);
            }
            FrameworkUtils.SetIl2CppField(actionNow, "pars", pars);
            
            FrameworkUtils.SetIl2CppField(node, "aN", actionNow);
        }
        
        private void SetupDialogueNode(FMQ_Node node, DialogueDefinition diagDef, FMQ_Map questMap)
        {
            FrameworkUtils.SetIl2CppField(node, "action", FMQ_Node.ETypeAction.ActionConv);
            
            var actionConv = new FMQ_ActionConv();
            
            if (diagDef != null)
            {
                FrameworkUtils.SetIl2CppField(actionConv, "note", diagDef.Text ?? "...");
                
                var optsList = new Il2CppSystem.Collections.Generic.List<FMC_Option>();
                
                foreach (var optDef in diagDef.Options)
                {
                    var optPtr = IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<FMC_Option>.NativeClassPtr);
                    var opt = new FMC_Option(optPtr);
                    
                    FrameworkUtils.SetIl2CppField(opt, "text", optDef.Text ?? "Continue");
                    
                    optsList.Add(opt);
                }
                
                FrameworkUtils.SetIl2CppField(actionConv, "opts", optsList);
            }
            
            FrameworkUtils.SetIl2CppField(node, "aC", actionConv);
        }
        
        private FMQ_ActionNow.TypeOfActionNow ParseActionType(string type)
        {
            return type.ToLowerInvariant() switch
            {
                "item" => FMQ_ActionNow.TypeOfActionNow.Item,
                "give_item" => FMQ_ActionNow.TypeOfActionNow.Item,
                "start_quest" => FMQ_ActionNow.TypeOfActionNow.StartQuest,
                "quest_done" => FMQ_ActionNow.TypeOfActionNow.QuestDone,
                "quest_fail" => FMQ_ActionNow.TypeOfActionNow.QuestFail,
                "skill" => FMQ_ActionNow.TypeOfActionNow.Skill,
                "rep" => FMQ_ActionNow.TypeOfActionNow.Rep,
                "spawn" => FMQ_ActionNow.TypeOfActionNow.ObjectsSpawn,
                "remove" => FMQ_ActionNow.TypeOfActionNow.ObjectsRemove,
                _ => FMQ_ActionNow.TypeOfActionNow.Nothing
            };
        }
        
        private void SetupWaitNode(FMQ_Node node, WaitDefinition waitDef, string description, FMQ_Map questMap, string nodeID)
        {
            FrameworkUtils.SetIl2CppField(node, "action", FMQ_Node.ETypeAction.ActionWait);
            
            var actionWait = new FMQ_ActionWait();
            
            // Set identification - CRITICAL
            FrameworkUtils.SetIl2CppField(actionWait, "questID", questMap.QuestID);
            FrameworkUtils.SetIl2CppField(actionWait, "questNodeID", nodeID);
            
            // Set description note
            actionWait.isNote = true;
            actionWait.note = description ?? "Complete this objective";
            actionWait.doneAftSuc = true; // Auto-complete when condition met
            
            if (waitDef?.Type == "item")
            {
                // Set action type to Item
                actionWait.action = FMQ_ActionWait.TypeOfActionWait.Item;
                
                // Get the item from database
                var item = ItemDatabase.GetItem((uint)waitDef.ItemId);
                if (item != null)
                {
                    // Create ItemInfo list
                    var itemInfos = new Il2CppSystem.Collections.Generic.List<FMQ_ActionWait.ItemInfo>();
                    
                    var itemInfo = new FMQ_ActionWait.ItemInfo();
                    itemInfo.item = item;
                    itemInfo.comparator = $">={waitDef.Count}";
                    
                    // CRITICAL: Create and assign FMC_Condition
                    // The game uses this to evaluate if the condition is met
                    var conditionStr = $"item_{waitDef.ItemId}>={waitDef.Count}";
                    var condition = new FMC_Condition(conditionStr, false);
                    itemInfo.myCondition = condition;
                    
                    itemInfos.Add(itemInfo);
                    actionWait.myItemInfos = itemInfos;
                    
                    // ALSO populate itemIDs list - critical for event filtering!
                    var ids = new Il2CppSystem.Collections.Generic.List<uint>();
                    ids.Add((uint)waitDef.ItemId);
                    actionWait.itemIDs = ids;
                    
                    _log.LogInfo($"      Created wait condition: collect {waitDef.Count}x item {waitDef.ItemId} with FMC_Condition");
                }
                else
                {
                    _log.LogWarning($"      Item {waitDef.ItemId} not found for wait condition");
                }
            }
            
            FrameworkUtils.SetIl2CppField(node, "aW", actionWait);
        }
    }

    public class RegisteredQuest
    {
        public string FullId { get; set; }
        public ModManifest Mod { get; set; }
        public QuestDefinition Definition { get; set; }
        public FMQ_Map BuiltQuest { get; set; }
    }

    public class QuestDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool IsPermanent { get; set; }
        public List<QuestStageDefinition> Stages { get; set; } = new();
    }

    public class QuestStageDefinition
    {
        public string Id { get; set; }
        public string Type { get; set; } // "dialogue", "action", "wait"
        public string Description { get; set; }
        public string Npc { get; set; }
        public DialogueDefinition Dialogue { get; set; }
        public ActionDefinition Action { get; set; }
        public WaitDefinition Wait { get; set; }
        public List<string> NextStages { get; set; } = new();
    }

    public class DialogueDefinition
    {
        public string Text { get; set; }
        public List<DialogueOptionDefinition> Options { get; set; } = new();
    }

    public class DialogueOptionDefinition
    {
        public string Text { get; set; }
        public string NextStage { get; set; }
        public string Condition { get; set; }
    }

    public class ActionDefinition
    {
        public string Type { get; set; } // "give_item", etc
        public string Param { get; set; }
    }

    public class WaitDefinition
    {
        [Newtonsoft.Json.JsonProperty("type")]
        public string Type { get; set; } // "item", etc
        
        [Newtonsoft.Json.JsonProperty("item_id")]
        public int ItemId { get; set; }
        
        [Newtonsoft.Json.JsonProperty("count")]
        public int Count { get; set; } = 1;
    }
}
