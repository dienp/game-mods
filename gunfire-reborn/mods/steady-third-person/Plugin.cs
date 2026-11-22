using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace SteadyThirdPerson;

[BepInPlugin(Id, "Steady Third Person", "0.1.0")]
[BepInProcess("Gunfire Reborn.exe")]
public sealed class Plugin : BasePlugin
{
    internal const string Id = "gunfire.steadythirdperson";

    public override void Load()
    {
        var toggleKey = Config.Bind("Camera", "ToggleKey", KeyCode.F8,
            "Toggle third person during normal play.");
        var transition = Config.Bind("Camera", "TransitionSeconds", 0.35f,
            new ConfigDescription("Duration of the view change, in seconds.",
                new AcceptableValueRange<float>(0.05f, 1.5f)));
        var levelHorizon = Config.Bind("Camera", "LevelHorizon", true,
            "Keep the third-person camera level while preserving pitch and yaw.");

        try
        {
            var camera = new GameCamera();
            AddComponent<CameraControls>().Initialize(camera, Log, toggleKey, transition, levelHorizon);
            Log.LogInfo($"Ready. Press {toggleKey.Value} during a run to switch camera views.");
        }
        catch (Exception error)
        {
            Log.LogWarning($"Camera controls unavailable: {error.GetBaseException().Message}");
        }
    }
}
