using BepInEx.Configuration;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
#if DEBUG
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
#endif

namespace SteadyThirdPerson;

public sealed class CameraControls : MonoBehaviour
{
    private GameCamera? camera;
    private ManualLogSource log = null!;
    private ConfigEntry<bool> enabledSetting = null!;
    private ConfigEntry<KeyCode> toggleKey = null!;
    private ConfigEntry<float> transition = null!;
#if DEBUG
    private MovementTrace? trace;
#endif

    public CameraControls(IntPtr ptr) : base(ptr)
    {
    }

    [HideFromIl2Cpp]
    internal void Initialize(GameCamera gameCamera, ManualLogSource logger, ConfigEntry<bool> enabled,
        ConfigEntry<KeyCode> key, ConfigEntry<float> transitionSeconds)
    {
        log = logger;
        enabledSetting = enabled;
        toggleKey = key;
        transition = transitionSeconds;
        camera = gameCamera;
    }

    private void Update()
    {
        if (camera is not { } active)
            return;

        try
        {
            bool gameplayInput = !Cursor.visible && Time.timeScale > 0f;
#if DEBUG
            if ((gameplayInput || trace is not null) && Application.isFocused && Input.GetKeyDown(KeyCode.F9))
                ToggleTrace(active);
#endif
            if (gameplayInput && active.CanAcceptInput && Input.GetKeyDown(toggleKey.Value))
                enabledSetting.Value = !enabledSetting.Value;
            active.UpdateRequest(enabledSetting.Value, gameplayInput, transition.Value);
        }
        catch (Exception error)
        {
#if DEBUG
            StopTrace();
#endif
            camera = null;
            log.LogWarning($"Centered Chase stopped: {error.GetBaseException().Message}");
            active.Dispose();
        }
    }

    private void OnDestroy()
    {
#if DEBUG
        StopTrace();
#endif
        camera?.Dispose();
    }

#if DEBUG
    [HideFromIl2Cpp]
    private void ToggleTrace(GameCamera active)
    {
        if (trace is not null)
        {
            StopTrace();
            return;
        }
        try
        {
            trace = new MovementTrace(log);
            active.Trace = trace;
            StartCoroutine(RecordFrames(active, trace).WrapToIl2Cpp());
        }
        catch (Exception error)
        {
            StopTrace();
            log.LogWarning($"Could not start the movement trace: {error.GetBaseException().Message}");
        }
    }

    [HideFromIl2Cpp]
    private IEnumerator RecordFrames(GameCamera active, MovementTrace recording)
    {
        var nextFrame = new WaitForEndOfFrame();
        while (ReferenceEquals(trace, recording))
        {
            yield return nextFrame;
            if (!ReferenceEquals(trace, recording))
                break;
            try
            {
                if (recording.Capture(active))
                    continue;
            }
            catch (Exception error)
            {
                log.LogWarning($"Movement trace stopped: {error.GetBaseException().Message}");
            }
            StopTrace();
        }
    }

    [HideFromIl2Cpp]
    private void StopTrace()
    {
        if (camera is not null)
            camera.Trace = null;
        var recording = trace;
        trace = null;
        try
        {
            recording?.Dispose();
        }
        catch (Exception error)
        {
            log.LogWarning($"Could not finish writing the movement trace: {error.GetBaseException().Message}");
        }
    }
#endif
}
