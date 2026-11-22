#if DEBUG
using System.Diagnostics;
using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using HeroCameraName;
using UnityEngine;

namespace SteadyThirdPerson;

internal sealed class MovementTrace : IDisposable
{
    private const double MaxSeconds = 180;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly ManualLogSource log;
    private readonly StreamWriter writer;
    private readonly string path;
    private double nextFlush;
    private int samples;
    private bool disposed;

    internal ShotImpactTrace Shots { get; }

    internal MovementTrace(ManualLogSource log)
    {
        this.log = log;
        string directory = Path.Combine(Paths.BepInExRootPath, "diagnostics", "steady-third-person");
        Directory.CreateDirectory(directory);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        path = Path.Combine(directory, $"movement-{stamp}.csv");
        writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        try
        {
            writer.WriteLine("time_s,frame,view,shoulder_blend,shoulder_distance_pct,w,a,s,d,mouse_x,mouse_y,"
                + "move_x,move_z,hero_x,hero_y,hero_z,camera_x,camera_y,camera_z,camera_yaw,camera_pitch,camera_roll,"
                + "aim_yaw,aim_pitch,boom_yaw_delta");
            writer.Flush();
            Shots = new ShotImpactTrace(Path.Combine(directory, $"shots-{stamp}.csv"), clock, log);
        }
        catch
        {
            writer.Dispose();
            throw;
        }
        log.LogInfo($"Movement trace started: {path}");
    }

    internal bool Capture(GameCamera chase)
    {
        double elapsed = clock.Elapsed.TotalSeconds;
        if (elapsed >= MaxSeconds)
            return false;

        if (elapsed >= nextFlush)
        {
            writer.Flush();
            Shots.Flush();
            nextFlush = elapsed + 0.5;
        }

        if (!Application.isFocused || Cursor.visible || Time.timeScale <= 0f || !HeroCameraManager.InNormal())
            return true;
        var hero = CharacterMotionManager.HeroTran;
        var camera = CameraManager.CurCameraCom;
        if (!camera)
            camera = CameraManager.CurCameraMode == CameraMode.FPS ? CameraManager.MainCameraCom : CameraManager.TpsCameraCom;
        if (!hero || !camera)
            return true;

        var movement = CharacterMotionManager.HMData?.Direction ?? Vector3.zero;
        var heroPosition = hero.position;
        var view = camera.transform;
        var position = view.position;
        var angles = view.eulerAngles;
        var aim = ETCTransformCtrl.ControllerPitchETCCtrl;
        Vector3? aimAngles = aim ? aim.transform.eulerAngles : null;
        var settings = chase.TraceView;
        var boomYaw = chase.TraceBoomYaw?.ToString("F4", CultureInfo.InvariantCulture);
        string row = string.Create(CultureInfo.InvariantCulture,
            $"{elapsed:F4},{Time.frameCount},{settings.View},{settings.ShoulderBlend:F3},{settings.DistancePercent}," +
            $"{Key(KeyCode.W)},{Key(KeyCode.A)},{Key(KeyCode.S)},{Key(KeyCode.D)},{Input.GetAxisRaw("Mouse X"):F4},{Input.GetAxisRaw("Mouse Y"):F4}," +
            $"{movement.x:F4},{movement.z:F4},{heroPosition.x:F4},{heroPosition.y:F4},{heroPosition.z:F4}," +
            $"{position.x:F4},{position.y:F4},{position.z:F4},{angles.y:F4},{angles.x:F4},{angles.z:F4}," +
            $"{aimAngles?.y:F4},{aimAngles?.x:F4},{boomYaw}");
        writer.WriteLine(row);
        samples++;
        return true;
    }

    private static int Key(KeyCode key) => Input.GetKey(key) ? 1 : 0;

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        try
        {
            writer.Dispose();
            log.LogInfo($"Movement trace saved ({samples} frames): {path}");
        }
        finally
        {
            Shots.Dispose();
        }
    }
}
#endif
