using System;
using System.Collections.Generic;
using UnityEngine;
#if ANOMALITH_IL2CPP
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif

internal static class NativeUnity
{
    internal static bool IsLocalized(Behaviour component)
    {
#if ANOMALITH_IL2CPP
        return component.GetIl2CppType().Name == "nnLocalizeDataUGUI";
#else
        return component.GetType().Name == "nnLocalizeDataUGUI";
#endif
    }

    internal static GameObject CreateObject(string name, params Type[] types)
    {
#if ANOMALITH_IL2CPP
        var nativeTypes = new Il2CppReferenceArray<Il2CppSystem.Type>(types.Length);
        for (var i = 0; i < types.Length; i++)
            nativeTypes[i] = Il2CppType.From(types[i]);
        return new GameObject(name, nativeTypes);
#else
        return new GameObject(name, types);
#endif
    }

    internal static T CreateInactive<T>(string name, Func<GameObject, T> initialize, params Type[] types)
    {
        var instance = CreateObject(name, types);
        instance.SetActive(false);
        try
        {
            return initialize(instance);
        }
        catch
        {
            UnityEngine.Object.Destroy(instance);
            throw;
        }
    }

#if ANOMALITH_IL2CPP
    internal static T Cast<T>(object value) where T : Il2CppObjectBase
    {
        return value is T typed ? typed : (value as Il2CppObjectBase)?.TryCast<T>();
    }

    internal static IEnumerable<T> Items<T>(Il2CppSystem.Collections.Generic.List<T> list)
    {
        if (list == null)
            yield break;
        for (var index = 0; index < list.Count; index++)
            yield return list[index];
    }

    internal static IEnumerable<T> FieldItems<T>(object value)
        => Items(value as Il2CppSystem.Collections.Generic.List<T>);
#else
    internal static T Cast<T>(object value) where T : class => value as T;

    internal static IEnumerable<T> Items<T>(List<T> list) => list ?? (IEnumerable<T>)Array.Empty<T>();

    internal static IEnumerable<T> FieldItems<T>(object value) => Items(value as List<T>);
#endif
}
