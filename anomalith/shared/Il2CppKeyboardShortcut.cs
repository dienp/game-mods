using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

internal readonly struct KeyboardShortcut
{
    private readonly KeyCode key;
    private readonly KeyCode[] modifiers;

    static KeyboardShortcut()
    {
        TomlTypeConverter.AddConverter(typeof(KeyboardShortcut), new TypeConverter
        {
            ConvertToString = (value, _) => value.ToString(),
            ConvertToObject = (value, _) => Parse(value)
        });
    }

    internal KeyboardShortcut(KeyCode key, params KeyCode[] modifiers)
    {
        this.key = key;
        this.modifiers = modifiers;
    }

    internal bool IsDown()
    {
        return key != KeyCode.None && Input.GetKeyDown(key) &&
            (modifiers == null || modifiers.All(modifier => Input.GetKey(modifier)));
    }

    public override string ToString()
    {
        return string.Join(" + ", new[] { key }.Concat(modifiers ?? Array.Empty<KeyCode>()));
    }

    private static KeyboardShortcut Parse(string value)
    {
        var keys = value.Split('+', StringSplitOptions.TrimEntries)
            .Select(part => Enum.Parse<KeyCode>(part, ignoreCase: true))
            .ToArray();
        return new KeyboardShortcut(keys[0], keys.Skip(1).ToArray());
    }
}
