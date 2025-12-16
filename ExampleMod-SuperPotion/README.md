# Super Potion Mod (Example)

An example mod for HoboModFramework demonstrating custom items, recipes, and quests.

## Installation

1. Copy this entire folder to:
   ```
   [Game]\BepInEx\plugins\HoboMods\SuperPotionMod\
   ```

2. Start the game!

## What This Mod Adds

### Super Potion (Item)
- **Effect:** Restores ALL stats to maximum
  - Health → Max
  - Energy → Max
  - Food → Max
  - Morale → Max
  - Warmth → Max
  - Dryness → Max
  - Odour → 0 (clean)

### Recipe
- **Crafting:** 2x Roll at any stove
- **Auto-unlocked:** Yes (appears immediately)

### Test Quest
- Simple quest to collect 1 Roll
- Used for testing quest system

## Files

```
SuperPotionMod/
├── mod.json             # Mod metadata
├── items/
│   └── super_potion.json
├── recipes/
│   └── super_potion.json
├── quests/
│   └── test_quest.json
├── localization/
│   └── en.json
└── assets/
    └── icons/
        └── super_potion.png
```

## Use As Template

Feel free to copy this mod and modify it to create your own!
