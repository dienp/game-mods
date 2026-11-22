using BepInEx.Logging;
using CommonDefine;
using Game;
using GameCoder.Engine;
using Il2CppInterop.Runtime;
using Item;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteadyThirdPerson;

internal sealed class ChaseAvatar : IDisposable
{
    private static readonly int WeaponType = Animator.StringToHash("WeaponType");
    private static readonly int DualWield = Animator.StringToHash("IsDW");

    private readonly ManualLogSource log;
    private Transform? model;
    private Animator? animator;
    private GameObject? mainWeapon;
    private GameObject? deputyWeapon;
    private WeaponAnimation? avatarAnimation;
    private WeaponAnimation[] mainAnimations = Array.Empty<WeaponAnimation>();
    private WeaponAnimation[] deputyAnimations = Array.Empty<WeaponAnimation>();
    private int mainId;
    private int deputyId;
    private int? originalWeaponType;
    private bool? originalDualWield;
    private bool reportedError;
    private bool wasFiring;
    private bool wasReloadingMain;
    private bool wasReloadingDeputy;
    private float? lastShot;

    internal ChaseAvatar(ManualLogSource log) => this.log = log;

    internal void Update(NewPlayerObject? hero)
    {
        var player = hero?.PlayerCom;
        if (player is null || !player.TPSModel || !player.TPSModel.gameObject.activeInHierarchy)
        {
            Dispose();
            return;
        }

        var nextModel = player.TPSModel;
        int nextMain = player.CurWeaponID;
        int nextDeputy = player.DeputyWeaponID;
        if (model == nextModel && mainId == nextMain && deputyId == nextDeputy)
        {
            UpdateAnimations(hero!, player);
            return;
        }

        Dispose();
        model = nextModel;
        mainId = nextMain;
        deputyId = nextDeputy;
        try
        {
            bool dualWield = nextDeputy > 0 && nextDeputy != nextMain;
            mainWeapon = AttachWeapon(hero!, player, model, nextMain, player.CurWeaponSID, "Bip001 Prop1", dualWield ? "right" : null);
            if (dualWield)
                deputyWeapon = AttachWeapon(hero!, player, model, nextDeputy, player.DeputyWeaponSID, "Bip001 Prop2", "left");

            if (player.CurWeaponData is { } weapon && player.TPSAnimator)
                SetWeaponPose(player.TPSAnimator, weapon.WeaponType, dualWield);

            avatarAnimation = player.TPSAnimator ? new WeaponAnimation(player.TPSAnimator) : null;
            mainAnimations = GetWeaponAnimations(mainWeapon);
            deputyAnimations = GetWeaponAnimations(deputyWeapon);
#if DEBUG
            if (avatarAnimation is not null)
                log.LogInfo($"Centered Chase avatar animation: {avatarAnimation.DescribeControls()}.");
            if (mainAnimations.Length > 0)
                log.LogInfo($"Centered Chase weapon animation: {string.Join("; ", mainAnimations.Select(binding => binding.DescribeControls()))}.");
#endif
            lastShot = GetLastShot(player);
            UpdateAnimations(hero!, player);
        }
        catch (Exception error)
        {
            if (!reportedError)
            {
                reportedError = true;
                log.LogWarning($"Centered Chase could not display the equipped weapon: {error.GetBaseException().Message}");
            }
        }
    }

