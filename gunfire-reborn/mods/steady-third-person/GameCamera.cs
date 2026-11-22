using BepInEx.Configuration;
using BepInEx.Logging;
using Cinemachine;
using Game;
using HarmonyLib;
using HeroCameraName;
using HeroMoveState;
using UnityEngine;

namespace SteadyThirdPerson;

internal sealed class GameCamera : IDisposable
{
    private static GameCamera? active;
    private readonly Harmony harmony = new(Plugin.Id);
    private readonly ManualLogSource log;
    private readonly ConfigEntry<float> cameraHeight;
    private readonly ConfigEntry<bool> levelHorizon;
    private readonly ConfigEntry<int> rollReduction;
    private readonly CloseShoulderSettings shoulder;
    private readonly ChaseAvatar avatar;

    private NewPlayerObject? owner;
    private Camera? presentedCamera;
    private Quaternion nativeRotation;
    private Quaternion appliedRotation;
    private float nativeFov;
    private float appliedFov;
    private float referenceFov;
    private bool presentationFailed;
    private bool reportedAimFailure;
    private bool reportedPitchFailure;
#if DEBUG
    private bool reportedLeftBlend;
    private bool reportedBackwardBlend;
    private bool reportedPitchLimits;
    private int traceBoomFrame = -1;
    private float? traceBoomStartYaw;
    private float? traceBoomDelta;

    internal MovementTrace? Trace { get; set; }
    private bool Tracing => Trace is not null;
    internal float? TraceBoomYaw => traceBoomFrame == Time.frameCount ? traceBoomDelta : null;
    internal (string View, float ShoulderBlend, int DistancePercent) TraceView => CameraManager.CurCameraMode == CameraMode.FPS
        ? ("first_person", 0f, 100)
        : !UsesChaseCamera
            ? ("native_third_person", 0f, 100)
            : ("close_shoulder", 1f, shoulder.Distance.Value);
#endif

    internal delegate (Quaternion Rotation, float Fov) Presentation(Quaternion rotation, float nativeFov,
        float referenceFov, float horizontalFov, float aspect, float rollReduction);

    internal Presentation Framing { get; set; } = CameraFraming.Evaluate;
    internal Func<Ray, Transform, Transform, Ray> RebaseAimRay { get; set; } = CameraFraming.RebaseAimRay;
    internal Func<float, float, float, (float Up, float Down)> ExtendPitchLimits { get; set; } =
        CameraFraming.ExtendPitchLimits;
    internal Func<Vector3, float, int, (Vector3 Socket, float ArmScale)> ShoulderFrame { get; set; } =
        CameraFraming.PlaceShoulder;

    internal GameCamera(ManualLogSource log, ConfigEntry<float> cameraHeight,
        ConfigEntry<bool> levelHorizon, ConfigEntry<int> rollReduction, CloseShoulderSettings shoulder)
    {
        this.log = log;
        this.cameraHeight = cameraHeight;
        this.levelHorizon = levelHorizon;
        this.rollReduction = rollReduction;
        this.shoulder = shoulder;
        avatar = new ChaseAvatar(log);
    }

    internal void Install()
    {
        var boom = AccessTools.Method(typeof(CameraBoom), nameof(CameraBoom.LateUpdate));
        var brain = AccessTools.Method(typeof(CinemachineBrain), nameof(CinemachineBrain.LateUpdate));
        var rotation = AccessTools.Method(typeof(ETCTransformCtrl), nameof(ETCTransformCtrl.CheckRotateUpdate));
        var angleLimits = AccessTools.Method(typeof(ETCTransformCtrl), nameof(ETCTransformCtrl.DoAngleLimitation));
        var locomotion = AccessTools.Method(typeof(HeroMoveStateBase), nameof(HeroMoveStateBase.SetTpsAni));
        harmony.Patch(boom, prefix: Hook(nameof(FrameBoom)), postfix: Hook(nameof(AfterBoom)));
        harmony.Patch(brain, postfix: Hook(nameof(AfterBrain)));
        harmony.Patch(rotation, prefix: Hook(nameof(FaceAim)), postfix: Hook(nameof(RestoreRotationSettings)));
        harmony.Patch(angleLimits, prefix: Hook(nameof(WidenPitchLimits)), finalizer: Hook(nameof(RestorePitchLimits)));
        harmony.Patch(locomotion, postfix: Hook(nameof(KeepStrafeBlendSigned)));
        ShoulderAim.Install(harmony);
        active = this;

        static HarmonyMethod Hook(string method) => new(typeof(GameCamera), method);
    }

