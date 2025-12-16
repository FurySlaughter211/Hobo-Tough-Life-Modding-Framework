using BepInEx.Logging;
using UnityEngine;
using Game;

namespace HoboModPlugin.Features
{
    /// <summary>
    /// Cheat and trainer-style features: God mode, teleport, position saving
    /// </summary>
    public static class CheatMods
    {
        // State for persistent god mode
        private static bool _persistentGodMode = false;
        private static Character _cachedCharacter = null;
        private static float _lastGodModeUpdate = 0f;
        private const float GOD_MODE_UPDATE_INTERVAL = 0.5f;
        
        // State for teleport
        private static Vector3 _savedPosition;
        private static bool _hasSavedPosition = false;
        
        public static bool PersistentGodModeEnabled => _persistentGodMode;
        
        /// <summary>
        /// Toggle persistent god mode on/off
        /// </summary>
        public static void TogglePersistentGodMode()
        {
            _persistentGodMode = !_persistentGodMode;
            Plugin.Log.LogInfo($"=== F1: Persistent God Mode: {(_persistentGodMode ? "ON" : "OFF")} ===");
        }
        
        /// <summary>
        /// Apply god mode if enabled (call every frame or Update)
        /// </summary>
        public static void ApplyIfEnabled()
        {
            if (_persistentGodMode)
            {
                ApplyPersistentGodMode();
            }
        }
        
