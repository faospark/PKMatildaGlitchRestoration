using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using PKMatildaGlitchRestoration.Patches;

namespace PKMatildaGlitchRestoration;

[BepInPlugin("faospark.pkmatildaglitchrestoration", "PK Matilda Glitch Restoration", "2.0.0")]
[BepInDependency("faospark.pkcore", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("d3xMachina.suikoden_fix", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public static Plugin Instance { get; private set; }
    public static new ManualLogSource Log;
    public static new ModConfiguration Config;

    public override void Load()
    {
        Instance = this;
        Log = base.Log;

        Log.LogInfo("Loading PKMatildaGlitchRestoration...");

        Config = new ModConfiguration(base.Config);
        Config.Init();

        Harmony harmony = new Harmony("faospark.pkmatildaglitchrestoration");

        if (Config.IsPushMode() || Config.IsOpenMode())
        {
            Log.LogInfo($"Registering Matilda Gate Glitch Restoration patch (Mode: {Config.EnableMatildaGateGlitch.Value})...");
            MatildaGatePatch.Initialize(harmony);
        }

        Log.LogInfo("PKMatildaGlitchRestoration loaded successfully!");
    }
}
