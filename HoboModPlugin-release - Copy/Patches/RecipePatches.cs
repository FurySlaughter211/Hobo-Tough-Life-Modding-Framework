using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Game;

namespace HoboModPlugin.Patches
{
    /// <summary>
    /// Harmony patch to trigger framework content injection when RecipeDatabase loads
    /// </summary>
    [HarmonyPatch(typeof(RecipeDatabase), nameof(RecipeDatabase.OnAwake))]
    public static class RecipeDatabasePatch
    {
        // First mod recipe ID to check for injection
        private const uint FIRST_MOD_RECIPE_ID = 51000;
        
        [HarmonyPrefix]
        public static void Prefix()
        {
            try
            {
                Plugin.Log.LogInfo("=== RecipeDatabase.OnAwake PREFIX ===");
                
                var recipes = RecipeDatabase.recipes;
                if (recipes == null)
                {
                    Plugin.Log.LogInfo("  Recipes dict is NULL at prefix - will try postfix");
                    return;
                }
                
                Plugin.Log.LogInfo($"  Recipes at PREFIX: {recipes.Count}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Prefix error: {ex.Message}");
            }
        }
        
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                Plugin.Log.LogInfo("=== RecipeDatabase.OnAwake POSTFIX ===");
                
                var recipes = RecipeDatabase.recipes;
                if (recipes == null || recipes.Count == 0)
                {
                    Plugin.Log.LogWarning("RecipeDatabase not ready yet");
                    return;
                }
                
                Plugin.Log.LogInfo($"  Recipes at POSTFIX: {recipes.Count}");
                
                // FIX: Check if mod recipes already exist in THIS database instance
                // This handles the case where RecipeDatabase is recreated (new world, load different save)
                // The old static flag approach didn't work because flags persist but databases are recreated
                if (recipes.ContainsKey(FIRST_MOD_RECIPE_ID))
                {
                    Plugin.Log.LogInfo("  Mod recipes already exist in this database, skipping injection");
                    return;
                }
                
                // Inject ALL framework content (items, recipes, etc.)
                if (Plugin.Framework != null)
                {
                    Plugin.Framework.InjectContent();
                }
                
                Plugin.Log.LogInfo($"  Total recipes after injection: {RecipeDatabase.recipes.Count}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Postfix error: {ex.Message}");
            }
        }
    }
}
