#if DEBUG
using System.Diagnostics;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using StandaloneWorks;
using SteamMatchTogether;
using UnityEngine;

namespace SteadyThirdPerson;

internal sealed class PeerPingTrace
{
    private static PeerPingTrace? active;

    private readonly ManualLogSource log;
    private readonly ConfigEntry<int> duration;
    private readonly ConfigEntry<KeyCode> toggleKey;
    private readonly object gate = new();
    private readonly Dictionary<(string Source, ulong Id), int> peers = new();
    private readonly Stopwatch clock = new();
    private StreamWriter? writer;
    private string path = "";
    private int samples;
    private double nextFlush;

    private PeerPingTrace(ManualLogSource log, ConfigEntry<int> duration, ConfigEntry<KeyCode> toggleKey)
    {
        this.log = log;
        this.duration = duration;
        this.toggleKey = toggleKey;
    }

    internal static void Install(Harmony harmony, ManualLogSource log, ConfigEntry<int> duration,
        ConfigEntry<KeyCode> toggleKey)
    {
        bool gameplay = Observe(typeof(NetStatMgr), nameof(NetStatMgr.OnPingUpdated),
            new[] { typeof(ulong), typeof(int) }, nameof(AfterGameplayPing));
        bool room = Observe(typeof(SteamMatchNetManager), nameof(SteamMatchNetManager.OnEndPointPingUpdated),
            new[] { typeof(SteamMatchNetEndPoint), typeof(int) }, nameof(AfterRoomPing));
        if (gameplay || room)
        {
            active = new PeerPingTrace(log, duration, toggleKey);
            log.LogInfo($"Peer ping diagnostics ready. Press {toggleKey.Value} to start or save a capture.");
        }

        bool Observe(Type type, string method, Type[] arguments, string observer)
        {
            try
            {
                var original = AccessTools.Method(type, method, arguments)
                    ?? throw new MissingMethodException(type.FullName, method);
                harmony.Patch(original, postfix: new HarmonyMethod(typeof(PeerPingTrace), observer));
                return true;
            }
            catch (Exception error)
            {
                log.LogWarning($"Peer ping observer {method} unavailable: {error.GetBaseException().Message}");
                return false;
            }
        }
    }

    internal static void Tick()
    {
        if (active is not { } trace)
            return;
        lock (trace.gate)
        {
            if (Application.isFocused && Input.GetKeyDown(trace.toggleKey.Value))
            {
                if (trace.writer is null)
                    trace.Start();
                else
                    trace.Finish();
            }
            else if (trace.writer is not null && trace.clock.Elapsed.TotalSeconds >= trace.duration.Value)
            {
                trace.Finish();
            }
            else if (trace.writer is not null && trace.clock.Elapsed.TotalSeconds >= trace.nextFlush)
            {
                try
                {
                    trace.writer.Flush();
                    trace.nextFlush = trace.clock.Elapsed.TotalSeconds + 1;
                }
                catch (Exception error)
                {
                    trace.Fail(error);
                }
            }
        }
    }

    internal static void Shutdown()
    {
        var trace = active;
        active = null;
        if (trace is not null)
        {
            lock (trace.gate)
                trace.Finish();
        }
    }

    private static void AfterGameplayPing(ulong __0, int __1) => active?.Record("gameplay", __0, __1);

    private static void AfterRoomPing(SteamMatchNetEndPoint __0, int __1)
    {
        if (active is not { } trace || __0 is null)
            return;
        lock (trace.gate)
        {
            if (trace.writer is null)
                return;
            try
            {
                trace.Write("steam_room", __0.target.m_SteamID, __1);
            }
            catch (Exception error)
            {
                trace.Fail(error);
            }
        }
    }

    private void Record(string source, ulong peer, int ping)
    {
        lock (gate)
        {
            if (writer is null)
                return;
            try
            {
                Write(source, peer, ping);
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }
    }

    private void Write(string source, ulong peer, int ping)
    {
        if (!peers.TryGetValue((source, peer), out int alias))
        {
            alias = peers.Count + 1;
            peers.Add((source, peer), alias);
        }
        int? measuredPing = ping >= 0 ? ping : null;
        int? status = ping < 0 ? ping : null;
        writer!.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{DateTimeOffset.UtcNow:O},{clock.Elapsed.TotalSeconds:F3},{source},{alias},{measuredPing},{status}"));
        samples++;
    }

    private void Start()
    {
        try
        {
            string directory = Path.Combine(Paths.BepInExRootPath, "diagnostics", "steady-third-person");
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, $"peer-ping-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv");
            writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
            peers.Clear();
            samples = 0;
            nextFlush = 1;
            clock.Restart();
            writer.WriteLine("utc_timestamp,elapsed_s,source,peer,ping_ms,status_code");
            writer.Flush();
            log.LogInfo($"Peer ping trace started: {path}");
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    private void Finish()
    {
        var recording = writer;
        writer = null;
        if (recording is null)
            return;
        clock.Stop();
        try
        {
            recording.Dispose();
            log.LogInfo($"Peer ping trace saved ({samples} samples): {path}");
        }
        catch (Exception error)
        {
            log.LogWarning($"Could not finish the peer ping trace: {error.GetBaseException().Message}");
        }
    }

    private void Fail(Exception error)
    {
        log.LogWarning($"Peer ping trace stopped: {error.GetBaseException().Message}");
        Finish();
    }
}
#endif
