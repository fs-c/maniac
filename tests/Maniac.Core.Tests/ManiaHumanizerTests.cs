using Maniac.Core.Mania;
using Maniac.Core.Settings;
using Xunit;

namespace Maniac.Core.Tests;

public class ManiaHumanizerTests
{
    [Fact]
    public void Params_log_lerp_between_skill_bands()
    {
        var high = ManiaHumanParams.From(new ManiaSettings { Skill = 100 });
        var low = ManiaHumanParams.From(new ManiaSettings { Skill = 0 });
        Assert.Equal(7 * Math.Pow(16.0 / 7.0, -0.5), high.Sigma0, 8);
        Assert.Equal(7 * Math.Pow(16.0 / 7.0, 1.5), low.Sigma0, 8);
        Assert.Equal(4 * Math.Pow(4, -0.5), high.ChordSigma, 8);
        Assert.Equal(4 * Math.Pow(4, 1.5), low.ChordSigma, 8);
        Assert.Equal(0.002 * Math.Pow(0.025 / 0.002, -0.5), high.MissRate, 10);
        Assert.Equal(0.002 * Math.Pow(0.025 / 0.002, 1.5), low.MissRate, 10);
        Assert.Equal(3 * Math.Pow(4, -0.5), high.FingerBiasSigma, 8);
        Assert.Equal(3 * Math.Pow(4, 1.5), low.FingerBiasSigma, 8);
        Assert.Equal(0, high.HoldTailBias, 6);
        Assert.Equal(-20, low.HoldTailBias, 6);
        Assert.Equal(0.315, high.Phi, 6);
        Assert.Equal(0.055, low.Phi, 6);
        Assert.Equal(70, high.TapLength, 6);
        Assert.Equal(ManiaHumanParams.BasePressureK, high.PressureK, 8);
        Assert.Equal(ManiaHumanParams.DefaultDensityWindowMs, high.DensityWindowMs, 6);
        Assert.Equal(ManiaHumanParams.DefaultIdleNps, high.DensityThresholdNps, 6);
        Assert.Equal(ManiaHumanParams.DefaultMissBurstDurationMs, high.MissBurstDurationMs, 6);
        Assert.Equal(ManiaHumanParams.DefaultMissBurstMultiplier, high.MissBurstMultiplier, 6);
        Assert.True(high.SigmaAt(20) > high.SigmaAt(2));
    }

    [Fact]
    public void Same_seed_is_deterministic()
    {
        var hits = IsolatedTaps(80);
        var a = Humanize(hits, skill: 75, seed: 42);
        var b = Humanize(hits, skill: 75, seed: 42);
        Assert.Equal(a.Events.Length, b.Events.Length);
        for (int i = 0; i < a.Events.Length; i++)
        {
            Assert.Equal(a.Events[i].Time, b.Events[i].Time);
            Assert.Equal(a.Events[i].Column, b.Events[i].Column);
            Assert.Equal(a.Events[i].Down, b.Events[i].Down);
        }
    }

    [Fact]
    public void Lower_skill_is_wider_than_high_skill()
    {
        var hits = IsolatedTaps(200);
        double high = Std(PressErrors(hits, Humanize(hits, skill: 100, seed: 7, s => s.Human.MissChanceAt8Nps = 0)));
        double low = Std(PressErrors(hits, Humanize(hits, skill: 0, seed: 7, s => s.Human.MissChanceAt8Nps = 0)));
        Assert.True(low > high * 1.4, $"high={high:0.00} low={low:0.00}");
    }

    [Fact]
    public void Dense_notes_are_wider_than_sparse()
    {
        var sparse = IsolatedTaps(120, gap: 500);
        var dense = Stream(120, gap: 40);
        var settings = new ManiaSettings { Skill = 75, Human = { MissChanceAt8Nps = 0 } };
        double sparseStd = Std(PressErrors(sparse, Humanize(sparse, settings, 11)));
        double denseStd = Std(PressErrors(dense, Humanize(dense, settings, 11)));
        Assert.True(denseStd > sparseStd * 1.2, $"sparse={sparseStd:0.00} dense={denseStd:0.00}");
    }

    [Fact]
    public void Tap_duration_is_not_the_perfect_auto_delay()
    {
        var hits = IsolatedTaps(80, gap: 400);
        var plan = Humanize(hits, skill: 75, seed: 3, s => s.Human.MissChanceAt8Nps = 0);
        var durations = TapDurations(plan);
        Assert.Equal(hits.Length, durations.Length);
        Assert.InRange(durations.Average(), 40, 110);
        Assert.True(durations.Average() > ManiaPlanner.ReleaseDelay + 15);
    }