    private GameObject? AttachWeapon(NewPlayerObject hero, OCPlayerBase player, Transform model,
        int id, int sid, string bone, string? hand)
    {
        if (sid <= 0)
            return null;

        var attachment = GOUtility.GetTransformByName(model, bone, true);
        if (!attachment)
        {
            log.LogWarning($"Centered Chase did not find {bone} for the equipped weapon.");
            return null;
        }

        var weapon = player.GetCurWeapon(id);
        var nativeModel = weapon?.Weapon3rd;
        if (nativeModel && nativeModel!.gameObject.activeInHierarchy && nativeModel.IsChildOf(attachment))
            return null;

        int shape = weapon?.Shape ?? ItemManager.GetItemByID(id)?.Shape ?? sid;
        if (shape <= 0)
            shape = sid;

        // Use the same weapon prefab as teammate and spectator presentation.
        string thirdPersonPath = WeaponDefine.Get3rdWeaponPath(shape, hero.Shape);
        var copy = Mount(LoadModel(thirdPersonPath), attachment, shape, hero.SID, hand)
            ?? Mount(nativeModel?.gameObject, attachment, shape, hero.SID, hand)
            ?? Mount(LoadModel(WeaponDefine.GetWeaponShowModel(shape)), attachment, shape, hero.SID, hand);
        if (!copy)
        {
            log.LogWarning($"Centered Chase could not find visible weapon meshes for shape {shape}.");
            return null;
        }

        log.LogInfo($"Centered Chase mounted {copy!.name} on {bone}.");
        return copy;

        static GameObject? LoadModel(string path) =>
            DYResourceManager.Load(path, Il2CppType.Of<GameObject>())?.TryCast<GameObject>();
    }

