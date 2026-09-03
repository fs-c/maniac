using Maniac.Core.Mania;
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
            s.Mania.Human.TapDurationMs = 55;
            s.General.PanelExpanded = false;
        });

        var loaded = new SettingsStore(path);
        Assert.True(loaded.Current.AutoplayEnabled);
        Assert.Equal("Insert", loaded.Current.PanelHotkey);
        Assert.False(loaded.Current.Osu.Enabled);
        Assert.True(loaded.Current.Mania.Enabled);
        Assert.Equal(40, loaded.Current.Mania.Skill);
        Assert.Equal(55, loaded.Current.Mania.Human.TapDurationMs);
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
        Assert.Equal(ManiaHumanLimits.SkillDefault, loaded.Mania.Skill);
    }

    [Fact]
    public void Mania_humanize_fields_are_clamped()
    {
        string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """{ "version": 3, "mania": { "skill": 140, "human": { "tapDurationMs": 5, "globalTimingOffsetMs": 99, "missChanceAt8Nps": 8, "densitySensitivity": -2 } } }""");
        var loaded = SettingsStore.LoadOrDefault(path);
        Assert.Equal(100, loaded.Mania.Skill);
        Assert.Equal(20, loaded.Mania.Human.TapDurationMs);
        Assert.Equal(40, loaded.Mania.Human.GlobalTimingOffsetMs);
        Assert.Equal(0.10, loaded.Mania.Human.MissChanceAt8Nps);
        Assert.Equal(0, loaded.Mania.Human.DensitySensitivity);
    }

    [Fact]
    public void V1_default_multipliers_stay_auto()
    {
        string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """{ "version": 1, "mania": { "skill": 75, "pressure": 1, "chordRoll": 1, "misses": 1, "tapLength": 70, "earlyLate": 0 } }""");
        var loaded = SettingsStore.LoadOrDefault(path);
        Assert.Equal(ManiacSettings.CurrentVersion, loaded.Version);
        Assert.Equal(63, loaded.Mania.Skill);
        Assert.Null(loaded.Mania.Human.DensitySensitivity);
        Assert.Null(loaded.Mania.Human.ChordSigmaMs);
        Assert.Null(loaded.Mania.Human.MissChanceAt8Nps);
        Assert.Null(loaded.Mania.Human.TapDurationMs);
        Assert.Null(loaded.Mania.Human.GlobalTimingOffsetMs);
        var resolved = ManiaHumanParams.From(loaded.Mania);
        var auto = ManiaHumanProfile.Auto(63);
        Assert.Equal(auto, resolved);
    }

    [Fact]
    public void V1_custom_multipliers_become_overrides()
    {
        string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """{ "version": 1, "mania": { "skill": 100, "pressure": 2, "chordRoll": 0, "misses": 4, "tapLength": 55, "earlyLate": -10 } }""");
        var loaded = SettingsStore.LoadOrDefault(path);
        Assert.Equal(75, loaded.Mania.Skill);
        Assert.Equal(0.30, loaded.Mania.Human.DensitySensitivity);
        Assert.Equal(0, loaded.Mania.Human.ChordSigmaMs);
        Assert.Equal(0.008, loaded.Mania.Human.MissChanceAt8Nps);
        Assert.Equal(55, loaded.Mania.Human.TapDurationMs);
        Assert.Equal(-10, loaded.Mania.Human.GlobalTimingOffsetMs);
        Assert.Null(loaded.Mania.Pressure);
        var resolved = ManiaHumanParams.From(loaded.Mania);
        Assert.Equal(0.30, resolved.PressureK, 6);
        Assert.Equal(0, resolved.ChordSigma, 6);
        Assert.Equal(0.008, resolved.MissRate, 8);
        Assert.Equal(55, resolved.TapLength, 6);
        Assert.Equal(-10, resolved.EarlyLate, 6);
        Assert.Equal(7, resolved.Sigma0, 6);
    }

    [Fact]
    public void V2_skill_is_rescaled_onto_the_stretched_range()
    {
        string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """{ "version": 2, "mania": { "skill": 75 } }""");
        var loaded = SettingsStore.LoadOrDefault(path);
        Assert.Equal(63, loaded.Mania.Skill);
        Assert.Equal(ManiacSettings.CurrentVersion, loaded.Version);
    }

    [Fact]
    public void Unversioned_v1_fields_still_migrate()
    {
        string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """{ "mania": { "skill": 100, "pressure": 2 } }""");
        var loaded = SettingsStore.LoadOrDefault(path);
        Assert.Equal(ManiacSettings.CurrentVersion, loaded.Version);
        Assert.Equal(0.30, loaded.Mania.Human.DensitySensitivity);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults()
    {
        string path = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");
        var loaded = SettingsStore.LoadOrDefault(path);
        Assert.True(loaded.AutoplayEnabled);
    }
}
