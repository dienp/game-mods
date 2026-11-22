#if DEBUG
using System.Diagnostics;
using System.Globalization;
using BepInEx.Logging;
using SkillBolt;
using UnityEngine;

namespace SteadyThirdPerson;

internal sealed class ShotImpactTrace : IDisposable
{
    private readonly string path;
    private readonly Stopwatch clock;
    private readonly ManualLogSource log;
    private readonly StreamWriter writer;
    private readonly Dictionary<(IntPtr Skill, int Action, IntPtr Cartoon), Shot> shots = new();
    private int rays;
    private int impacts;
    private bool failed;

    private readonly record struct Shot(int Id, string View, Ray Ray, Matrix4x4 Projection,
        int Width, int Height, float NativeSpread, float AppliedSpread);

    internal readonly record struct HitState(ShotImpactTrace? Trace, int Targets, int Surfaces);

    internal ShotImpactTrace(string path, Stopwatch clock, ManualLogSource log)
    {
        this.path = path;
        this.clock = clock;
        this.log = log;
        writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        writer.WriteLine("time_s,frame,event,view,weapon_sid,skill_action,aim_id,aim_match,victim_id,"
            + "world_x,world_y,world_z,offset_x_px,offset_y_px,ray_x_px,ray_y_px,native_spread_deg,applied_spread_deg");
    }

    internal void RecordRay(GameCamera chase, CSkillBase skill, CCartoonBase? cartoon, Camera source,
        Camera? shoulder, Ray original, Ray applied)
    {
        if (failed)
            return;
        try
        {
            var view = shoulder ? shoulder! : source;
            var shot = new Shot(++rays, chase.TraceView.View, applied,
                view.projectionMatrix * view.worldToCameraMatrix, view.pixelWidth, view.pixelHeight,
                Vector3.Angle(source.transform.forward, original.direction),
                Vector3.Angle(view.transform.forward, applied.direction));
            shots[(skill.Pointer, skill.SkillActId, cartoon?.Pointer ?? IntPtr.Zero)] = shot;
            shots[(skill.Pointer, skill.SkillActId, IntPtr.Zero)] = shot;
            Write("aim", shot.View, skill, shot, "", null, null, null, ProjectRay(shot));
        }
        catch (Exception error)
        {
            ReportFailure(error);
        }
    }

    internal static void BeforeHit(CCartoonBase __instance, CSkillBase __0, out HitState __state)
    {
        __state = default;
        if (GameCamera.Active?.Trace?.Shots is not { failed: false } trace)
            return;
        try
        {
            if (__0 is not null && __0.isWeaponSkill && CArgBase.CheckSkillOwnedHero(__0))
                __state = new HitState(trace, __instance.m_hitinfolst?.Count ?? 0, __instance.m_hitStaticInfoLst?.Count ?? 0);
        }
        catch (Exception error)
        {
            trace.ReportFailure(error);
        }
    }

    internal static void AfterHit(CCartoonBase __instance, CSkillBase __0, HitState __state)
    {
        if (__state.Trace is not { failed: false } trace)
            return;
        try
        {
            trace.RecordHits(__instance, __0, __instance.m_hitinfolst, __state.Targets, "target");
            trace.RecordHits(__instance, __0, __instance.m_hitStaticInfoLst, __state.Surfaces, "surface");
        }
        catch (Exception error)
        {
            trace.ReportFailure(error);
        }
    }

    private void RecordHits(CCartoonBase cartoon, CSkillBase skill,
        Il2CppSystem.Collections.Generic.List<SClientHitInfo>? list, int from, string kind)
    {
        if (list is null || from >= list.Count)
            return;
        int action = skill.SkillActId;
        bool exactMatch = shots.TryGetValue((skill.Pointer, action, cartoon.Pointer), out var found);
        Shot? shot = exactMatch || shots.TryGetValue((skill.Pointer, action, IntPtr.Zero), out found) ? found : null;
        string match = exactMatch ? "cartoon" : shot.HasValue ? "skill_action" : "none";
        string view = shot?.View ?? GameCamera.Active?.TraceView.View ?? "";
        for (int i = from; i < list.Count; i++)
        {
            var hit = list[i];
            if (hit is null)
                continue;
            Vector3 point = hit.hitpos;
            var projected = shot is { } aimed ? ProjectPoint(aimed, point) : null;
            var expected = shot is { } ray ? ProjectRay(ray, point) : null;
            Write(kind, view, skill, shot, match, hit.victimID, point, projected, expected);
            impacts++;
        }
    }

    private static Vector2? ProjectPoint(Shot shot, Vector3 point) =>
        ToPixels(shot, shot.Projection * new Vector4(point.x, point.y, point.z, 1f));

    private static Vector2? ProjectRay(Shot shot, Vector3? at = null)
    {
        if (at is not { } point)
            return ProjectPoint(shot, shot.Ray.GetPoint(100f));
        var origin = shot.Ray.origin;
        var direction = shot.Ray.direction;
        var start = shot.Projection * new Vector4(origin.x, origin.y, origin.z, 1f);
        var delta = shot.Projection * new Vector4(direction.x, direction.y, direction.z, 0f);
        if (Mathf.Abs(delta.w) < 0.0001f)
            return null;
        var target = shot.Projection * new Vector4(point.x, point.y, point.z, 1f);
        return ToPixels(shot, start + delta * ((target.w - start.w) / delta.w));
    }

    private static Vector2? ToPixels(Shot shot, Vector4 clip) => clip.w > 0f
        ? new Vector2(clip.x / clip.w * shot.Width * 0.5f, clip.y / clip.w * shot.Height * 0.5f)
        : null;

    private void Write(string kind, string view, CSkillBase skill, Shot? shot, string match,
        int? victim, Vector3? point, Vector2? actual, Vector2? expected)
    {
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{clock.Elapsed.TotalSeconds:F4},{Time.frameCount},{kind},{view},{skill.WeaponSID},{skill.SkillActId}," +
            $"{shot?.Id},{match},{victim},{point?.x:F4},{point?.y:F4},{point?.z:F4}," +
            $"{actual?.x:F2},{actual?.y:F2},{expected?.x:F2},{expected?.y:F2},{shot?.NativeSpread:F4},{shot?.AppliedSpread:F4}"));
    }

    private void ReportFailure(Exception error)
    {
        failed = true;
        log.LogWarning($"Shot impact recording stopped: {error.GetBaseException().Message}");
    }

    internal void Flush()
    {
        if (!failed)
            writer.Flush();
    }

    public void Dispose()
    {
        writer.Dispose();
        log.LogInfo($"Shot trace saved ({rays} aim rays, {impacts} impacts): {path}");
    }
}
#endif
