# FFTweaks

Mod orchestrator for my Outward DE modpack.

This repository is the central package for the modpack and contains its custom plugin modifications and configuration assets.

## What it does

- Ships multiple plugin DLLs in one mod package:
	- `FFT.TrueHardcore.dll`: keeps TrueHardcore installed and active while disabling only its combat loot animation injection (`SpellCastAnim` calls in its loot interaction patch).
	- `FFT.KnivesMaster.dll`: auto-learns Knives Master dagger recipes when a character loads.

## Notes

- Both fixes are implemented as separate BepInEx + Harmony plugins distributed together as one `FFTweaks` release.
- No edits to `HardcoreRebalance.dll` are required.

## Release workflow

- `CHANGELOG.md` is updated only when a release version is ready.
