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

- Dependencies are synchronized from the active profile plugin folder before build.
- Default source: `c:\Users\fierr\AppData\Roaming\r2modmanPlus-local\OutwardDe\profiles\Classfixes\BepInEx\plugins`
- To sync without building:

```powershell
./build_mod.ps1 -Action sync-deps
```

- To build without syncing (optional):

```powershell
./build_mod.ps1 -Action build -SkipDependencySync
```

## Notes

- No direct edits to `HardcoreRebalance.dll` are required.
