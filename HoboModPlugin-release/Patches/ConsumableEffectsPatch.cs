using HarmonyLib;
using Game;
using UnityEngine;

namespace HoboModPlugin.Patches
{
    /// <summary>
    /// Harmony patch for custom consumable effects.
    /// Intercepts Consumable.Use() to apply effects from JSON-defined items.
    /// Uses the EffectHandler from the Framework.
    /// </summary>
    [HarmonyPatch]
    public static class ConsumableEffectsPatch
    {
        /// <summary>
        /// PREFIX patch on Consumable.Use() - apply custom item effects
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Consumable), "Use")]
        public static bool Use_Prefix(Consumable __instance)
        {
            try
            {
                // Check if this is a mod item (ID >= 60000)
                if (__instance.id < 60000)
                {
                    return true; // Vanilla item - let original run
                }
                
                // Get player character using PlayerManager (not FindObjectsOfType which may return NPCs)
                var playerManager = PlayerManager.instance;
                if (playerManager == null)
                {
                    return true;
                }
                
                var character = playerManager.GetComponent<Character>();
                if (character == null)
                {
                    character = playerManager.GetComponentInChildren<Character>();
                }
                
                if (character == null)
                {
                    return true;
                }
                
                // Apply effects using the Framework's EffectHandler
                if (Plugin.Framework?.EffectHandler != null)
                {
                    Plugin.Framework.EffectHandler.ApplyItemEffects(__instance.id, character);
                }
                
                // Return true to let original Use() run (handles item removal)
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"ConsumableEffectsPatch error: {ex.Message}");
                return true;
            }
        }
    }
}