    [Fact]
    public void Chord_keys_are_not_simultaneous()
    {
        var hits = new ManiaHit[4];
        for (int c = 0; c < 4; c++)
            hits[c] = new ManiaHit(0, 0, c, false, false);
        var plan = Humanize(hits, skill: 50, seed: 9, s => s.Human.MissChanceAt8Nps = 0);
        var presses = plan.Events.Where(e => e.Down).Select(e => Math.Floor(e.Time)).ToArray();
        Assert.Equal(4, presses.Length);
        Assert.Equal(presses.Length, presses.Distinct().Count());
    }

    [Fact]
    public void Events_stay_legal_per_column()
    {
        var hits = Stream(400, gap: 30, columns: 4);
        var plan = Humanize(hits, skill: 0, seed: 21);
        AssertLegal(plan);
    }

    [Fact]
    public void Misses_drop_notes_on_dense_low_skill()
    {
        var hits = Stream(300, gap: 40, columns: 4);
        var source = ManiaPlanner.Build(hits);
        var plan = new ManiaHumanizer(new ManiaSettings { Skill = 0, Human = { MissChanceAt8Nps = 0.1 } }, 5).Apply(source);
        Assert.True(plan.HitCount < hits.Length);
        Assert.True(plan.HitCount > 0);
        Assert.Same(source.Hits, plan.Hits);
        AssertLegal(plan);
    }

    [Fact]
    public void Reapply_uses_original_hits()
    {
        var hits = IsolatedTaps(40);
        var source = ManiaPlanner.Build(hits);
        var first = new ManiaHumanizer(new ManiaSettings { Skill = 80 }, 4).Apply(source);
        var second = new ManiaHumanizer(new ManiaSettings { Skill = 20 }, 4).Apply(first);
        Assert.Same(source.Hits, second.Hits);
        AssertLegal(second);
    }

    static ManiaPlayPlan Humanize(ManiaHit[] hits, double skill, int seed, Action<ManiaSettings>? tweak = null)
    {
        var settings = new ManiaSettings { Skill = skill };
        tweak?.Invoke(settings);
        return Humanize(hits, settings, seed);
    }

    static ManiaPlayPlan Humanize(ManiaHit[] hits, ManiaSettings settings, int seed) =>
        new ManiaHumanizer(settings, seed).Apply(ManiaPlanner.Build(hits));

    static ManiaHit[] IsolatedTaps(int count, double gap = 500)
    {
        var hits = new ManiaHit[count];
        for (int i = 0; i < count; i++)
        {
            double t = i * gap;
            hits[i] = new ManiaHit(t, t, 0, false, false);
        }

        return hits;
    }

    static ManiaHit[] Stream(int count, double gap, int columns = 1)
    {
        var hits = new ManiaHit[count];
        for (int i = 0; i < count; i++)
        {
            double t = i * gap;
            hits[i] = new ManiaHit(t, t, i % columns, false, false);
        }

        return hits;
    }

    static double[] PressErrors(ManiaHit[] hits, ManiaPlayPlan plan)
    {
        var next = new int[ManiaPlanner.MaxColumn + 1];
        var presses = new List<double>[ManiaPlanner.MaxColumn + 1];
        foreach (var e in plan.Events)
        {
            if (!e.Down)
                continue;
            presses[e.Column] ??= [];
            presses[e.Column].Add(e.Time);
        }

        var errors = new List<double>();
        foreach (var hit in hits)
        {
            var list = presses[hit.Column];
            int i = next[hit.Column];
            if (list == null || i >= list.Count)
                continue;
            errors.Add(list[i] - hit.Start);
            next[hit.Column] = i + 1;
        }

        return errors.ToArray();
    }

    static double[] TapDurations(ManiaPlayPlan plan)
    {
        var downAt = new double?[ManiaPlanner.MaxColumn + 1];
        var durations = new List<double>();
        foreach (var e in plan.Events)
        {
            if (e.Down)
            {
                downAt[e.Column] = e.Time;
                continue;
            }

            if (downAt[e.Column] is not double start)
                continue;
            durations.Add(e.Time - start);
            downAt[e.Column] = null;
        }

        return durations.ToArray();
    }

    static double Std(double[] values)
    {
        Assert.True(values.Length > 2);
        double mean = values.Average();
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double d = values[i] - mean;
            sum += d * d;
        }

        return Math.Sqrt(sum / (values.Length - 1));
    }

    static void AssertLegal(ManiaPlayPlan plan)
    {
        var held = new bool[ManiaPlanner.MaxColumn + 1];
        double[] last = new double[ManiaPlanner.MaxColumn + 1];
        Array.Fill(last, double.NegativeInfinity);
        foreach (var e in plan.Events)
        {
            Assert.False(held[e.Column] == e.Down, $"column {e.Column} repeated {(e.Down ? "press" : "release")} at {e.Time}");
            Assert.True(e.Time >= last[e.Column], $"column {e.Column} went backwards");
            if (e.Down)
                Assert.True(e.Time > last[e.Column] || last[e.Column] < 0, "press overlapped previous release");
            held[e.Column] = e.Down;
            last[e.Column] = e.Time;
        }

        Assert.All(held, h => Assert.False(h));
    }
}
