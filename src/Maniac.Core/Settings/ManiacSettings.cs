namespace Maniac.Core.Settings;

public sealed class ManiacSettings
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;
    public bool AutoplayEnabled { get; set; } = true;
    public string PanelHotkey { get; set; } = "F7";
    public string AutoplayHotkey { get; set; } = "F8";
    public GeneralSettings General { get; set; } = new();
    public OsuSettings Osu { get; set; } = new();
    public ManiaSettings Mania { get; set; } = new();
}

public sealed class GeneralSettings
{
    public bool OverlayVisible { get; set; } = true;
    public bool PanelExpanded { get; set; } = true;
}

public sealed class OsuSettings
{
    public bool Enabled { get; set; } = true;
}

public sealed class ManiaSettings
{
    public bool Enabled { get; set; } = true;
    public double Skill { get; set; } = 63;
    public bool ShowAdvanced { get; set; }
    public ManiaHumanOverrides Human { get; set; } = new();

    public double? Pressure { get; set; }
    public double? ChordRoll { get; set; }
    public double? Misses { get; set; }
    public double? TapLength { get; set; }
    public double? EarlyLate { get; set; }
}

public sealed class ManiaHumanOverrides
{
    public double? TimingSigmaMs { get; set; }
    public double? FingerSigmaMs { get; set; }
    public double? TimingCorrelation { get; set; }
    public double? DensitySensitivity { get; set; }
    public double? DensityThresholdNps { get; set; }
    public double? DensityWindowMs { get; set; }
    public double? ChordSigmaMs { get; set; }
    public double? MissChanceAt8Nps { get; set; }
    public double? MissBurstDurationMs { get; set; }
    public double? MissBurstMultiplier { get; set; }
    public double? TapDurationMs { get; set; }
    public double? HoldReleaseOffsetMs { get; set; }
    public double? GlobalTimingOffsetMs { get; set; }

    public void Clear()
    {
        TimingSigmaMs = null;
        FingerSigmaMs = null;
        TimingCorrelation = null;
        DensitySensitivity = null;
        DensityThresholdNps = null;
        DensityWindowMs = null;
        ChordSigmaMs = null;
        MissChanceAt8Nps = null;
        MissBurstDurationMs = null;
        MissBurstMultiplier = null;
        TapDurationMs = null;
        HoldReleaseOffsetMs = null;
        GlobalTimingOffsetMs = null;
    }
}
