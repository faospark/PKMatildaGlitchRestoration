# PKMatildaGlitchRestoration

A BepInEx 6 (IL2CPP) mod for **Suikoden I & II HD Remaster** that restores the iconic PS1 **Matilda Gate Push Glitch** on the Muse–Matilda border (`vi11`) directly in memory—with full **HD Gate Sprite Synchronization** (`t_vi11_00_obj_gate01_01`) so the wooden border gate visual moves smoothly as you push it, plus an optional **Open Border** mode to completely disable the gate and its textures.

## Features

- **[Can Be Toggled] Classic Matilda Gate Push Glitch (`Push` Mode — Default)**:
  - Restores the classic PS1 pushable border gate on the Muse–Matilda border (`vi11`, Room 0) by patching `eventobj[6].wt = 1` and live `ehuman[14].wt = 1` in `MACHICON.EventInit`.
  - **HD Gate Sprite Sync**: Hooks `MapBGManagerHD.RefleshObject` and `MapBGManagerHD.SetMap` to attach the HD wooden gate sprite (`t_vi11_00_obj_gate01_01`) to the live 2D gate object (`eventobj[6]`), ensuring the HD gate slides smoothly out of the way as you push it instead of leaving a stationary visual ghost that you warp through.
- **[Can Be Toggled] Open Border Mode (`Open` Mode)**:
  - Completely disables the 2D border gate collision (`eventobj[6]` and `eventobj[7]`, `ehuman[14]` and `ehuman[15]`, and room auto-init opcodes) and replaces the HD gate textures (`t_vi11_00_obj_gate01_01` / `t_vi11_00_obj_gate01_02`) with a 1×1 transparent sprite so you can walk freely across the Muse–Matilda border without pushing the gate.
- **[Baked In] Zero `.bundle` Replacements**:
  - Replaces the old `88be920ee2cf746260c35a530a4dd770.bundle` / `vi11.json` file overwrite with a lightweight, update-proof in-memory Harmony patch (`PKMatildaGlitchRestoration.dll`).

## Configuration

Configuration is generated at `BepInEx/config/faospark.pkmatildaglitchrestoration.cfg` after the first launch:

```ini
[Suikoden 2 Matilda Gate Glitch]

## Controls the Muse-Matilda border gate (vi11) behavior:
## - Push (or true / classic): Restores the classic PS1 Matilda Gate push glitch and synchronizes the HD gate sprite (t_vi11_00_obj_gate01_01) so it moves smoothly with the pushed gate.
## - Open: Completely disables the border gate collision and hides its HD gate textures (t_vi11_00_obj_gate01_01 / t_vi11_00_obj_gate01_02) so the border is wide open.
## - False: Vanilla behavior (gate cannot be pushed).
# Setting type: String
# Default value: Push
# Acceptable values: Push, Open, False, true, classic, false
EnableMatildaGateGlitch = Push
```
