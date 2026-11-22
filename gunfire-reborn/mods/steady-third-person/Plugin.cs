using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using GunfireReborn.Mods.Core;
using UnityEngine;
using CorePlugin = GunfireReborn.Mods.Core.Plugin;

namespace SteadyThirdPerson;

[BepInPlugin(Id, "Steady Third Person", "0.2.0")]
[BepInProcess("Gunfire Reborn.exe")]
[BepInDependency(CorePlugin.Id)]
public sealed class Plugin : BasePlugin
{
    internal const string Id = "gunfire.steadythirdperson";

    public override void Load()
    {
        var settings = new ModsSettings(Config, "Steady Third Person", "Camera");
        var enabled = settings.Toggle("Enabled", "Third-person camera", "Switch to Close Shoulder during normal play.");
        var cameraHeight = settings.Slider("CameraHeight", "Camera height",
            "Raises the chase camera to move your character farther below the crosshair.", CameraFraming.DefaultHeight, 0f, 1.75f);
        var transition = settings.Slider("TransitionSeconds", "Transition time",
            "Sets how long the change between views takes, in seconds.", 0.35f, 0.05f, 1.5f);
        var levelHorizon = settings.Toggle("LevelHorizon", "Level horizon",
            "Reduces camera roll while keeping your aiming direction.", true);
        var rollReduction = settings.Slider("RollReduction", "Roll reduction",
            "Sets how much camera roll is removed when Level horizon is on.", 80, 0, 100);
        var shoulder = new CloseShoulderSettings(
            settings.Slider("ShoulderOffset", "Shoulder offset",
                "Moves the camera to the right so your character sits farther left in Close Shoulder.", 0.75f, 0f, 1.35f),
            settings.Slider("ShoulderDistance", "Close Shoulder distance",
                "Sets the follow distance as a percentage of the game's third-person camera distance.",
                CameraFraming.DefaultShoulderDistance, 35, 100),
            settings.Slider("ShoulderFieldOfView", "Close Shoulder FOV",
                "Adjusts how much of the scene you see in Close Shoulder.", 85, 60, 130));
        var toggleKey = Config.Bind("Camera", "ToggleKey", KeyCode.F8,
            "Toggle the third-person camera during normal play.");

        var camera = new GameCamera(Log, cameraHeight, levelHorizon, rollReduction, shoulder);
#if DEBUG
        var reloadPath = Config.Bind("Development", "ReloadAssembly", "",
            "Absolute path to the workspace Debug DLL. Keep the installed plugin and workspace DLL separate.");
        var peerPingSeconds = Config.Bind("Development", "PeerPingTraceSeconds", 180,
            new ConfigDescription("Maximum length of a peer ping trace, in seconds.",
                new AcceptableValueRange<int>(15, 600)));
        var peerPingKey = Config.Bind("Development", "PeerPingTraceKey", KeyCode.F7,
            "Key used to start or save a peer ping trace.");
        var framing = new DevelopmentFraming(Log, reloadPath);
        camera.ShoulderFrame = framing.PlaceShoulder;
        camera.Framing = framing.Evaluate;
        camera.RebaseAimRay = framing.RebaseAimRay;
        camera.ExtendPitchLimits = framing.ExtendPitchLimits;
#endif
        try
        {
            camera.Install();
#if DEBUG
            camera.InstallPeerPingTrace(peerPingSeconds, peerPingKey);
#endif
            AddComponent<CameraControls>().Initialize(camera, Log, enabled, toggleKey, transition);
            Log.LogInfo($"Close Shoulder ready. Press {toggleKey.Value} during a run or use Settings > Mods.");
        }
        catch (Exception error)
        {
            camera.Dispose();
            Log.LogWarning($"Close Shoulder unavailable: {error.GetBaseException().Message}");
        }
    }
}

internal sealed record CloseShoulderSettings(ConfigEntry<float> Offset,
    ConfigEntry<int> Distance, ConfigEntry<int> FieldOfView);
