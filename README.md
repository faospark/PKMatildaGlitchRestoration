# P.K. - Matilda Glitch Restoration

**By [faospark](https://github.com/faospark)**

A **BepInEx 6 (IL2CPP)** mod for **Suikoden I & II HD Remaster** that restores the classic PS1 **Matilda Gate Push Glitch** on the Muse–Matilda border (`vi11`) in memory—complete with **HD Gate Sprite Sync** and an optional **Open Border** mode.

## Features

- **Classic Matilda Gate Push (`Push` — Default)**: Restores the pushable Muse–Matilda border gate (`vi11`) and synchronizes the HD wooden gate sprite so it slides smoothly as you push it instead of leaving a static visual ghost.
- **Open Border Mode (`Open`)**: Disables the border gate collision and hides the HD gate textures so you can walk freely across the Muse–Matilda border without pushing.
- **In-Memory Harmony Patching**: Applies cleanly in memory with zero `.bundle` or `vi11.json` file replacements.

## Configuration (`BepInEx/config/faospark.pkmatildaglitchrestoration.cfg`)

```ini
[Suikoden 2 Matilda Gate Glitch]
EnableMatildaGateGlitch = Push  # Push (or true/classic): pushable gate | Open: gate removed | False: vanilla
```

## Requirements

- **BepInEx 6.0.0-pre.2 IL2CPP**
- **Suikoden I & II HD Remaster**
- **Recommended:** [Suikoden Fix](https://github.com/d3xMachina/SuikodenFix) by d3xMachina

## Installation

1. Install [BepInEx 6.0 (Unity IL2CPP x64)](https://github.com/BepInEx/BepInEx/releases/download/v6.0.0-pre.2/BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip) into your game root directory.
2. Place `PKMatildaGlitchRestoration.dll` into `BepInEx/plugins/`.
3. Launch the game once to generate `BepInEx/config/faospark.pkmatildaglitchrestoration.cfg`.

## Credits & License

- **[faospark](https://github.com/faospark)** — Primary Author & Lead Researcher
- **[d3xMachina](https://github.com/d3xMachina/Suikoden-Fix)** — Creator of Suikoden-Fix & BepInEx IL2CPP foundation
- **[asilverthorn](https://github.com/asilverthorn/suikoden_ref)** — Reference tables, internal IDs, and save structure research
- **Moogles & Mods Discord** — Community support & research ([Invite](https://discord.com/invite/bSnpVBV))
- **Framework:** [BepInEx](https://github.com/BepInEx/BepInEx) & [HarmonyX](https://github.com/BepInEx/HarmonyX)

Distributed under the [MIT License](LICENSE).
