using Maniac.Core.Settings;

namespace Maniac.Core.Mania;

public readonly struct ManiaHumanParams
{
    public const double BasePressureK = 0.15;
    public const double TapDurationStd = 20;
    public const double DensityWindowMs = 300;
    public const double IdleNps = 4;
    public const double MissNpsRef = 8;
    public const double MinTapMs = 8;
    public const double MaxTapMs = 160;

    public readonly double Sigma0;
    public readonly double ChordSigma;
    public readonly double MissRate;
    public readonly double FingerBiasSigma;
    public readonly double HoldTailBias;
    public readonly double Phi;
    public readonly double PressureK;
    public readonly double TapLength;
    public readonly double EarlyLate;
    public readonly double ChordWindow;

    public ManiaHumanParams(
        double sigma0,
        double chordSigma,
        double missRate,
        double fingerBiasSigma,
        double holdTailBias,
        double phi,
        double pressureK,
        double tapLength,
        double earlyLate,
        double chordWindow)
    {
        Sigma0 = sigma0;
        ChordSigma = chordSigma;
        MissRate = missRate;
        FingerBiasSigma = fingerBiasSigma;
        HoldTailBias = holdTailBias;
        Phi = phi;
        PressureK = pressureK;
        TapLength = tapLength;
        EarlyLate = earlyLate;
        ChordWindow = chordWindow;
    }

    public static ManiaHumanParams From(ManiaSettings settings)
    {
        double skill = Math.Clamp(settings.Skill, 0, 100);
        double t = 1 - skill / 100;
        return new ManiaHumanParams(
            sigma0: 7 * Math.Pow(16.0 / 7.0, t),
            chordSigma: 4 * Math.Pow(4, t) * Math.Max(0, settings.ChordRoll),
            missRate: 0.002 * Math.Pow(0.025 / 0.002, t) * Math.Max(0, settings.Misses),
            fingerBiasSigma: 3 * Math.Pow(4, t),
            holdTailBias: -5 - 10 * t,
            phi: 0.25 - 0.13 * t,
            pressureK: BasePressureK * Math.Max(0, settings.Pressure),
            tapLength: Math.Clamp(settings.TapLength, MinTapMs, MaxTapMs),
            earlyLate: settings.EarlyLate,
            chordWindow: 25 + 25 * t);
    }

    public double SigmaAt(double nps) =>
        Sigma0 * (1 + PressureK * Math.Max(0, nps - IdleNps));
}
