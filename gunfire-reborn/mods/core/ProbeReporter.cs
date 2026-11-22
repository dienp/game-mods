using BepInEx.Logging;

namespace GunfireReborn.Mods.Core;

internal static class ProbeReporter
{
    internal static void Report(ManualLogSource log, bool enabled, int intensity)
    {
        var message = $"Settings probe {(enabled ? "on" : "off")}; intensity {intensity}%.";
        log.LogInfo(message);
    }
}
