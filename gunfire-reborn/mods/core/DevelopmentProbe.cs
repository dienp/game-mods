#if DEBUG
using System.Reflection;
using System.Runtime.Loader;
using BepInEx.Logging;

namespace GunfireReborn.Mods.Core;

internal sealed class DevelopmentProbe
{
    private readonly ManualLogSource log;
    private readonly string? debugAssembly;
    private Action<ManualLogSource, bool, int> reporter = ProbeReporter.Report;
    private AssemblyLoadContext? activeContext;
    private (DateTime Modified, long Length)? loadedRevision;

    internal DevelopmentProbe(ManualLogSource log)
    {
        this.log = log;
        debugAssembly = Environment.GetEnvironmentVariable("GUNFIRE_MODS_CORE_DEBUG_DLL");
    }

    internal void Report(bool enabled, int intensity)
    {
        ReloadIfChanged();
        reporter(log, enabled, intensity);
    }

    private void ReloadIfChanged()
    {
        if (string.IsNullOrWhiteSpace(debugAssembly))
            return;

        AssemblyLoadContext? context = null;
        try
        {
            var file = new FileInfo(debugAssembly);
            if (!file.Exists)
                return;

            var revision = (file.LastWriteTimeUtc, file.Length);
            if (revision == loadedRevision)
                return;

            context = new AssemblyLoadContext("Mods Core probe", isCollectible: true);
            using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var assembly = context.LoadFromStream(stream);
            var type = assembly.GetType(typeof(ProbeReporter).FullName!, throwOnError: true)!;
            var method = type.GetMethod(nameof(ProbeReporter.Report), BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(type.FullName, nameof(ProbeReporter.Report));
            var next = method.CreateDelegate<Action<ManualLogSource, bool, int>>();

            var previous = activeContext;
            reporter = next;
            activeContext = context;
            loadedRevision = revision;
            previous?.Unload();
            log.LogInfo("Development probe reloaded.");
        }
        catch (Exception error)
        {
            log.LogWarning($"Could not reload the development probe: {error.GetBaseException().Message}");
        }
        finally
        {
            if (context != activeContext)
                context?.Unload();
        }
    }
}
#endif
