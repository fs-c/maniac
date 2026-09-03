using Maniac.Core.Settings;

namespace Maniac.Core.Mania;

public readonly record struct ManiaHumanParams(
    double Sigma0,
    double ChordSigma,
    double MissRate,
    double FingerBiasSigma,
    double HoldTailBias,
    double Phi,
    double PressureK,
    double TapLength,
    double EarlyLate,
    double ChordWindow,
    double DensityWindowMs,
    double DensityThresholdNps,
    double MissBurstDurationMs,
    double MissBurstMultiplier)
{
    public const double BasePressureK = 0.15;
    public const double TapDurationStd = 20;
    public const double DefaultDensityWindowMs = 300;
    public const double DefaultIdleNps = 4;
    public const double MissNpsRef = 8;
    public const double MinTapMs = 8;
    public const double MaxTapMs = 160;
    public const double DefaultTapDurationMs = 70;
    public const double DefaultMissBurstDurationMs = 160;
    public const double DefaultMissBurstMultiplier = 2.2;

    public static ManiaHumanParams From(ManiaSettings settings) =>
        ManiaHumanProfile.Resolve(settings);

    public double SigmaAt(double nps) =>
        Sigma0 * (1 + PressureK * Math.Max(0, nps - DensityThresholdNps));
}
