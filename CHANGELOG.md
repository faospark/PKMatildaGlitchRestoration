# Changelog

## [2.0.0] - 2026-11-01

### Added
- **In-Memory BepInEx 6 (IL2CPP) Implementation** (`Patches/MatildaGatePatch.cs`, `Plugin.cs`, `ModConfiguration.cs`):
  - Replaced the standalone `88be920ee2cf746260c35a530a4dd770.bundle` / `vi11.json` asset overwrite with a pure in-memory Harmony patch on `MACHICON.EventInit`, `MACHICON.OnMapInit`, `MapBGManagerHD.SetMap`, `MapBGManagerHD.RefleshObject`, and `MapSpriteHD.Awake`.
- **HD Gate Texture Synchronization (`t_vi11_00_obj_gate01_01`)**:
  - Dynamically binds the HD wooden border gate sprite (`t_vi11_00_obj_gate01_01`) in `vi11_00` to the live 2D gate entity (`eventobj[6]` / `ehuman[14]`) via `MapBGManagerHD.RefleshObject` delta tracking so the HD gate moves smoothly when pushed instead of remaining stationary.
- **Unified `EnableMatildaGateGlitch` Configuration (`ModConfiguration.cs`)**:
  - Supports `Push` (default; classic pushable gate with synced HD sprite), `Open` (completely disables 2D gate collision and hides HD gate textures `t_vi11_00_obj_gate01_01` and `t_vi11_00_obj_gate01_02`), and `False` (vanilla behavior).
