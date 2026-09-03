using Maniac.Core.Mania;
using Maniac.Core.Settings;
using Xunit;

namespace Maniac.Core.Tests;

public class ManiaHumanProfileTests
{
    static double Sigma(double t) => 7 * Math.Pow(16.0 / 7.0, t);
    static double Chord(double t) => 4 * Math.Pow(4, t);
    static double Finger(double t) => 3 * Math.Pow(4, t);
    static double Miss(double t) => 0.002 * Math.Pow(0.025 / 0.002, t);

    [Theory]
    [InlineData(100, -0.5)]
    [InlineData(75, 0)]
    [InlineData(25, 1)]
    [InlineData(0, 1.5)]
    public void Skill_curves_match_stretched_t(double skill, double t)
    {
        var auto = ManiaHumanProfile.Auto(skill);
        Assert.Equal(Sigma(t), auto.Sigma0, 8);
        Assert.Equal(Finger(t), auto.FingerBiasSigma, 8);
        Assert.Equal(Chord(t), auto.ChordSigma, 8);
        Assert.Equal(Miss(t), auto.MissRate, 10);
        Assert.Equal(-5 - 10 * t, auto.HoldTailBias, 8);
        Assert.Equal(0.25 - 0.13 * t, auto.Phi, 8);
        Assert.Equal(25 + 25 * t, auto.ChordWindow, 8);
        Assert.Equal(ManiaHumanParams.BasePressureK, auto.PressureK, 8);
        Assert.Equal(70, auto.TapLength, 6);
        Assert.Equal(0, auto.EarlyLate, 6);
        Assert.Equal(300, auto.DensityWindowMs, 6);
        Assert.Equal(4, auto.DensityThresholdNps, 6);
        Assert.Equal(160, auto.MissBurstDurationMs, 6);
        Assert.Equal(2.2, auto.MissBurstMultiplier, 6);
    }

    [Fact]
    public void Landmarks_preserve_the_old_0_and_100_bands()
    {
        var oldHigh = ManiaHumanProfile.Auto(75);
        var oldLow = ManiaHumanProfile.Auto(25);
        Assert.Equal(7, oldHigh.Sigma0, 6);
        Assert.Equal(16, oldLow.Sigma0, 6);
        Assert.Equal(4, oldHigh.ChordSigma, 6);
        Assert.Equal(16, oldLow.ChordSigma, 6);
        Assert.Equal(0.002, oldHigh.MissRate, 8);
        Assert.Equal(0.025, oldLow.MissRate, 8);
        Assert.Equal(-5, oldHigh.HoldTailBias, 6);
        Assert.Equal(-15, oldLow.HoldTailBias, 6);
        Assert.True(ManiaHumanProfile.Auto(100).Sigma0 < oldHigh.Sigma0);
        Assert.True(ManiaHumanProfile.Auto(0).Sigma0 > oldLow.Sigma0);
    }

    [Fact]
    public void Override_wins_and_skill_does_not_move_it()
    {
        var settings = new ManiaSettings
        {
            Skill = 100,
            Human = { TimingSigmaMs = 12, MissChanceAt8Nps = 0 },
        };
        var high = ManiaHumanProfile.Resolve(settings);
        settings.Skill = 0;
        var low = ManiaHumanProfile.Resolve(settings);
        Assert.Equal(12, high.Sigma0, 6);
        Assert.Equal(12, low.Sigma0, 6);
        Assert.Equal(0, high.MissRate, 8);
        Assert.Equal(0, low.MissRate, 8);
        Assert.Equal(Chord(-0.5), high.ChordSigma, 8);
        Assert.Equal(Chord(1.5), low.ChordSigma, 8);
    }

    [Fact]
    public void Chord_window_always_follows_skill()
    {
        var settings = new ManiaSettings
        {
            Skill = 75,
            Human = { ChordSigmaMs = 20 },
        };
        Assert.Equal(25, ManiaHumanProfile.Resolve(settings).ChordWindow, 6);
        settings.Skill = 25;
        Assert.Equal(50, ManiaHumanProfile.Resolve(settings).ChordWindow, 6);
        Assert.Equal(20, ManiaHumanProfile.Resolve(settings).ChordSigma, 6);
    }
}
