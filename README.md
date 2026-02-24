# FFTweaks

This is a carefully assembled modpack built on top of the Vheos Modpack and several other dependencies. It represents many hours of selection, testing, and tuning to arrive at a specific experience: a harder, more unforgiving Outward that still feels fair, is co-op ready, and doesn't drown you in UI clutter.

The config overrides are the heart of this pack. They aren't just defaults — they are deliberate choices that define how the game plays.

---

## Installation

1. Install via r2modman. All dependencies are handled automatically.
2. Launch the game once to let first-install config overrides apply.
3. Start a **new save**.

---

## The experience

### Combat and difficulty (Vheos Modpack)

Enemies are smarter and more aggressive out of the box — detection range is tuned up, they re-target when you break line-of-sight, and they hit harder. You also hit them for less. This is intentional: Classfixes significantly increases player power, so the damage output is pulled back to keep fights from being trivialized. Dodge costs more stamina, and breaking enemy stability requires consistent pressure at specific breakpoints rather than just spamming attacks.

**You cannot loot during combat.** This is enforced by Vheos's disallowed-in-combat flag, which is a cleaner solution than TrueHardcore's animation patch (see below).

### Crafting knowledge (Vheos Modpack)

Vheos's limited manual crafting is enabled. This means you cannot discover recipes by throwing ingredients together — you need actual recipe knowledge. Some of the mods in this pack add recipes that have no in-game scrolls or teachers, so FFTweaks auto-teaches those recipes the moment you pick up or equip the relevant item:

- **Classfixes Part 1** — pistol ↔ handgun conversion recipes
- **Knives Master** — knife ↔ dagger conversion recipes
- **More Decrafting Recipes** — arrow decrafting recipes

### Enemies are information-dark (Combat HUD)

Your own vitals and status timers show as normal. Enemy health, damage numbers, status effects, and info boxes are hidden. You learn their behavior by fighting them, not by reading their stats.

Same goes for the map — enemies and merchant caravans are not shown. You navigate and explore on your own terms.

### TrueHardcore — trimmed down

TrueHardcore is included for its enemy AI improvements and permadeath system. The harsher extras — per-city stashes, night gate locks, enemy healing from player damage — are all turned off. TrueHardcore's loot animation patch is also removed, since Vheos already handles no-looting-in-combat more reliably.

### Co-op (Raid Mode)

Up to 5 players with balanced difficulty scaling that doesn't spike unfairly like vanilla does above 2 players. Story and side-quest skills are shared with the whole party. Revival health burn is reduced slightly. Enemies only stagger at stability breakpoints, so you can't chain-stun-lock them with a full group.

### Transmorphic

Transmogrify is available. The enchanting menu is not — it uses different ingredients than the defaults, tuned to fit the rest of the pack.

### Runic weapons

Runic weapon models are replaced with iron weapon models. This is a personal aesthetic preference.

---

## Config overrides

On first install, FFTweaks writes a tuned config baseline for all participating mods. This covers the settings above plus HUD layout, description detail level, durability behavior, camping rules, and a few smaller things.

If you want to reset to the baseline after changing things yourself:

- Open the in-game config manager with **F9** (changed from the default F5).
- Find **FFTweaks** and use the re-apply or refresh options.
