using BepInEx.Configuration;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace SteadyThirdPerson;

public sealed class CameraControls : MonoBehaviour
{
    private GameCamera? camera;
    private ManualLogSource log = null!;
    private ConfigEntry<KeyCode> toggleKey = null!;
    private ConfigEntry<float> transition = null!;
    private ConfigEntry<bool> levelHorizon = null!;
    private IntPtr owningHero;

    public CameraControls(IntPtr ptr) : base(ptr)
    {
    }

    [HideFromIl2Cpp]
    internal void Initialize(GameCamera gameCamera, ManualLogSource logger, ConfigEntry<KeyCode> key,
        ConfigEntry<float> transitionSeconds, ConfigEntry<bool> keepLevel)
    {
        log = logger;
        toggleKey = key;
        transition = transitionSeconds;
        levelHorizon = keepLevel;
        camera = gameCamera;
    }

    private void Update()
    {
        if (camera is not { } active)
            return;

        try
        {
            if (!Input.GetKeyDown(toggleKey.Value))
                return;

            var hero = active.LocalHero;
            if (hero is null || !active.CanSwitch(hero))
                return;

            if (owningHero == hero.Pointer)
            {
                Release(active, hero);
            }
            else if (active.IsFirstPerson)
            {
                owningHero = hero.Pointer;
                active.Enter(hero, transition.Value);
            }
        }
        catch (Exception error)
        {
            Stop(active, error);
        }
    }

    private void LateUpdate()
    {
        if (camera is not { } active || owningHero == IntPtr.Zero || !levelHorizon.Value)
            return;

        try
        {
            var hero = active.LocalHero;
            if (hero is null || hero.Pointer != owningHero || !active.CanSwitch(hero))
                return;

            var view = active.ActiveThirdPersonCamera;
            if (view == null)
                return;

            var forward = view.forward;
            if (Mathf.Abs(forward.y) < 0.999f)
                view.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        catch (Exception error)
        {
            Stop(active, error);
        }
    }

    private void OnDestroy()
    {
        if (camera is { } active)
            TryRelease(active);
    }

    [HideFromIl2Cpp]
    private void Release(GameCamera active, Il2CppObjectBase hero)
    {
        active.Leave(hero, transition.Value);
        owningHero = IntPtr.Zero;
    }

    [HideFromIl2Cpp]
    private void Stop(GameCamera active, Exception error)
    {
        camera = null;
        log.LogWarning($"Camera controls stopped: {error.GetBaseException().Message}");
        TryRelease(active);
    }

    [HideFromIl2Cpp]
    private void TryRelease(GameCamera active)
    {
        if (owningHero == IntPtr.Zero)
            return;

        try
        {
            var hero = active.LocalHero;
            if (hero?.Pointer == owningHero && active.CanSwitch(hero))
                Release(active, hero);
        }
        catch (Exception error)
        {
            log.LogWarning($"Could not release the third-person camera: {error.GetBaseException().Message}");
        }
    }
}
