using Maniac.Core.Settings;

namespace Maniac.Core.Mania;

public readonly record struct ManiaHumanField(
    string Group,
    string Title,
    double Min,
    double Max,
    double Step,
    Func<ManiaHumanParams, double> Read,
    Func<ManiaHumanOverrides, double?> OverrideOf,
    Action<ManiaHumanOverrides, double?> Assign,
    Func<double, string> Format,
    string Tooltip);

public static class ManiaHumanFields
{
    public static readonly ManiaHumanField[] All =
    [
        Field("Timing", "Timing variation", ManiaHumanLimits.TimingSigmaMin, ManiaHumanLimits.TimingSigmaMax, 0.1,
            p => p.Sigma0, h => h.TimingSigmaMs, (h, v) => h.TimingSigmaMs = v, v => $"{v:0.0} ms",
            "Base press-time spread. Higher means less consistent tapping."),
        Field("Timing", "Finger variation", ManiaHumanLimits.FingerSigmaMin, ManiaHumanLimits.FingerSigmaMax, 0.1,
            p => p.FingerBiasSigma, h => h.FingerSigmaMs, (h, v) => h.FingerSigmaMs = v, v => $"{v:0.0} ms",
            "Per-column bias so each finger is consistently a bit early or late."),
        Field("Timing", "Timing correlation", ManiaHumanLimits.CorrelationMin, ManiaHumanLimits.CorrelationMax, 0.01,
            p => p.Phi, h => h.TimingCorrelation, (h, v) => h.TimingCorrelation = v, v => $"{v:0.00}",
            "How much nearby notes share the same early/late drift. 0 is independent errors."),
        Field("Timing", "Early/late", ManiaHumanLimits.EarlyLateMin, ManiaHumanLimits.EarlyLateMax, 0.5,
            p => p.EarlyLate, h => h.GlobalTimingOffsetMs, (h, v) => h.GlobalTimingOffsetMs = v, v => $"{v:+0.0;-0.0;0} ms",
            "Constant shift applied to every press. Negative is early, positive is late."),
        Field("Density", "Density sensitivity", ManiaHumanLimits.DensitySensitivityMin, ManiaHumanLimits.DensitySensitivityMax, 0.01,
            p => p.PressureK, h => h.DensitySensitivity, (h, v) => h.DensitySensitivity = v, v => $"{v:0.00}/nps",
            "Extra timing spread added for each NPS above the density threshold."),
        Field("Density", "Density threshold", ManiaHumanLimits.DensityThresholdMin, ManiaHumanLimits.DensityThresholdMax, 0.5,
            p => p.DensityThresholdNps, h => h.DensityThresholdNps, (h, v) => h.DensityThresholdNps = v, v => $"{v:0.0} nps",
            "Notes-per-second at which density starts widening timing."),
        Field("Density", "Density lookaround", ManiaHumanLimits.DensityWindowMin, ManiaHumanLimits.DensityWindowMax, 10,
            p => p.DensityWindowMs, h => h.DensityWindowMs, (h, v) => h.DensityWindowMs = v, v => $"{v:0} ms",
            "How far before and after each note is counted when measuring density."),
        Field("Chords", "Chord spread", ManiaHumanLimits.ChordSigmaMin, ManiaHumanLimits.ChordSigmaMax, 0.1,
            p => p.ChordSigma, h => h.ChordSigmaMs, (h, v) => h.ChordSigmaMs = v, v => $"{v:0.0} ms",
            "How far apart chord keys are rolled."),
        Field("Misses", "Miss chance", ManiaHumanLimits.MissChanceMin, ManiaHumanLimits.MissChanceMax, 0.0005,
            p => p.MissRate, h => h.MissChanceAt8Nps, (h, v) => h.MissChanceAt8Nps = v, v => $"{v * 100:0.00}%",
            "Chance of skipping a note at 8 NPS. Denser patterns miss more often."),
        Field("Misses", "Miss-burst window", ManiaHumanLimits.MissBurstDurationMin, ManiaHumanLimits.MissBurstDurationMax, 10,
            p => p.MissBurstDurationMs, h => h.MissBurstDurationMs, (h, v) => h.MissBurstDurationMs = v, v => $"{v:0} ms",
            "After a miss, later notes in this window are more likely to miss too."),
        Field("Misses", "Miss-burst multiplier", ManiaHumanLimits.MissBurstMultiplierMin, ManiaHumanLimits.MissBurstMultiplierMax, 0.05,
            p => p.MissBurstMultiplier, h => h.MissBurstMultiplier, (h, v) => h.MissBurstMultiplier = v, v => $"{v:0.00}×",
            "How much the miss chance increases during a burst window."),
        Field("Releases", "Tap duration", ManiaHumanLimits.TapDurationMin, ManiaHumanLimits.TapDurationMax, 1,
            p => p.TapLength, h => h.TapDurationMs, (h, v) => h.TapDurationMs = v, v => $"{v:0} ms",
            "Average how long a non-hold key is held."),
        Field("Releases", "Hold-release offset", ManiaHumanLimits.HoldReleaseMin, ManiaHumanLimits.HoldReleaseMax, 0.5,
            p => p.HoldTailBias, h => h.HoldReleaseOffsetMs, (h, v) => h.HoldReleaseOffsetMs = v, v => $"{v:+0.0;-0.0;0} ms",
            "Bias on hold-note releases. Negative means releasing early."),
    ];

    static ManiaHumanField Field(
        string group,
        string title,
        double min,
        double max,
        double step,
        Func<ManiaHumanParams, double> read,
        Func<ManiaHumanOverrides, double?> overrideOf,
        Action<ManiaHumanOverrides, double?> assign,
        Func<double, string> format,
        string tooltip) =>
        new(group, title, min, max, step, read, overrideOf, assign, format, tooltip);
}
