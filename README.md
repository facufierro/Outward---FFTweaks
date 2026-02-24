# FFTweaks

Compatibility patch bundle for Outward DE that keeps your loadout stable by applying targeted fixes and override syncs for your installed mods.

## Included patches

- `FFT.TrueHardcore`: keeps TrueHardcore active while removing its combat loot animation injection.
- `FFT.Knives_Master`: learns the paired dagger/knife conversion recipe when equipping either item.
- `FFT.MoreDecraftingRecipes`: learns supported arrow decrafting recipes from equipped arrow types.
- `FFT.Classfixes_Part_1`: recipe unlock and classfix integration patch.
- `FFT.Classfixes_Part_2`: texture override persistence patch for Classfixes Part 2.
- `FFT.Beard_Additions`: copies bundled Beard Additions override files into the installed Beard Additions mod folder.
- `FFT.Configs`: copies bundled config override files into `BepInEx/config` once on first install (then leaves player-edited settings untouched), and adds manual `Override Configs` / `Override Configs Now` controls in Config Manager.

## Installation

1. Install BepInEx for Outward DE.
2. Install required dependencies.
3. Extract this package into `BepInEx/plugins`.
