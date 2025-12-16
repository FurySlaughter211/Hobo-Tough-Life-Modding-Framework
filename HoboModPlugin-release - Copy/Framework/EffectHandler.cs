using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using Game;

namespace HoboModPlugin.Framework
{
    /// <summary>
    /// Handles applying effects from item definitions to the player
    /// </summary>
    public class EffectHandler
    {
        private readonly ManualLogSource _log;
        private readonly ItemRegistry _itemRegistry;
        
        // Effect applicators
        private readonly Dictionary<string, Action<Character, string>> _effectApplicators = new();
        
        public EffectHandler(ManualLogSource log, ItemRegistry itemRegistry)
        {
            _log = log;
            _itemRegistry = itemRegistry;
            
            RegisterBuiltInEffects();
        }
        
        private void RegisterBuiltInEffects()
        {
            // Positive stats (higher = better)
            _effectApplicators["health"] = ApplyRangedStat((c) => c.Health);
            _effectApplicators["food"] = ApplyRangedStat((c) => c.Food);
            _effectApplicators["morale"] = ApplyRangedStat((c) => c.Morale);
            
            // Energy = Grit (mental energy, displayed as "Energy" in UI)
            _effectApplicators["energy"] = ApplyRangedStat((c) => c.Grit);
            _effectApplicators["grit"] = ApplyRangedStat((c) => c.Grit);
            
            // Stamina = physical endurance (for running/sprinting, internal stat)
            _effectApplicators["stamina"] = ApplyRangedStat((c) => c.Stamina);
            _effectApplicators["fatigue"] = ApplyRangedStat((c) => c.Stamina);
            _effectApplicators["tiredness"] = ApplyRangedStat((c) => c.Stamina);
            
            _effectApplicators["warmth"] = ApplyRangedStat((c) => c.Warm);
            _effectApplicators["warm"] = ApplyRangedStat((c) => c.Warm);
            
            // Negative/Inverse stats (lower = better, 0=good, 100=bad)
            _effectApplicators["illness"] = ApplyRangedStat((c) => c.Illness);
            _effectApplicators["toxicity"] = ApplyRangedStat((c) => c.Toxicity);
            _effectApplicators["wet"] = ApplyRangedStat((c) => c.Wet);
            _effectApplicators["wetness"] = ApplyRangedStat((c) => c.Wet);
            // NOTE: UI "Dryness" shows Wet value directly (not inverted)
            // So dryness=max means Wet=max which shows as Dryness 100%
            _effectApplicators["dryness"] = ApplyRangedStat((c) => c.Wet);
            _effectApplicators["freshness"] = ApplyRangedStat((c) => c.Freshness); // 0=clean, 100=smelly
            _effectApplicators["odour"] = ApplyRangedStat((c) => c.Freshness);
            _effectApplicators["odor"] = ApplyRangedStat((c) => c.Freshness);
            _effectApplicators["smell"] = ApplyRangedStat((c) => c.Freshness);
        }
        
        private Action<Character, string> ApplyInverseRangedStat(Func<Character, ParameterRange> getter)
        {
            return (character, value) =>
            {
                var param = getter(character);
                if (param == null) return;
                
                // Invert: max -> 0, min -> max
                if (value.Equals("max", StringComparison.OrdinalIgnoreCase))
                {
                    param.value = 0;
                }
                else if (value.Equals("min", StringComparison.OrdinalIgnoreCase))
                {
                    param.value = param.actualMax;
                }
                else
                {
                    param.value = ParseValue(value, param.value, param.actualMax);
                }
            };
        }
        
        private Action<Character, string> ApplyRangedStat(Func<Character, ParameterRange> getter)
        {
            return (character, value) =>
            {
                var param = getter(character);
                if (param == null) return;
                
                param.value = ParseValue(value, param.value, param.actualMax);
            };
        }
        
        /// <summary>
        /// Parse effect value string
        /// </summary>
        private float ParseValue(string value, float current, float max)
        {
            // Special values
            if (value.Equals("max", StringComparison.OrdinalIgnoreCase))
                return max;
            
            if (value.Equals("min", StringComparison.OrdinalIgnoreCase))
                return 0;
            
            // Relative values
            if (value.StartsWith("add:"))
            {
                if (float.TryParse(value.Substring(4), out var addVal))
                    return current + addVal;
            }
            
            if (value.StartsWith("+"))
            {
                if (float.TryParse(value.Substring(1), out var addVal))
                    return current + addVal;
            }
            
            if (value.StartsWith("-"))
            {
                if (float.TryParse(value, out var subVal))
                    return current + subVal; // subVal is already negative
            }
            
            if (value.StartsWith("multiply:"))
            {
                if (float.TryParse(value.Substring(9), out var mulVal))
                    return current * mulVal;
            }
            
            // Absolute value
            if (float.TryParse(value, out var absVal))
                return absVal;
            
            return current; // No change if can't parse
        }
        
        /// <summary>
        /// Apply all effects from an item to the character
        /// </summary>
        public bool ApplyItemEffects(uint itemId, Character character)
        {
            var registered = _itemRegistry.GetItemByNumericId(itemId);
            if (registered == null) return false;
            
            _log.LogInfo($"=== Applying effects for {registered.Definition.Id} ===");
            
            foreach (var effect in registered.Definition.Effects)
            {
                ApplyEffect(character, effect);
            }
            
            return true;
        }
        
        private void ApplyEffect(Character character, EffectDefinition effect)
        {
            var statKey = effect.Stat.ToLowerInvariant();
            
            if (_effectApplicators.TryGetValue(statKey, out var applicator))
            {
                try
                {
                    applicator(character, effect.Value);
                    _log.LogInfo($"  {effect.Stat} -> {effect.Value}");
                }
                catch (Exception ex)
                {
                    _log.LogError($"  Failed to apply {effect.Stat}: {ex.Message}");
                }
            }
            else
            {
                _log.LogWarning($"  Unknown stat: {effect.Stat}");
            }
        }
        
        /// <summary>
        /// Register a custom effect applicator
        /// </summary>
        public void RegisterEffect(string statName, Action<Character, string> applicator)
        {
            _effectApplicators[statName.ToLowerInvariant()] = applicator;
        }
    }
}
