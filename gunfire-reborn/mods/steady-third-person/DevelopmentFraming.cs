#if DEBUG
using System.Reflection;
using System.Runtime.Loader;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace SteadyThirdPerson;

internal sealed class DevelopmentFraming
{
    private readonly ManualLogSource log;
    private readonly ConfigEntry<string> assemblyPath;
    private Func<Vector3, Vector3> centerOffset = CameraFraming.CenterOffset;
    private Func<Vector3, float, float, int, (Vector3 Socket, float ArmScale)> shoulderFrame = CameraFraming.PlaceShoulder;
    private GameCamera.Presentation framing = CameraFraming.Evaluate;
    private Func<Ray, Transform, Transform, Ray> aimRay = CameraFraming.RebaseAimRay;
    private AssemblyLoadContext? context;
    private (string Path, DateTime Modified, long Length)? loaded;
    private float nextCheck;

    internal DevelopmentFraming(ManualLogSource log, ConfigEntry<string> assemblyPath)
    {
        this.log = log;
        this.assemblyPath = assemblyPath;
    }

    internal Vector3 CenterOffset(Vector3 offset)
    {
        CheckForReload();
        return centerOffset(offset);
    }

    internal (Vector3 Socket, float ArmScale) PlaceShoulder(Vector3 socket, float weight, float offset, int distancePercent)
    {
        CheckForReload();
        return shoulderFrame(socket, weight, offset, distancePercent);
    }

    internal (Quaternion Rotation, float Fov) Evaluate(Quaternion rotation, float nativeFov,
        float referenceFov, float horizontalFov, float aspect, float rollReduction)
    {
        CheckForReload();
        return framing(rotation, nativeFov, referenceFov, horizontalFov, aspect, rollReduction);
    }

    internal Ray RebaseAimRay(Ray ray, Transform source, Transform shoulder)
    {
        CheckForReload();
        return aimRay(ray, source, shoulder);
    }

    private void CheckForReload()
    {
        float now = Time.unscaledTime;
        if (now < nextCheck)
            return;
        nextCheck = now + 0.5f;
        Reload();
    }

    private void Reload()
    {
        if (string.IsNullOrWhiteSpace(assemblyPath.Value))
            return;

        AssemblyLoadContext? candidate = null;
        try
        {
            var file = new FileInfo(assemblyPath.Value);
            if (!file.Exists)
                return;
            var revision = (file.FullName, file.LastWriteTimeUtc, file.Length);
            if (revision == loaded)
                return;

            candidate = new AssemblyLoadContext("Centered Chase framing", isCollectible: true);
            using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var assembly = candidate.LoadFromStream(stream);
            var type = assembly.GetType(typeof(CameraFraming).FullName!, throwOnError: true)!;
            var nextOffset = LoadMethod<Func<Vector3, Vector3>>(type, nameof(CameraFraming.CenterOffset));
            var nextShoulder = LoadMethod<Func<Vector3, float, float, int, (Vector3, float)>>(type, nameof(CameraFraming.PlaceShoulder));
            var nextFraming = LoadMethod<GameCamera.Presentation>(type, nameof(CameraFraming.Evaluate));
            var nextAimRay = LoadMethod<Func<Ray, Transform, Transform, Ray>>(type, nameof(CameraFraming.RebaseAimRay));

            var previous = context;
            centerOffset = nextOffset;
            shoulderFrame = nextShoulder;
            framing = nextFraming;
            aimRay = nextAimRay;
            context = candidate;
            loaded = revision;
            previous?.Unload();
            log.LogInfo("Centered Chase framing reloaded.");
        }
        catch (Exception error)
        {
            log.LogWarning($"Could not reload Centered Chase framing: {error.GetBaseException().Message}");
        }
        finally
        {
            if (candidate != context)
                candidate?.Unload();
        }
    }

    private static T LoadMethod<T>(Type type, string name) where T : Delegate =>
        (type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, name)).CreateDelegate<T>();
}
#endif
