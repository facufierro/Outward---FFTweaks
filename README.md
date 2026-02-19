# FFTweaks

Compatibility bundle for Outward DE.

## What it does

- Ships two plugin DLLs in one mod package:
	- `FFT.TrueHardcore.dll`: keeps TrueHardcore installed and active while disabling only its combat loot animation injection (`SpellCastAnim` calls in its loot interaction patch).
	- `FFT.KnivesMaster.dll`: auto-learns Knives Master dagger recipes when a character loads.

## Notes

- Both fixes are implemented as separate BepInEx + Harmony plugins distributed together as one `FFTweaks` release.
- No edits to `HardcoreRebalance.dll` are required.
