using Maniac.Core.Mania;
using Maniac.Core.Settings;
using osu.Framework.Graphics;

namespace Maniac.Plugin.Ui;

readonly record struct HumanSliderSpec(
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

static class HumanSliderLayout
{
    public static readonly HumanSliderSpec[] Specs =
    [
        Spec("Timing", "Timing variation", ManiaHumanLimits.TimingSigmaMin, ManiaHumanLimits.TimingSigmaMax, 0.1,
            p => p.Sigma0, h => h.TimingSigmaMs, (h, v) => h.TimingSigmaMs = v, v => $"{v:0.0} ms",
            "Base press-time spread. Higher means less consistent tapping."),
        Spec("Timing", "Finger variation", ManiaHumanLimits.FingerSigmaMin, ManiaHumanLimits.FingerSigmaMax, 0.1,
            p => p.FingerBiasSigma, h => h.FingerSigmaMs, (h, v) => h.FingerSigmaMs = v, v => $"{v:0.0} ms",
            "Per-column bias so each finger is consistently a bit early or late."),
        Spec("Timing", "Timing correlation", ManiaHumanLimits.CorrelationMin, ManiaHumanLimits.CorrelationMax, 0.01,
            p => p.Phi, h => h.TimingCorrelation, (h, v) => h.TimingCorrelation = v, v => $"{v:0.00}",
            "How much nearby notes share the same early/late drift. 0 is independent errors."),
        Spec("Timing", "Early/late", ManiaHumanLimits.EarlyLateMin, ManiaHumanLimits.EarlyLateMax, 0.5,
            p => p.EarlyLate, h => h.GlobalTimingOffsetMs, (h, v) => h.GlobalTimingOffsetMs = v, v => $"{v:+0.0;-0.0;0} ms",
            "Constant shift applied to every press. Negative is early, positive is late."),
        Spec("Density", "Density sensitivity", ManiaHumanLimits.DensitySensitivityMin, ManiaHumanLimits.DensitySensitivityMax, 0.01,
            p => p.PressureK, h => h.DensitySensitivity, (h, v) => h.DensitySensitivity = v, v => $"{v:0.00}/nps",
            "Extra timing spread added for each NPS above the density threshold."),
        Spec("Density", "Density threshold", ManiaHumanLimits.DensityThresholdMin, ManiaHumanLimits.DensityThresholdMax, 0.5,
            p => p.DensityThresholdNps, h => h.DensityThresholdNps, (h, v) => h.DensityThresholdNps = v, v => $"{v:0.0} nps",
            "Notes-per-second at which density starts widening timing."),
        Spec("Density", "Density lookaround", ManiaHumanLimits.DensityWindowMin, ManiaHumanLimits.DensityWindowMax, 10,
            p => p.DensityWindowMs, h => h.DensityWindowMs, (h, v) => h.DensityWindowMs = v, v => $"{v:0} ms",
            "How far before and after each note is counted when measuring density."),
        Spec("Chords", "Chord spread", ManiaHumanLimits.ChordSigmaMin, ManiaHumanLimits.ChordSigmaMax, 0.1,
            p => p.ChordSigma, h => h.ChordSigmaMs, (h, v) => h.ChordSigmaMs = v, v => $"{v:0.0} ms",
            "How far apart chord keys are rolled."),
        Spec("Misses", "Miss chance", ManiaHumanLimits.MissChanceMin, ManiaHumanLimits.MissChanceMax, 0.0005,
            p => p.MissRate, h => h.MissChanceAt8Nps, (h, v) => h.MissChanceAt8Nps = v, v => $"{v * 100:0.00}%",
            "Chance of skipping a note at 8 NPS. Denser patterns miss more often."),
        Spec("Misses", "Miss-burst window", ManiaHumanLimits.MissBurstDurationMin, ManiaHumanLimits.MissBurstDurationMax, 10,
            p => p.MissBurstDurationMs, h => h.MissBurstDurationMs, (h, v) => h.MissBurstDurationMs = v, v => $"{v:0} ms",
            "After a miss, later notes in this window are more likely to miss too."),
        Spec("Misses", "Miss-burst multiplier", ManiaHumanLimits.MissBurstMultiplierMin, ManiaHumanLimits.MissBurstMultiplierMax, 0.05,
            p => p.MissBurstMultiplier, h => h.MissBurstMultiplier, (h, v) => h.MissBurstMultiplier = v, v => $"{v:0.00}×",
            "How much the miss chance increases during a burst window."),
        Spec("Releases", "Tap duration", ManiaHumanLimits.TapDurationMin, ManiaHumanLimits.TapDurationMax, 1,
            p => p.TapLength, h => h.TapDurationMs, (h, v) => h.TapDurationMs = v, v => $"{v:0} ms",
            "Average how long a non-hold key is held."),
        Spec("Releases", "Hold-release offset", ManiaHumanLimits.HoldReleaseMin, ManiaHumanLimits.HoldReleaseMax, 0.5,
            p => p.HoldTailBias, h => h.HoldReleaseOffsetMs, (h, v) => h.HoldReleaseOffsetMs = v, v => $"{v:+0.0;-0.0;0} ms",
            "Bias on hold-note releases. Negative means releasing early."),
    ];

    public static Drawable[] BuildColumn(
        SettingsStore settings,
        ManiaHumanParams resolved,
        ManiaHumanOverrides human,
        out HumanSliderBinding[] bindings)
    {
        bindings = new HumanSliderBinding[Specs.Length];
        var children = new List<Drawable>();
        string group = "";
        for (int i = 0; i < Specs.Length; i++)
        {
            var spec = Specs[i];
            if (spec.Group != group)
            {
                group = spec.Group;
                children.Add(UiLayout.Section(group));
            }

            double initial = spec.Read(resolved);
            bool custom = spec.OverrideOf(human) != null;
            var row = new AutoSliderRow(
                spec.Title, spec.Min, spec.Max, spec.Step, initial, custom,
                v => settings.Update(s => spec.Assign(s.Mania.Human, v)),
                () => settings.Update(s => spec.Assign(s.Mania.Human, null)),
                spec.Format,
                spec.Tooltip);
            bindings[i] = new HumanSliderBinding(row, spec);
            children.Add(row);
        }

        children.Add(new QuietLink(
            "Reset advanced to Auto",
            () => settings.Update(s => s.Mania.Human.Clear()),
            "Clear custom advanced values so they follow skill again."));
        return children.ToArray();
    }

    static HumanSliderSpec Spec(
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

readonly record struct HumanSliderBinding(AutoSliderRow Row, HumanSliderSpec Spec)
{
    public void Refresh(ManiaHumanParams resolved, ManiaHumanOverrides human) =>
        Row.SetValue(Spec.Read(resolved), Spec.OverrideOf(human) != null);
}