    internal bool CanAcceptInput => NormalCamera(CharacterMotionManager.HeroObj) is not null && HeroCameraManager.canOP();

    private bool UsesChaseCamera => owner is not null && CameraManager.CurCameraMode == CameraMode.TPS
        && CameraManager.CurTpsMode == TpsMode.FREE;

    internal static GameCamera? Active => active;

#if DEBUG
    internal void InstallPeerPingTrace(ConfigEntry<int> duration, ConfigEntry<KeyCode> toggleKey) =>
        PeerPingTrace.Install(harmony, log, duration, toggleKey);
#endif

    internal Camera? ShoulderAimCamera => !presentationFailed && UsesChaseCamera
        && owner is { } hero && CharacterMotionManager.HeroObj?.Pointer == hero.Pointer
        && presentedCamera && NormalCamera(hero) is not null ? presentedCamera : null;

    internal void UpdateRequest(bool enabled, bool gameplayInput, float seconds)
    {
        var hero = CharacterMotionManager.HeroObj;
        if (owner is not null && hero?.Pointer != owner.Pointer)
        {
            owner = null;
            RestorePresentation();
        }

        var normal = NormalCamera(hero);
        avatar.Update(enabled && UsesChaseCamera && normal is not null ? hero : null);
        if (normal is null || !gameplayInput)
            return;

        if (!enabled)
        {
            Release(normal, seconds);
            return;
        }

        if (normal.isChanging || CameraManager.CurCameraMode != CameraMode.FPS || normal.tarCamMode != CameraMode.FPS)
            return;

        if (owner is null)
        {
            hero!.ChangeTpsState(true, Plugin.Id);
            owner = hero;
        }

        HeroCameraManager.ChangeToTpsMode(TpsMode.FREE, seconds);
        log.LogInfo("Close Shoulder entered.");
    }

    private static NormalCS? NormalCamera(NewPlayerObject? hero)
    {
        if (hero is null || hero.FakerDeadCom?.IsFakerDead == true || !HeroCameraManager.InNormal())
            return null;
        return HeroCameraManager.CSObject?.TryCast<NormalCS>();
    }

    private void Release(NormalCS normal, float seconds)
    {
        if (owner is null)
            return;
        RestorePresentation();
        var hero = owner;
        hero.ChangeTpsState(false, Plugin.Id);
        owner = null;
        if (!hero.IsMoreTpsState() && (CameraManager.CurCameraMode == CameraMode.TPS || normal.tarCamMode == CameraMode.TPS))
            normal.ChangeToFpsMode(seconds);
        log.LogInfo("Close Shoulder released.");
    }

    private bool OwnsBoom(CameraBoom boom)
    {
        return owner is not null && !presentationFailed
            && CharacterMotionManager.HeroObj?.Pointer == owner.Pointer
            && NormalCamera(owner)?.tpsCamBoom?.Pointer == boom.Pointer
            && CameraManager.CurTpsMode == TpsMode.FREE;
    }

    private static void FaceAim(ETCTransformCtrl __instance, out (bool Applied, bool Orient, bool Yaw) __state)
    {
        __state = default;
        if (active is not { UsesChaseCamera: true, owner: { } hero }
            || ETCTransformCtrl.CharacterETCCtrl != __instance
            || CharacterMotionManager.HeroObj?.Pointer != hero.Pointer
            || NormalCamera(hero) is null)
            return;

        __state = (true, __instance.OrientRotationToMovement, __instance.UseControllerRotationYaw);
        __instance.OrientRotationToMovement = false;
        __instance.UseControllerRotationYaw = true;
    }

    private static void RestoreRotationSettings(ETCTransformCtrl __instance, (bool Applied, bool Orient, bool Yaw) __state)
    {
        if (__state.Applied)
        {
            __instance.OrientRotationToMovement = __state.Orient;
            __instance.UseControllerRotationYaw = __state.Yaw;
        }
    }