        /// <summary>
        /// One-time god mode activation with logging
        /// </summary>
        public static void ActivateGodMode()
        {
            try
            {
                var characters = Object.FindObjectsOfType<Character>();
                if (characters == null || characters.Length == 0)
                {
                    Plugin.Log.LogWarning("No Character components found in scene");
                    return;
                }

                var character = characters[0];
                if (character == null)
                {
                    Plugin.Log.LogWarning("Character is null");
                    return;
                }

                Plugin.Log.LogInfo("=== ACTIVATING GOD MODE ===");
                
                // Fill positive stats to max
                if (character.Health != null)
                {
                    character.Health.value = character.Health.actualMax;
                    Plugin.Log.LogInfo($"  Health set to MAX: {character.Health.actualMax:F1}");
                }

                if (character.Food != null)
                {
                    character.Food.value = character.Food.actualMax;
                    Plugin.Log.LogInfo($"  Food set to MAX: {character.Food.actualMax:F1}");
                }

                if (character.Morale != null)
                {
                    character.Morale.value = character.Morale.actualMax;
                    Plugin.Log.LogInfo($"  Morale set to MAX: {character.Morale.actualMax:F1}");
                }

                if (character.Stamina != null)
                {
                    character.Stamina.value = character.Stamina.actualMax;
                    Plugin.Log.LogInfo($"  Stamina set to MAX: {character.Stamina.actualMax:F1}");
                }

                if (character.Freshness != null)
                {
                    character.Freshness.value = character.Freshness.actualMax;
                    Plugin.Log.LogInfo($"  Freshness set to MAX: {character.Freshness.actualMax:F1}");
                }

                if (character.Warm != null)
                {
                    character.Warm.value = character.Warm.actualMax;
                    Plugin.Log.LogInfo($"  Warmth set to MAX: {character.Warm.actualMax:F1}");
                }

                // Clear negative stats
                if (character.Illness != null)
                {
                    character.Illness.value = 0f;
                    Plugin.Log.LogInfo("  Illness CURED!");
                }

                if (character.Toxicity != null)
                {
                    character.Toxicity.value = 0f;
                    Plugin.Log.LogInfo("  Toxicity CLEARED!");
                }

                if (character.Wet != null)
                {
                    character.Wet.value = character.Wet.actualMax;
                    Plugin.Log.LogInfo($"  Dryness set to MAX: {character.Wet.actualMax:F1}");
                }

                // Give cash bonus
                int oldCash = character.cash;
                character.cash = oldCash + 1000;
                Plugin.Log.LogInfo($"  Cash: {oldCash} -> {character.cash} (+1000)");

                Plugin.Log.LogInfo("=== GOD MODE ACTIVATED! ===");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error in God Mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Silent god mode update (no logging)
        /// </summary>
        private static void ApplyPersistentGodMode()
        {
            try
            {
                if (Time.time - _lastGodModeUpdate < GOD_MODE_UPDATE_INTERVAL)
                    return;
                _lastGodModeUpdate = Time.time;

                if (_cachedCharacter == null)
                {
                    var characters = Object.FindObjectsOfType<Character>();
                    if (characters == null || characters.Length == 0) return;
                    _cachedCharacter = characters[0];
                }
                
                if (_cachedCharacter == null) return;

                // Fill stats silently
                if (_cachedCharacter.Health != null) _cachedCharacter.Health.value = _cachedCharacter.Health.actualMax;
                if (_cachedCharacter.Food != null) _cachedCharacter.Food.value = _cachedCharacter.Food.actualMax;
                if (_cachedCharacter.Morale != null) _cachedCharacter.Morale.value = _cachedCharacter.Morale.actualMax;
                if (_cachedCharacter.Stamina != null) _cachedCharacter.Stamina.value = _cachedCharacter.Stamina.actualMax;
                if (_cachedCharacter.Freshness != null) _cachedCharacter.Freshness.value = _cachedCharacter.Freshness.actualMax;
                if (_cachedCharacter.Warm != null) _cachedCharacter.Warm.value = _cachedCharacter.Warm.actualMax;
                if (_cachedCharacter.Grit != null) _cachedCharacter.Grit.value = _cachedCharacter.Grit.actualMax;  // Energy
                if (_cachedCharacter.Wet != null) _cachedCharacter.Wet.value = _cachedCharacter.Wet.actualMax;    // Dryness
                
                // Clear negative stats
                if (_cachedCharacter.Illness != null) _cachedCharacter.Illness.value = 0f;
                if (_cachedCharacter.Toxicity != null) _cachedCharacter.Toxicity.value = 0f;
            }
            catch 
            { 
                _cachedCharacter = null;
            }
        }

        /// <summary>
        /// Save current player position
        /// </summary>
        public static void SavePosition()
        {
            try
            {
                var playerManager = PlayerManager.instance;
                if (playerManager == null)
                {
                    Plugin.Log.LogWarning("PlayerManager not found");
                    return;
                }

                var transform = playerManager.transform;
                if (transform == null)
                {
                    Plugin.Log.LogWarning("Player transform not found");
                    return;
                }

                _savedPosition = transform.position;
                _hasSavedPosition = true;
                Plugin.Log.LogInfo($"=== F2: Position Saved ===");
                Plugin.Log.LogInfo($"  Location: ({_savedPosition.x:F2}, {_savedPosition.y:F2}, {_savedPosition.z:F2})");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error saving position: {ex.Message}");
            }
        }

        /// <summary>
        /// Teleport to saved position
        /// </summary>
        public static void TeleportToSavedPosition()
        {
            try
            {
                if (!_hasSavedPosition)
                {
                    Plugin.Log.LogWarning("No saved position! Press F2 first to save a position.");
                    return;
                }

                var playerManager = PlayerManager.instance;
                if (playerManager == null)
                {
                    Plugin.Log.LogWarning("PlayerManager not found");
                    return;
                }

                var transform = playerManager.transform;
                if (transform == null)
                {
                    Plugin.Log.LogWarning("Player transform not found");
                    return;
                }

                var oldPos = transform.position;
                transform.position = _savedPosition;
                
                Plugin.Log.LogInfo($"=== F3: Teleported! ===");
                Plugin.Log.LogInfo($"  From: ({oldPos.x:F2}, {oldPos.y:F2}, {oldPos.z:F2})");
                Plugin.Log.LogInfo($"  To: ({_savedPosition.x:F2}, {_savedPosition.y:F2}, {_savedPosition.z:F2})");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error teleporting: {ex.Message}");
            }
        }
    }
}
