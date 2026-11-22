using Game;
using HarmonyLib;
using SkillBolt;
using UnityEngine;

namespace SteadyThirdPerson;

internal static class ShoulderAim
{
    [ThreadStatic]
    private static AimScope scope;

    private readonly record struct AimScope(GameCamera? Chase, Camera? Source, Camera? View
#if DEBUG
        , CSkillBase? Skill, CCartoonBase? Cartoon
#endif
    );

    internal static void Install(Harmony harmony)
    {
        foreach (string method in new[]
        {
            nameof(CArgBase.GetAimTargetPosition),
            nameof(CArgBase.GetSceneCenterPosition),
            nameof(CArgBase.CrtArgSightFakerAnglePos)
        })
        {
            harmony.Patch(AccessTools.Method(typeof(CArgBase), method),
                prefix: Hook(nameof(BeginAim)), finalizer: Hook(nameof(EndAim)));
        }

        harmony.Patch(AccessTools.Method(typeof(UnityHelper), nameof(UnityHelper.GetRayByScreenPos),
                new[] { typeof(Camera), typeof(float), typeof(float) }),
            postfix: Hook(nameof(RebaseScreenRay)));

#if DEBUG
        foreach (var (type, method) in new[]
        {
            (typeof(RayCastCartoon), "_addHitInfo"),
            (typeof(RaycastTimerCartoon), "AddHitInfo"),
            (typeof(TraceCartoon), "_addHitInfo"),
            (typeof(TraceLineCartoon), "_addHitInfo")
        })
        {
            harmony.Patch(AccessTools.Method(type, method, new[] { typeof(CSkillBase), typeof(RaycastHit) }),
                prefix: new HarmonyMethod(typeof(ShotImpactTrace), nameof(ShotImpactTrace.BeforeHit)),
                postfix: new HarmonyMethod(typeof(ShotImpactTrace), nameof(ShotImpactTrace.AfterHit)));
        }
#endif
    }

    private static HarmonyMethod Hook(string method) => new(typeof(ShoulderAim), method);

    private static bool IsLocalWeapon(CSkillBase? skill) =>
        skill is not null && skill.isWeaponSkill && CArgBase.CheckSkillOwnedHero(skill);

    private static void BeginAim(CSkillBase __0, CCartoonBase __1, out AimScope __state)
    {
        __state = scope;
        scope = default;
        if (GameCamera.Active is not { } chase)
            return;
        try
        {
            var shoulder = chase.ShoulderAimCamera;
#if DEBUG
            if (!shoulder && chase.Trace is null)
#else
            if (!shoulder)
#endif
                return;
            if (!IsLocalWeapon(__0))
                return;
            scope = new AimScope(chase, CameraManager.MainCameraCom, shoulder
#if DEBUG
                , __0, __1
#endif
            );
        }
        catch (Exception error)
        {
            chase.ReportAimFailure(error);
        }
    }

    private static void EndAim(AimScope __state) => scope = __state;

    private static void RebaseScreenRay(Camera __0, ref Ray __result)
    {
        if (scope.Chase is not { } chase || !__0 || __0 != scope.Source)
            return;
        try
        {
#if DEBUG
            var original = __result;
#endif
            var shoulder = scope.View;
            if (shoulder)
                __result = chase.RebaseAimRay(__result, __0.transform, shoulder!.transform);
#if DEBUG
            if (chase.Trace?.Shots is { } shots && scope.Skill is { } skill)
                shots.RecordRay(chase, skill, scope.Cartoon, __0, shoulder, original, __result);
#endif
        }
        catch (Exception error)
        {
            chase.ReportAimFailure(error);
        }
    }
}
