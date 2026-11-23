using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

// In Debug the injected component stays registered. Only the managed feature is replaced.
public abstract class Il2CppDebugHost<TDriver, TFeature> : BasePlugin
    where TDriver : MonoBehaviour
    where TFeature : Il2CppDebugFeature
{
    public sealed override void Load()
    {
        var assemblyName = typeof(TFeature).Assembly.GetName().Name;
        var path = Path.Combine(Paths.BepInExRootPath, "development", assemblyName + ".dll");
        var driver = AddComponent<TDriver>();
        Il2CppDebugSession.Current = new Il2CppDebugSession(typeof(TFeature), path, driver, Config, Log);
    }

    public sealed override bool Unload()
    {
        Il2CppDebugSession.Current?.Dispose();
        return true;
    }
}

public abstract class Il2CppDebugFeature
{
    private MonoBehaviour driver;
    protected ConfigFile Config { get; private set; }
    protected ManualLogSource Logger { get; private set; }
    protected MonoBehaviour DebugDriver => driver;
    protected Transform transform => driver.transform;
    protected bool enabled { get; set; } = true;
    protected int GetInstanceID() => driver.GetInstanceID();
    protected static void Destroy(UnityEngine.Object value) => UnityEngine.Object.Destroy(value);

    protected void BindDebugHost(MonoBehaviour host, ConfigFile config, ManualLogSource logger)
    {
        driver = host;
        Config = config;
        Logger = logger;
    }

    protected bool IsDebugFeatureEnabled() => enabled;
}

internal sealed class Il2CppDebugSession : IDisposable
{
    internal static Il2CppDebugSession Current { get; set; }

    private readonly Type originalType;
    private readonly string path;
    private readonly MonoBehaviour driver;
    private readonly string configPath;
    private readonly ManualLogSource logger;
    private Feature current;
    private DateTime attemptedWrite;
    private double nextCheck;

    internal Il2CppDebugSession(Type originalType, string path, MonoBehaviour driver, ConfigFile config, ManualLogSource logger)
    {
        this.originalType = originalType;
        this.path = path;
        this.driver = driver;
        configPath = config.ConfigFilePath;
        this.logger = logger;
        if (!TryReload())
            Activate(new Feature(originalType));
    }

    internal void Update()
    {
        if (Time.realtimeSinceStartupAsDouble >= nextCheck)
        {
            nextCheck = Time.realtimeSinceStartupAsDouble + 0.5;
            TryReload();
        }
        if (current?.IsEnabled() == true)
            current.Update?.Invoke();
    }

    internal void LateUpdate()
    {
        if (current?.IsEnabled() == true)
            current.LateUpdate?.Invoke();
    }

    internal void Draw()
    {
        if (current?.IsEnabled() == true)
            current.Draw?.Invoke();
    }

    private bool TryReload()
    {
        if (!File.Exists(path))
            return false;
        var write = File.GetLastWriteTimeUtc(path);
        if (write == attemptedWrite)
            return false;

        Feature candidate;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var snapshot = new MemoryStream();
            file.CopyTo(snapshot);
            // Harmony-generated methods can reference the payload assembly, so load it as non-collectible.
            candidate = new Feature(Assembly.Load(snapshot.ToArray()).GetType(originalType.FullName, true));
        }
        catch (Exception error) when (error is IOException or BadImageFormatException)
        {
            // A build can still be writing the copy. Try again on the next poll.
            return false;
        }
        catch (Exception error)
        {
            attemptedWrite = write;
            logger.LogError($"Could not load the Debug DLL: {error.GetBaseException().Message}");
            return false;
        }

        attemptedWrite = write;
        var previous = current;
        try
        {
            previous?.Stop?.Invoke();
        }
        catch (Exception error)
        {
            logger.LogError($"Could not stop the previous Debug feature: {error}");
            return false;
        }

        current = null;
        try
        {
            Activate(candidate);
        }
        catch (Exception error)
        {
            logger.LogError($"Could not start the rebuilt Debug feature: {error}");
            try
            {
                candidate.Stop?.Invoke();
                if (previous != null)
                    Activate(new Feature(previous.Type));
            }
            catch (Exception restoreError)
            {
                logger.LogError($"Could not restore the previous Debug feature: {restoreError}");
            }
            return false;
        }

        logger.LogInfo($"Hot reloaded {originalType.Assembly.GetName().Name} from {Path.GetFileName(path)}.");
        return true;
    }

    private void Activate(Feature feature)
    {
        feature.Bind(driver, new ConfigFile(configPath, false), logger);
        feature.Start?.Invoke();
        current = feature;
    }

    public void Dispose()
    {
        var previous = current;
        Current = null;
        current = null;
        previous?.Stop?.Invoke();
    }

    private sealed class Feature
    {
        internal Type Type { get; }
        internal Action<MonoBehaviour, ConfigFile, ManualLogSource> Bind { get; }
        internal Func<bool> IsEnabled { get; }
        internal Action Start { get; }
        internal Action Update { get; }
        internal Action LateUpdate { get; }
        internal Action Draw { get; }
        internal Action Stop { get; }

        internal Feature(Type type)
        {
            Type = type;
            var instance = Activator.CreateInstance(type);
            Bind = BindMethod<Action<MonoBehaviour, ConfigFile, ManualLogSource>>("BindDebugHost");
            IsEnabled = BindMethod<Func<bool>>("IsDebugFeatureEnabled");
            Start = BindMethod<Action>("Awake");
            Update = BindMethod<Action>("Update");
            LateUpdate = BindMethod<Action>("LateUpdate");
            Draw = BindMethod<Action>("OnGUI");
            Stop = BindMethod<Action>("OnDestroy");

            T BindMethod<T>(string name) where T : Delegate =>
                type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.CreateDelegate<T>(instance);
        }
    }
}
