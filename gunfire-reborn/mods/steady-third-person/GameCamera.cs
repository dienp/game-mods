using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace SteadyThirdPerson;

internal sealed class GameCamera
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private readonly PropertyInfo localHero;
    private readonly PropertyInfo downedComponent;
    private readonly PropertyInfo isDowned;
    private readonly PropertyInfo cameraMode;
    private readonly PropertyInfo thirdPersonCamera;
    private readonly MethodInfo isNormal;
    private readonly MethodInfo requestThirdPerson;
    private readonly MethodInfo hasThirdPersonRequests;
    private readonly MethodInfo enterThirdPerson;
    private readonly MethodInfo enterFirstPerson;
    private readonly object firstPersonMode;
    private readonly object thirdPersonMode;
    private readonly object freeCamera;

    internal GameCamera()
    {
        var game = Assembly.Load("Assembly-CSharp");
        var motion = FindType(game, "CharacterMotionManager");
        var cameras = FindType(game, "CameraManager");
        var heroCameras = FindType(game, "HeroCameraManager");
        var tpsMode = FindType(game, "TpsMode");

        localHero = RequireProperty(motion, "HeroObj", Static);
        var hero = localHero.PropertyType;
        downedComponent = RequireProperty(hero, "FakerDeadCom", Instance);
        isDowned = RequireProperty(downedComponent.PropertyType, "IsFakerDead", Instance);
        cameraMode = RequireProperty(cameras, "CurCameraMode", Static);
        thirdPersonCamera = RequireProperty(cameras, "TpsCamera", Static);

        isNormal = RequireMethod(heroCameras, "InNormal", Static);
        requestThirdPerson = RequireMethod(hero, "ChangeTpsState", Instance, typeof(bool), typeof(string));
        hasThirdPersonRequests = RequireMethod(hero, "IsMoreTpsState", Instance);
        enterThirdPerson = RequireMethod(heroCameras, "ChangeToTpsMode", Static, tpsMode, typeof(AnimationCurve));
        enterFirstPerson = RequireMethod(heroCameras, "ChangeToFpsMode", Static, typeof(AnimationCurve));
        firstPersonMode = Enum.Parse(cameraMode.PropertyType, "FPS");
        thirdPersonMode = Enum.Parse(cameraMode.PropertyType, "TPS");
        freeCamera = Enum.Parse(tpsMode, "FREE");
    }

    internal Il2CppObjectBase? LocalHero => localHero.GetValue(null) as Il2CppObjectBase;

    internal bool CanSwitch(Il2CppObjectBase hero)
    {
        if (isNormal.Invoke(null, null) is not true)
            return false;

        var component = downedComponent.GetValue(hero);
        return component is null || isDowned.GetValue(component) is false;
    }

    internal bool IsFirstPerson => Equals(cameraMode.GetValue(null), firstPersonMode);

    internal Transform? ActiveThirdPersonCamera => Equals(cameraMode.GetValue(null), thirdPersonMode)
        ? thirdPersonCamera.GetValue(null) as Transform
        : null;

    internal void Enter(Il2CppObjectBase hero, float seconds)
    {
        requestThirdPerson.Invoke(hero, new object[] { true, Plugin.Id });
        var blend = AnimationCurve.EaseInOut(0f, 0f, seconds, 1f);
        enterThirdPerson.Invoke(null, new[] { freeCamera, blend });
    }

    internal void Leave(Il2CppObjectBase hero, float seconds)
    {
        requestThirdPerson.Invoke(hero, new object[] { false, Plugin.Id });
        if (hasThirdPersonRequests.Invoke(hero, null) is false)
        {
            var blend = AnimationCurve.EaseInOut(0f, 1f, seconds, 0f);
            enterFirstPerson.Invoke(null, new object[] { blend });
        }
    }

    private static Type FindType(Assembly assembly, string name) =>
        assembly.GetType(name) ?? assembly.GetType($"HeroCameraName.{name}")
        ?? throw new TypeLoadException($"Assembly-CSharp is missing {name}.");

    private static PropertyInfo RequireProperty(Type type, string name, BindingFlags flags) =>
        type.GetProperty(name, flags) ?? throw new MissingMemberException(type.FullName, name);

    private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters) =>
        type.GetMethod(name, flags, null, parameters, null) ?? throw new MissingMethodException(type.FullName, name);
}