    private static GameObject? Mount(GameObject? source, Transform attachment, int shape, int heroSid, string? hand)
    {
        if (!source)
            return null;

        // Clone while inactive so gameplay components never start on the visual copy.
        var staging = new GameObject("Centered Chase weapon");
        staging.SetActive(false);
        GameObject? copy = null;
        try
        {
            copy = Object.Instantiate(source!, staging.transform, false);
            copy.SetActive(false);
            foreach (var component in copy.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(component);
            foreach (var body in copy.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(body);
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(body);
            foreach (var child in copy.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = attachment.gameObject.layer;
            foreach (var animation in copy.GetComponentsInChildren<Animator>(true))
                animation.applyRootMotion = false;

            var display = copy.transform;
            display.SetParent(attachment, false);
            display.localPosition = Vector3.zero;
            display.localRotation = Quaternion.identity;
            if (hand is null)
                ThirdPlayerWeaponMananger.SetThirdWeaponTran(shape, heroSid, display);
            else
                ThirdPlayerWeaponMananger.SetThirdWeaponDoubleHoldTran(shape, heroSid, hand, display);
            copy.SetActive(true);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>())
            {
                if (renderer.enabled)
                    return copy;
            }
            copy.SetActive(false);
            Object.Destroy(copy);
            return null;
        }
        catch
        {
            if (copy)
                Object.Destroy(copy);
            throw;
        }
        finally
        {
            Object.Destroy(staging);
        }
    }

    private static WeaponAnimation[] GetWeaponAnimations(GameObject? weapon)
    {
        if (!weapon)
            return Array.Empty<WeaponAnimation>();
        return weapon!.GetComponentsInChildren<Animator>().Select(animation => new WeaponAnimation(animation)).ToArray();
    }

    private static float? GetLastShot(OCPlayerBase player) =>
        player.CurWeaponASSlot?.m_ItemID2LastFireTimeDict is { } times
        && times.TryGetValue(player.CurWeaponID, out float shot) ? shot : null;

    private void UpdateAnimations(NewPlayerObject hero, OCPlayerBase player)
    {
        if (avatarAnimation is null && mainAnimations.Length == 0 && deputyAnimations.Length == 0)
            return;

        bool reloadMain = mainId != 0 && HeroAttackCtrl.GetIsReload(mainId);
        bool reloadDeputy = deputyId > 0 && HeroAttackCtrl.GetIsReload(deputyId);
        bool firing = HeroAttackCtrl.IsFire;
        bool deputyFiring = firing && HeroAttackCtrl.DeputyFire;
        float? shot = GetLastShot(player);
        bool startedFiring = firing && !wasFiring || shot.HasValue && shot != lastShot;
        float reloadSpeed = hero.AniCom?.ReloadSpeed ?? 1f;

        avatarAnimation?.Update(firing, startedFiring, reloadMain || reloadDeputy,
            reloadMain && !wasReloadingMain || reloadDeputy && !wasReloadingDeputy, reloadSpeed);
        foreach (var animation in mainAnimations)
            animation.Update(firing && !deputyFiring, startedFiring && !deputyFiring,
                reloadMain, reloadMain && !wasReloadingMain, reloadSpeed);
        foreach (var animation in deputyAnimations)
            animation.Update(deputyFiring, startedFiring && deputyFiring,
                reloadDeputy, reloadDeputy && !wasReloadingDeputy, reloadSpeed);

        wasFiring = firing;
        wasReloadingMain = reloadMain;
        wasReloadingDeputy = reloadDeputy;
        lastShot = shot;
    }

    private void SetWeaponPose(Animator target, int weaponType, bool dualWield)
    {
        animator = target;
        foreach (var parameter in target.parameters)
        {
            if (parameter.nameHash == WeaponType && parameter.type == AnimatorControllerParameterType.Int)
            {
                originalWeaponType = target.GetInteger(WeaponType);
                target.SetInteger(WeaponType, weaponType);
            }
            if (parameter.nameHash == DualWield && parameter.type == AnimatorControllerParameterType.Bool)
            {
                originalDualWield = target.GetBool(DualWield);
                target.SetBool(DualWield, dualWield);
            }
        }
    }

    public void Dispose()
    {
        if (!model && !mainWeapon && !deputyWeapon)
            return;

        if (animator)
        {
            avatarAnimation?.Clear();
            if (originalWeaponType is int weaponType)
                animator!.SetInteger(WeaponType, weaponType);
            if (originalDualWield is bool dualWield)
                animator!.SetBool(DualWield, dualWield);
        }
        if (mainWeapon)
        {
            mainWeapon!.SetActive(false);
            Object.Destroy(mainWeapon);
        }
        if (deputyWeapon)
        {
            deputyWeapon!.SetActive(false);
            Object.Destroy(deputyWeapon);
        }

        animator = null;
        avatarAnimation = null;
        model = null;
        mainWeapon = null;
        deputyWeapon = null;
        mainId = 0;
        deputyId = 0;
        originalWeaponType = null;
        originalDualWield = null;
        mainAnimations = Array.Empty<WeaponAnimation>();
        deputyAnimations = Array.Empty<WeaponAnimation>();
        wasFiring = false;
        wasReloadingMain = false;
        wasReloadingDeputy = false;
        lastShot = null;
    }

    private sealed class WeaponAnimation
    {
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Reload = Animator.StringToHash("Reload");
        private static readonly int ReloadSpeed = Animator.StringToHash("ReloadSpeed");
        private readonly Animator animator;
        private readonly AnimatorControllerParameterType? attackType;
        private readonly AnimatorControllerParameterType? reloadType;
        private readonly bool hasReloadSpeed;

        internal WeaponAnimation(Animator animator)
        {
            this.animator = animator;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == Attack)
                    attackType = parameter.type;
                else if (parameter.nameHash == Reload)
                    reloadType = parameter.type;
                else if (parameter.nameHash == ReloadSpeed)
                    hasReloadSpeed = parameter.type == AnimatorControllerParameterType.Float;
            }
        }

        internal void Update(bool firing, bool fireStarted, bool reloading, bool reloadStarted, float reloadSpeed)
        {
            if (!animator)
                return;
            if (hasReloadSpeed && reloadStarted && reloadSpeed > 0f && float.IsFinite(reloadSpeed))
                animator.SetFloat(ReloadSpeed, reloadSpeed);
            Set(Attack, attackType, firing, fireStarted);
            Set(Reload, reloadType, reloading, reloadStarted);
        }

        internal void Clear()
        {
            Set(Attack, attackType, false, false);
            Set(Reload, reloadType, false, false);
        }

#if DEBUG
        internal string DescribeControls() =>
            $"Attack={attackType?.ToString() ?? "absent"}, Reload={reloadType?.ToString() ?? "absent"}";
#endif

        private void Set(int parameter, AnimatorControllerParameterType? type, bool on, bool started)
        {
            if (type == AnimatorControllerParameterType.Bool && animator.GetBool(parameter) != on)
                animator.SetBool(parameter, on);
            else if (type == AnimatorControllerParameterType.Trigger && started)
                animator.SetTrigger(parameter);
        }
    }
}
