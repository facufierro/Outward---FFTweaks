# FFTweaks

Compatibility and integration package for Outward DE.

`FFTweaks` bundles custom fixes used in my modpack into a single Thunderstore package.

## What it does

- Ships multiple plugin DLLs in one mod package:
	- `FFT.TrueHardcore.dll`: keeps TrueHardcore installed and active while disabling only its combat loot animation injection (`SpellCastAnim` calls in its loot interaction patch).
	- `FFT.Knives_Master.dll`: auto-learns Knives Master dagger recipes when a character loads.

## Included plugins

- `FFT.TrueHardcore.dll`
- `FFT.Knives_Master.dll`
- `FFT.Classfixes_Part_1.dll`

## Dependencies

- `BepInEx-BepInExPack_Outward`
- `IggyTheMad-TrueHardcore`
- `stormcancer-Knives_Master`
- `Vheos-VheosModPack`

## Notes

- No direct edits to `HardcoreRebalance.dll` are required.
