using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace GunfireReborn.Mods.Core;

[BepInPlugin(Id, "Mods Core", "0.1.0")]
[BepInProcess("Gunfire Reborn.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "gunfire.mods.core";

    public override void Load()
    {
        Log.LogInfo("Loader check passed: Mods Core started.");
        NativeModsMenu.Active = new NativeModsMenu(Log);

        var settings = new ModsSettings(Config, "Mods Core");
        var enabled = settings.Toggle("ProbeEnabled", "Settings probe",
            "Write a test message to the BepInEx log when this control changes.");
        var intensity = settings.Slider("ProbeIntensity", "Probe intensity",
            "Reports this value in the log while the settings probe is on.", 50, 0, 100);

        Action<bool, int> report = (isEnabled, value) => ProbeReporter.Report(Log, isEnabled, value);
#if DEBUG
        var reload = new DevelopmentProbe(Log);
        report = reload.Report;
#endif
        enabled.SettingChanged += (_, _) => report(enabled.Value, intensity.Value);
        intensity.SettingChanged += (_, _) =>
        {
            if (enabled.Value)
                report(true, intensity.Value);
        };
        NativeModsMenu.Active.Install();
    }
}
