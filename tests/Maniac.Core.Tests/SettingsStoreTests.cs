using Maniac.Core.Settings;
using Xunit;

namespace Maniac.Core.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Roundtrip_persists_nested_values()
    {
        string path = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new SettingsStore(path);
        store.Update(s =>
        {
            s.AutoplayEnabled = true;
            s.PanelHotkey = "Insert";
            s.Osu.Enabled = false;
            s.Mania.Enabled = true;
            s.Mania.Skill = 40;
            s.Mania.TapLength = 55;
            s.General.PanelExpanded = false;
        });

        var loaded = new SettingsStore(path);
        Assert.True(loaded.Current.AutoplayEnabled);
        Assert.Equal("Insert", loaded.Current.PanelHotkey);
        Assert.False(loaded.Current.Osu.Enabled);
        Assert.True(loaded.Current.Mania.Enabled);
        Assert.Equal(40, loaded.Current.Mania.Skill);
        Assert.Equal(55, loaded.Current.Mania.TapLength);
        Assert.False(loaded.Current.General.PanelExpanded);
        Assert.Equal(ManiacSettings.CurrentVersion, loaded.Current.Version);
    }

    [Fact]
    public void Unknown_properties_are_ignored_and_missing_get_defaults()
    {
        string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """{ "version": 1, "autoplayEnabled": true, "futureThing": 12 }""");
        var loaded = SettingsStore.LoadOrDefault(path);
        Assert.True(loaded.AutoplayEnabled);
        Assert.Equal("F7", loaded.PanelHotkey);
        Assert.True(loaded.Osu.Enabled);
        Assert.Equal(75, loaded.Mania.Skill);
    }

    [Fact]
    public void Mania_humanize_fields_are_clamped()
    {
        string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """{ "version": 1, "mania": { "skill": 140, "pressure": -2, "tapLength": 5, "earlyLate": 99, "misses": 8 } }""");
        var loaded = SettingsStore.LoadOrDefault(path);
        Assert.Equal(100, loaded.Mania.Skill);
        Assert.Equal(0, loaded.Mania.Pressure);
        Assert.Equal(20, loaded.Mania.TapLength);
        Assert.Equal(40, loaded.Mania.EarlyLate);
        Assert.Equal(4, loaded.Mania.Misses);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults()
    {
        string path = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");
        var loaded = SettingsStore.LoadOrDefault(path);
        Assert.False(loaded.AutoplayEnabled);
    }
}
