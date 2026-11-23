using System;
using System.Reflection;

internal sealed class NativeField
{
    private readonly MemberInfo member;

    private NativeField(MemberInfo member)
    {
        this.member = member;
    }

    internal static NativeField Find(Type type, string name, BindingFlags flags)
    {
#if ANOMALITH_IL2CPP
        MemberInfo member = type.GetProperty(name, flags | BindingFlags.Public);
#else
        MemberInfo member = type.GetField(name, flags);
#endif
        return member == null ? null : new NativeField(member);
    }

    internal object GetValue(object instance)
    {
#if ANOMALITH_IL2CPP
        return ((PropertyInfo)member).GetValue(instance);
#else
        return ((FieldInfo)member).GetValue(instance);
#endif
    }

    internal void SetValue(object instance, object value)
    {
#if ANOMALITH_IL2CPP
        ((PropertyInfo)member).SetValue(instance, value);
#else
        ((FieldInfo)member).SetValue(instance, value);
#endif
    }

    internal Type FieldType =>
#if ANOMALITH_IL2CPP
        ((PropertyInfo)member).PropertyType;
#else
        ((FieldInfo)member).FieldType;
#endif
}