    private static void WidenPitchLimits(ETCTransformCtrl __instance, out PitchBounds __state)
    {
        __state = default;
        if (active is not { UsesChaseCamera: true, presentationFailed: false, owner: { } hero } camera)
            return;
        try
        {
            if (__instance.Pointer != ETCTransformCtrl.ControllerETCCtrl?.Pointer
                || CharacterMotionManager.HeroObj?.Pointer != hero.Pointer
                || NormalCamera(hero) is not { isChanging: false }
                || !__instance.GetAngleLimitX(out float up, out float down))
                return;

            float upWithRecoil = __instance.FixLimit();
            var (wideUp, wideDown) = camera.ExtendPitchLimits(up, down, upWithRecoil);
            if (!float.IsFinite(wideUp) || !float.IsFinite(wideDown) || (wideUp == up && wideDown == down))
                return;
            __state = new PitchBounds(true, up, down);
            __instance.m_minAngleX = wideUp;
            __instance.m_maxAngleX = wideDown;
#if DEBUG
            if (!camera.reportedPitchLimits)
            {
                camera.reportedPitchLimits = true;
                camera.log.LogInfo($"Widened third-person pitch from {up:0.#}° up / {down:0.#}° down to "
                    + $"{wideUp + (upWithRecoil - up):0.#}° up / {wideDown:0.#}° down.");
            }
#endif
        }
        catch (Exception error)
        {
            if (!camera.reportedPitchFailure)
            {
                camera.reportedPitchFailure = true;
                camera.log.LogWarning($"Could not widen third-person pitch: {error.GetBaseException().Message}");
            }
        }
    }

    private static void RestorePitchLimits(ETCTransformCtrl __instance, PitchBounds __state)
    {
        if (__state.Changed)
        {
            __instance.m_minAngleX = __state.Up;
            __instance.m_maxAngleX = __state.Down;
        }
    }

    private readonly record struct PitchBounds(bool Changed, float Up, float Down);

    private static void KeepStrafeBlendSigned(float speedRate)
    {
        if (speedRate <= 0f || active is not { UsesChaseCamera: true, owner: { } hero } camera
            || CharacterMotionManager.HeroObj?.Pointer != hero.Pointer || NormalCamera(hero) is null)
            return;

        var motion = CharacterMotionManager.HMData;
        var root = hero.gameTrans;
        var animator = hero.PlayerCom?.TPSAnimator;
        if (motion is null || !root || !animator)
            return;

        var direction = motion.Direction;
        var forward = root.forward;
        direction.y = 0f;
        forward.y = 0f;
        direction.Normalize();
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward);
        float x = Vector3.Dot(direction, right);
        float y = Vector3.Dot(direction, forward);

