using System;
using BepInEx.Configuration;

namespace PKMatildaGlitchRestoration;

public sealed class ModConfiguration
{
    private readonly ConfigFile _config;

    public ConfigEntry<string> EnableMatildaGateGlitch { get; private set; }

    public ModConfiguration(ConfigFile config)
    {
        _config = config;
    }

    public void Init()
    {
        EnableMatildaGateGlitch = _config.Bind(
            "Suikoden 2 Matilda Gate Glitch",
            "EnableMatildaGateGlitch",
            "Push",
            new ConfigDescription(
                "Controls the Muse-Matilda border gate (vi11) behavior:\n" +
                "- Push (or true / classic): Restores the classic PS1 Matilda Gate push glitch and synchronizes the HD gate sprite (t_vi11_00_obj_gate01_01) so it moves smoothly with the pushed gate.\n" +
                "- Open: Completely disables the border gate collision and hides its HD gate textures (t_vi11_00_obj_gate01_01 / t_vi11_00_obj_gate01_02) so the border is wide open.\n" +
                "- False: Vanilla behavior (gate cannot be pushed).",
                new AcceptableValueList<string>("Push", "Open", "False", "true", "classic", "false")
            )
        );
    }

    public bool IsPushMode()
    {
        string val = EnableMatildaGateGlitch?.Value?.Trim();
        if (string.IsNullOrEmpty(val)) return false;

        return val.Equals("Push", StringComparison.OrdinalIgnoreCase) ||
               val.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               val.Equals("classic", StringComparison.OrdinalIgnoreCase) ||
               val.Equals("classic mode", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsOpenMode()
    {
        string val = EnableMatildaGateGlitch?.Value?.Trim();
        if (string.IsNullOrEmpty(val)) return false;

        return val.Equals("Open", StringComparison.OrdinalIgnoreCase);
    }
}
