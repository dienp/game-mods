using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

internal static class Il2CppPluginContext
{
    internal static ConfigFile Config { get; set; }
    internal static ManualLogSource Log { get; set; }
}

public abstract class Il2CppPluginHost<T> : BasePlugin where T : MonoBehaviour
{
    private T behaviour;

    public sealed override void Load()
    {
        Il2CppPluginContext.Config = Config;
        Il2CppPluginContext.Log = Log;
        behaviour = AddComponent<T>();
    }

    public sealed override bool Unload()
    {
        if (behaviour != null)
            Object.Destroy(behaviour);
        return true;
    }
}
