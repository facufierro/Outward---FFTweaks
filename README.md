# FFTweaks

Small compatibility patch for Outward DE.

## What it does

- Keeps TrueHardcore installed and active.
- Disables only TrueHardcore's combat loot animation injection (`SpellCastAnim` calls in its loot interaction patch).
- Lets another mod handle loot-in-combat behavior.

## Notes

- This is implemented as a separate BepInEx + Harmony plugin.
- No edits to `HardcoreRebalance.dll` are required.