        // The native local animator clamps negative components to zero. The spectator
        // animator uses the signed components, so restore only the discarded directions.
        const float directionThreshold = -0.001f;
        if (x < directionThreshold)
        {
            AnimatorInterface.SetFloatWithAnimator(animator!, AnimatorEvent.MoveDirX, Mathf.Max(-1f, x * speedRate));
#if DEBUG
            if (!camera.reportedLeftBlend)
            {
                camera.reportedLeftBlend = true;
                camera.log.LogInfo("Close Shoulder applied the signed left animation blend.");
            }
#endif
        }
        if (y < directionThreshold)
        {
            AnimatorInterface.SetFloatWithAnimator(animator!, AnimatorEvent.MoveDirY, Mathf.Max(-1f, y * speedRate));
#if DEBUG
            if (!camera.reportedBackwardBlend)
            {
                camera.reportedBackwardBlend = true;
                camera.log.LogInfo("Close Shoulder applied the signed backward animation blend.");
            }
#endif
        }
    }

    private static void FrameBoom(CameraBoom __instance, out BoomSettings __state)
    {
        __state = default;
        if (active is not { } camera)
            return;
        try
        {
            if (!camera.OwnsBoom(__instance))
                return;
            var config = __instance.CurrentCameraBoomConfig;
            if (config is null || config.TpsMode != TpsMode.FREE)
                return;
#if DEBUG
            if (camera.Tracing)
            {
                camera.traceBoomFrame = Time.frameCount;
                camera.traceBoomDelta = null;
                camera.traceBoomStartYaw = HeroCtrlMgr.Controller?.parent?.localEulerAngles.y;
            }
#endif
            var offset = config.SocketOffset;
            var pivot = __instance.DefaultOffset;
            float armLength = config.TargetArmLength;
            __state = new BoomSettings(config, offset, pivot, armLength, config.FocusSpeed);
            var (framing, armScale) = camera.ShoulderFrame(offset, camera.shoulder.Offset.Value,
                camera.shoulder.Distance.Value);
            float height = framing.y - offset.y + camera.cameraHeight.Value - CameraFraming.DefaultHeight;
            // Virtual cameras follow the boom pivot. The regular boom uses its socket directly.
            if (__instance.BrainEnabled)
            {
                __instance.DefaultOffset = new Vector3(pivot.x, pivot.y + height, pivot.z);
                framing.y = offset.y;
            }
            else
            {
                framing.y = offset.y + height;
            }
            config.SocketOffset = framing;
            // The native boom still computes the collision-constrained camera position.
            config.TargetArmLength = armLength * armScale;
            // Free TPS normally turns the view toward the follow target when movement opens a gap.
            config.FocusSpeed = 0f;
        }
        catch (Exception error)
        {
            camera.StopPresentation(error);
        }
    }

    private static void AfterBoom(CameraBoom __instance, BoomSettings __state)
    {
        if (active is not { } camera)
            return;
        try
        {
            if (__state.Config is not null)
            {
                __state.Config.SocketOffset = __state.SocketOffset;
                __state.Config.TargetArmLength = __state.ArmLength;
                __state.Config.FocusSpeed = __state.FocusSpeed;
                __instance.DefaultOffset = __state.PivotOffset;
#if DEBUG
                if (camera.Tracing && camera.traceBoomFrame == Time.frameCount
                    && camera.traceBoomStartYaw is float start
                    && HeroCtrlMgr.Controller?.parent?.localEulerAngles.y is float end)
                    camera.traceBoomDelta = Mathf.DeltaAngle(start, end);
#endif
                if (!__instance.BrainEnabled)
                    camera.Present(CameraManager.TpsCameraCom);
            }
        }
        catch (Exception error)
        {
            camera.StopPresentation(error);
        }
    }

    private readonly record struct BoomSettings(CameraBoomConfig? Config, Vector3 SocketOffset,
        Vector3 PivotOffset, float ArmLength, float FocusSpeed);

    private static void AfterBrain(CinemachineBrain __instance)
    {
        if (active is not { owner: not null } camera)
            return;
        try
        {
            camera.Present(__instance.OutputCamera);
        }
        catch (Exception error)
        {
            camera.StopPresentation(error);
        }
    }

    private void Present(Camera? output)
    {
        if (presentationFailed || owner is null || !output || output != CameraManager.TpsCameraCom)
            return;
        if (CameraManager.CurCameraMode != CameraMode.TPS || CameraManager.CurTpsMode != TpsMode.FREE
            || NormalCamera(owner) is null || CharacterMotionManager.HeroObj?.Pointer != owner.Pointer)
        {
            RestorePresentation();
            return;
        }

        var view = output!;
        var rotation = view.transform.rotation;
        float fov = view.fieldOfView;
        int targetFov = shoulder.FieldOfView.Value;
        if (presentedCamera != view)
        {
            RestorePresentation();
            presentedCamera = view;
            referenceFov = fov;
            nativeRotation = rotation;
            nativeFov = fov;
            log.LogInfo($"Third person rendering at {targetFov:0.#}° horizontal FOV.");
        }
        else
        {
            if (!SameRotation(rotation, appliedRotation))
                nativeRotation = rotation;
            if (!Mathf.Approximately(fov, appliedFov))
                nativeFov = fov;
        }

        (appliedRotation, appliedFov) = Framing(nativeRotation, nativeFov, referenceFov, targetFov,
            view.aspect, levelHorizon.Value ? rollReduction.Value / 100f : 0f);
        view.transform.rotation = appliedRotation;
        view.fieldOfView = appliedFov;
    }

    private void RestorePresentation()
    {
        if (presentedCamera)
        {
            var view = presentedCamera!;
            if (SameRotation(view.transform.rotation, appliedRotation))
                view.transform.rotation = nativeRotation;
            if (Mathf.Approximately(view.fieldOfView, appliedFov))
                view.fieldOfView = nativeFov;
        }

        presentedCamera = null;
    }

    private static bool SameRotation(Quaternion left, Quaternion right) =>
        Mathf.Abs(Quaternion.Dot(left, right)) >= 0.9999999f;

    private void StopPresentation(Exception error)
    {
        presentationFailed = true;
        RestorePresentation();
        log.LogWarning($"Close Shoulder framing stopped: {error.GetBaseException().Message}");
    }

    internal void ReportAimFailure(Exception error)
    {
        if (reportedAimFailure)
            return;
        reportedAimFailure = true;
        log.LogWarning($"Close Shoulder aim error: {error.GetBaseException().Message}");
    }

    public void Dispose()
    {
        try
        {
            avatar.Dispose();
            RestorePresentation();
            if (owner is { } hero && CharacterMotionManager.HeroObj?.Pointer == hero.Pointer)
            {
                if (NormalCamera(hero) is { } normal)
                    Release(normal, 0.05f);
                else
                    hero.ChangeTpsState(false, Plugin.Id);
            }
        }
        catch (Exception error)
        {
            log.LogWarning($"Could not release Close Shoulder: {error.GetBaseException().Message}");
        }
        finally
        {
            owner = null;
            if (active == this)
            {
#if DEBUG
                PeerPingTrace.Shutdown();
#endif
                active = null;
            }
            harmony.UnpatchSelf();
        }
    }
}
