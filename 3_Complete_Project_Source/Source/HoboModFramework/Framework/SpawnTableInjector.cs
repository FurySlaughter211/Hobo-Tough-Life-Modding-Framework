using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Game;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace HoboModPlugin.Framework
{
    public class LootInjectionRequest
    {
        public string TableID { get; set; }

        public int Amount { get; set; }

        public uint ItemID { get; set; }

        public float DropChance { get; set; }

        public int _pricePercent { get; set; }


    }

    [HarmonyPatch(typeof(SpawnItemTableDatabase), nameof(SpawnItemTableDatabase.Init))]
    public static class SpawnTableInjector
    {


        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
        }

        public static List<LootInjectionRequest> LootInjectionRequests = new List<LootInjectionRequest>();

        public static void RegisterBinLoot(string TableID, int Amount, uint ItemID, float DropChance, int _pricePercent)
        {
            LootInjectionRequests.Add(new LootInjectionRequest
            {
                TableID = TableID,
                Amount = Amount,
                ItemID = ItemID,
                DropChance = DropChance,
                _pricePercent = _pricePercent
            });

        }





        public static void LoadLootFromFolder(ModManifest mod)
        {
            var lootPath = System.IO.Path.Combine(mod.FolderPath, "loot");

            if (!System.IO.Directory.Exists(lootPath))
            {
                return;
            }

            var lootFiles = System.IO.Directory.GetFiles(lootPath, "*.json");

            foreach (var file in lootFiles)
            {

                try
                {
                    var json = System.IO.File.ReadAllText(file);

                    var definitions = Newtonsoft.Json.JsonConvert.DeserializeObject<List<LootDefinition>>(json);

                    if (definitions == null) continue;

                    foreach (var def in definitions)
                    {
                        uint finalItemID;

                        if (uint.TryParse(def.ItemID, out uint vanillaId))
                        {
                            finalItemID = vanillaId;
                        }
                        else
                        {
                            string fullId = def.ItemID.Contains(":") ? def.ItemID : $"{mod.Id}:{def.ItemID}";
                            finalItemID = ItemRegistry.GetDeterministicId(fullId);
                        }

                        RegisterBinLoot(def.TableID, def.Amount, finalItemID, def.DropChance, def._pricePercent);

                    }


                }
                catch (Exception ex)
                {
                    _log?.LogError($"Failed to load loot from {file}: {ex.Message}");

                }


            }


        }
        public static void Clear()
        {
            LootInjectionRequests.Clear(); //Clear the loot Injection Requests when reloading mods so no dublicates get created
        }


        [HarmonyPostfix]
        public static void Postfix()
        {
            var binTables = SpawnItemTableDatabase.tablesBin;


            foreach (var entry in binTables)
            {
                bool wasModified = false;

                foreach (var lootRequest in LootInjectionRequests)
                {
                    if (lootRequest.TableID == "*" || lootRequest.TableID == entry.Key)
                    {
                        var card = new SpawnItemTable_ItemInfo(lootRequest.ItemID, lootRequest.Amount, false, lootRequest._pricePercent);

                        var bucket = new SpawnItemTableChance();
                        bucket.percent = lootRequest.DropChance;
                        bucket.items = new Il2CppSystem.Collections.Generic.List<SpawnItemTable_ItemInfo>();
                        bucket.items.Add(card);

                        entry.Value.DropChances.Add(bucket);
                        wasModified = true;
                    }
                }

                if (wasModified)
                {
                    entry.Value.RecountProbabilities();
                }
            }

        }

    }
    public class LootDefinition
    {
        [Newtonsoft.Json.JsonProperty("TableID")]
        public string TableID { get; set; } = "";

        [Newtonsoft.Json.JsonProperty("ItemID")]
        public string ItemID { get; set; } = "";

        [Newtonsoft.Json.JsonProperty("Amount")]
        public int Amount { get; set; } = 0;

        [Newtonsoft.Json.JsonProperty("DropChance")]
        public float DropChance { get; set; } = 0;

        [Newtonsoft.Json.JsonProperty("PricePercent")]
        public int _pricePercent { get; set; } = 0;


    }

}
