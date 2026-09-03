using Maniac.Core.Settings;

namespace Maniac.Core.Mania;

public static class ManiaHumanProfile
{
    public static double SkillT(double skill)
    {
        skill = Math.Clamp(skill, ManiaHumanLimits.SkillMin, ManiaHumanLimits.SkillMax);
        // Old 0–100 occupies 25–75. t=-0.5 at skill 100, t=1.5 at skill 0.
        return 1.5 - skill / 50;
    }

    public static ManiaHumanParams Auto(double skill)
    {
        double t = SkillT(skill);
        return new ManiaHumanParams(
            Sigma0: 7 * Math.Pow(16.0 / 7.0, t),
            ChordSigma: 4 * Math.Pow(4, t),
            MissRate: 0.002 * Math.Pow(0.025 / 0.002, t),
            FingerBiasSigma: 3 * Math.Pow(4, t),
            HoldTailBias: -5 - 10 * t,
            Phi: 0.25 - 0.13 * t,
            PressureK: ManiaHumanParams.BasePressureK,
            TapLength: ManiaHumanParams.DefaultTapDurationMs,
            EarlyLate: 0,
            ChordWindow: 25 + 25 * t,
            DensityWindowMs: ManiaHumanParams.DefaultDensityWindowMs,
            DensityThresholdNps: ManiaHumanParams.DefaultIdleNps,
            MissBurstDurationMs: ManiaHumanParams.DefaultMissBurstDurationMs,
            MissBurstMultiplier: ManiaHumanParams.DefaultMissBurstMultiplier);
    }

    public static ManiaHumanParams Resolve(ManiaSettings settings)
    {
        var auto = Auto(settings.Skill);
        var o = settings.Human ?? new ManiaHumanOverrides();
        return auto with
        {
            Sigma0 = Override(o.TimingSigmaMs, auto.Sigma0, ManiaHumanLimits.TimingSigmaMin, ManiaHumanLimits.TimingSigmaMax),
            FingerBiasSigma = Override(o.FingerSigmaMs, auto.FingerBiasSigma, ManiaHumanLimits.FingerSigmaMin, ManiaHumanLimits.FingerSigmaMax),
            Phi = Override(o.TimingCorrelation, auto.Phi, ManiaHumanLimits.CorrelationMin, ManiaHumanLimits.CorrelationMax),
            PressureK = Override(o.DensitySensitivity, auto.PressureK, ManiaHumanLimits.DensitySensitivityMin, ManiaHumanLimits.DensitySensitivityMax),
            DensityThresholdNps = Override(o.DensityThresholdNps, auto.DensityThresholdNps, ManiaHumanLimits.DensityThresholdMin, ManiaHumanLimits.DensityThresholdMax),
            DensityWindowMs = Override(o.DensityWindowMs, auto.DensityWindowMs, ManiaHumanLimits.DensityWindowMin, ManiaHumanLimits.DensityWindowMax),
            ChordSigma = Override(o.ChordSigmaMs, auto.ChordSigma, ManiaHumanLimits.ChordSigmaMin, ManiaHumanLimits.ChordSigmaMax),
            MissRate = Override(o.MissChanceAt8Nps, auto.MissRate, ManiaHumanLimits.MissChanceMin, ManiaHumanLimits.MissChanceMax),
            MissBurstDurationMs = Override(o.MissBurstDurationMs, auto.MissBurstDurationMs, ManiaHumanLimits.MissBurstDurationMin, ManiaHumanLimits.MissBurstDurationMax),
            MissBurstMultiplier = Override(o.MissBurstMultiplier, auto.MissBurstMultiplier, ManiaHumanLimits.MissBurstMultiplierMin, ManiaHumanLimits.MissBurstMultiplierMax),
            TapLength = Override(o.TapDurationMs, auto.TapLength, ManiaHumanLimits.TapDurationMin, ManiaHumanLimits.TapDurationMax),
            HoldTailBias = Override(o.HoldReleaseOffsetMs, auto.HoldTailBias, ManiaHumanLimits.HoldReleaseMin, ManiaHumanLimits.HoldReleaseMax),
            EarlyLate = Override(o.GlobalTimingOffsetMs, auto.EarlyLate, ManiaHumanLimits.EarlyLateMin, ManiaHumanLimits.EarlyLateMax),
        };
    }

    static double Override(double? value, double auto, double min, double max)
    {
        if (value is not double v || double.IsNaN(v) || double.IsInfinity(v))
            return auto;
        return Math.Clamp(v, min, max);
    }
}
