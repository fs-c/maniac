namespace Maniac.Core.Settings;

public sealed class ManiacSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public bool AutoplayEnabled { get; set; }
    public string PanelHotkey { get; set; } = "F7";
    public string AutoplayHotkey { get; set; } = "F8";
    public GeneralSettings General { get; set; } = new();
    public OsuSettings Osu { get; set; } = new();
    public ManiaSettings Mania { get; set; } = new();
}

public sealed class GeneralSettings
{
    public bool PanelExpanded { get; set; } = true;
}

public sealed class OsuSettings
{
    public bool Enabled { get; set; } = true;
}

public sealed class ManiaSettings
{
    public bool Enabled { get; set; } = true;
    public double Skill { get; set; } = 75;
    public double Pressure { get; set; } = 1;
    public double ChordRoll { get; set; } = 1;
    public double Misses { get; set; } = 1;
    public double TapLength { get; set; } = 70;
    public double EarlyLate { get; set; } = 0;
}
