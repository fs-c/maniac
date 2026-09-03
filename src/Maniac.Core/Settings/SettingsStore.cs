using System.Text.Json;
using System.Text.Json.Serialization;
using Maniac.Core.Mania;

namespace Maniac.Core.Settings;

public sealed class SettingsStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string FilePath { get; }
    public ManiacSettings Current { get; private set; }

    public event Action<ManiacSettings>? Changed;

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath();
        Current = LoadOrDefault(FilePath);
    }

    public static string DefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "maniac-lazer");

    public static string DefaultPath() => Path.Combine(DefaultDirectory(), "settings.json");

    public void Update(Action<ManiacSettings> mutate)
    {
        mutate(Current);
        Save();
        Changed?.Invoke(Current);
    }

    public void Replace(ManiacSettings settings)
    {
        Current = Sanitize(settings);
        Save();
        Changed?.Invoke(Current);
    }

    public void Reset() => Replace(new ManiacSettings());

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        Current = Sanitize(Current);
        Current.Version = ManiacSettings.CurrentVersion;
        string json = JsonSerializer.Serialize(Current, JsonOptions);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);
        if (File.Exists(FilePath))
            File.Replace(temp, FilePath, destinationBackupFileName: null);
        else
            File.Move(temp, FilePath);
    }

    public static ManiacSettings LoadOrDefault(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new ManiacSettings();
            string json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<ManiacSettings>(json, JsonOptions);
            return Sanitize(loaded ?? new ManiacSettings());
        }
        catch
        {
            return new ManiacSettings();
        }
    }

    public static ManiacSettings Sanitize(ManiacSettings settings)
    {
        settings.General ??= new GeneralSettings();
        settings.Osu ??= new OsuSettings();
        settings.Mania ??= new ManiaSettings();
        settings.Mania.Human ??= new ManiaHumanOverrides();
        if (string.IsNullOrWhiteSpace(settings.PanelHotkey))
            settings.PanelHotkey = "F7";
        if (string.IsNullOrWhiteSpace(settings.AutoplayHotkey))
            settings.AutoplayHotkey = "F8";

        int version = settings.Version;
        if (version < 1)
            version = 1;

        settings.Mania.Skill = Clamp(settings.Mania.Skill, ManiaHumanLimits.SkillMin, ManiaHumanLimits.SkillMax, ManiaHumanLimits.SkillDefault);
        if (version < 3)
        {
            double oldSkill = settings.Mania.Skill;
            if (oldSkill == ManiaHumanLimits.SkillDefault)
                oldSkill = 75;
            settings.Mania.Skill = Math.Clamp(
                Math.Round(25 + 0.5 * oldSkill, MidpointRounding.AwayFromZero),
                ManiaHumanLimits.SkillMin,
                ManiaHumanLimits.SkillMax);
        }

        bool hasV1 = settings.Mania.Pressure != null
                     || settings.Mania.ChordRoll != null
                     || settings.Mania.Misses != null
                     || settings.Mania.TapLength != null
                     || settings.Mania.EarlyLate != null;
        if (version < 2 || hasV1)
            MigrateManiaV1(settings);

        ClampOverrides(settings.Mania.Human);
        settings.Mania.Pressure = null;
        settings.Mania.ChordRoll = null;
        settings.Mania.Misses = null;
        settings.Mania.TapLength = null;
        settings.Mania.EarlyLate = null;
        settings.Version = ManiacSettings.CurrentVersion;
        return settings;
    }

    static void MigrateManiaV1(ManiacSettings settings)
    {
        var mania = settings.Mania;
        var auto = ManiaHumanProfile.Auto(mania.Skill);
        var human = mania.Human;

        if (mania.Pressure is double pressure)
        {
            pressure = Clamp(pressure, 0, 4, 1);
            if (pressure != 1)
                human.DensitySensitivity ??= ManiaHumanParams.BasePressureK * Math.Max(0, pressure);
        }

        if (mania.ChordRoll is double chordRoll)
        {
            chordRoll = Clamp(chordRoll, 0, 4, 1);
            if (chordRoll != 1)
                human.ChordSigmaMs ??= auto.ChordSigma * Math.Max(0, chordRoll);
        }

        if (mania.Misses is double misses)
        {
            misses = Clamp(misses, 0, 4, 1);
            if (misses != 1)
                human.MissChanceAt8Nps ??= auto.MissRate * Math.Max(0, misses);
        }

        if (mania.TapLength is double tapLength)
        {
            tapLength = Clamp(tapLength, ManiaHumanLimits.TapDurationMin, ManiaHumanLimits.TapDurationMax, ManiaHumanParams.DefaultTapDurationMs);
            if (tapLength != ManiaHumanParams.DefaultTapDurationMs)
                human.TapDurationMs ??= tapLength;
        }

        if (mania.EarlyLate is double earlyLate)
        {
            earlyLate = Clamp(earlyLate, ManiaHumanLimits.EarlyLateMin, ManiaHumanLimits.EarlyLateMax, 0);
            if (earlyLate != 0)
                human.GlobalTimingOffsetMs ??= earlyLate;
        }
    }

    static void ClampOverrides(ManiaHumanOverrides human)
    {
        human.TimingSigmaMs = ClampOptional(human.TimingSigmaMs, ManiaHumanLimits.TimingSigmaMin, ManiaHumanLimits.TimingSigmaMax);
        human.FingerSigmaMs = ClampOptional(human.FingerSigmaMs, ManiaHumanLimits.FingerSigmaMin, ManiaHumanLimits.FingerSigmaMax);
        human.TimingCorrelation = ClampOptional(human.TimingCorrelation, ManiaHumanLimits.CorrelationMin, ManiaHumanLimits.CorrelationMax);
        human.DensitySensitivity = ClampOptional(human.DensitySensitivity, ManiaHumanLimits.DensitySensitivityMin, ManiaHumanLimits.DensitySensitivityMax);
        human.DensityThresholdNps = ClampOptional(human.DensityThresholdNps, ManiaHumanLimits.DensityThresholdMin, ManiaHumanLimits.DensityThresholdMax);
        human.DensityWindowMs = ClampOptional(human.DensityWindowMs, ManiaHumanLimits.DensityWindowMin, ManiaHumanLimits.DensityWindowMax);
        human.ChordSigmaMs = ClampOptional(human.ChordSigmaMs, ManiaHumanLimits.ChordSigmaMin, ManiaHumanLimits.ChordSigmaMax);
        human.MissChanceAt8Nps = ClampOptional(human.MissChanceAt8Nps, ManiaHumanLimits.MissChanceMin, ManiaHumanLimits.MissChanceMax);
        human.MissBurstDurationMs = ClampOptional(human.MissBurstDurationMs, ManiaHumanLimits.MissBurstDurationMin, ManiaHumanLimits.MissBurstDurationMax);
        human.MissBurstMultiplier = ClampOptional(human.MissBurstMultiplier, ManiaHumanLimits.MissBurstMultiplierMin, ManiaHumanLimits.MissBurstMultiplierMax);
        human.TapDurationMs = ClampOptional(human.TapDurationMs, ManiaHumanLimits.TapDurationMin, ManiaHumanLimits.TapDurationMax);
        human.HoldReleaseOffsetMs = ClampOptional(human.HoldReleaseOffsetMs, ManiaHumanLimits.HoldReleaseMin, ManiaHumanLimits.HoldReleaseMax);
        human.GlobalTimingOffsetMs = ClampOptional(human.GlobalTimingOffsetMs, ManiaHumanLimits.EarlyLateMin, ManiaHumanLimits.EarlyLateMax);
    }

    static double? ClampOptional(double? value, double min, double max)
    {
        if (value is not double v)
            return null;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return null;
        return Math.Clamp(v, min, max);
    }

    static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;
        return Math.Clamp(value, min, max);
    }
}
