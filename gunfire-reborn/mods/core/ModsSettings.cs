using BepInEx.Configuration;

namespace GunfireReborn.Mods.Core;

/// <summary>Registers a mod's BepInEx configuration in a group under Settings → Mods.</summary>
public sealed class ModsSettings
{
    private readonly ConfigFile config;
    private readonly string group;
    private readonly string section;

    public ModsSettings(ConfigFile config, string group, string section = "Settings")
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(group))
            throw new ArgumentException("A mod group name is required.", nameof(group));

        this.config = config;
        this.group = group;
        this.section = section;
    }

    public ConfigEntry<bool> Toggle(string key, string label, string help, bool defaultValue = false) =>
        Add(key, label, defaultValue, new ConfigDescription(help));

    public ConfigEntry<int> Slider(string key, string label, string help, int defaultValue, int min, int max) =>
        Add(key, label, defaultValue, new ConfigDescription(help, new AcceptableValueRange<int>(min, max)));

    public ConfigEntry<float> Slider(string key, string label, string help, float defaultValue, float min, float max) =>
        Add(key, label, defaultValue, new ConfigDescription(help, new AcceptableValueRange<float>(min, max)));

    private ConfigEntry<T> Add<T>(string key, string label, T defaultValue, ConfigDescription description)
    {
        var entry = config.Bind(section, key, defaultValue, description);
        NativeModsMenu.Active?.Add(entry, label, group);
        return entry;
    }
}
