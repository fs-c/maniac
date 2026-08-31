using System.Diagnostics;
using Maniac.Core.Geometry;
using Maniac.Core.Input;
using Maniac.Core.Mania;
using Maniac.Core.Osu;
using Xunit;

namespace Maniac.Core.Tests;

public class DenseMapTests
{
    [Fact]
    public void Mania_30k_plan_builds_quickly()
    {
        var hits = StreamMania(30_000, columns: 7);
        var sw = Stopwatch.StartNew();
        var plan = ManiaPlanner.Build(hits);
        sw.Stop();
        Assert.Equal(30_000, plan.HitCount);
        Assert.True(sw.ElapsedMilliseconds < 500, $"mania 30k plan took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Osu_5k_plan_builds_quickly()
    {
        var sw = Stopwatch.StartNew();
        var plan = StreamOsu(5_000);
        sw.Stop();
        Assert.Equal(5_000, plan.Notes.Length);
        Assert.True(sw.ElapsedMilliseconds < 250, $"osu 5k plan took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Mania_tick_cost_does_not_scale_with_map_size()
    {
        var small = ManiaPlanner.Build(StreamMania(1_000, 4));
        var large = ManiaPlanner.Build(StreamMania(20_000, 4));
        long smallNs = TimeManiaTicks(small, 200);
        long largeNs = TimeManiaTicks(large, 200);
        Assert.True(largeNs < smallNs * 8 + 2_000_000, $"small={smallNs}ns large={largeNs}ns");
    }

    [Fact]
    public void Mania_steady_state_tick_allocates_nothing()
    {
        var plan = ManiaPlanner.Build(StreamMania(5_000, 4));
        var driver = new ManiaDriver();
        var sink = NullInputSink.Instance;
        driver.Load(plan);
        for (double t = 0; t < 2000; t += 16)
            driver.Tick(t, sink, play: true);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (double t = 2000; t < 4000; t += 16)
            driver.Tick(t, sink, play: true);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Osu_steady_state_tick_allocates_nothing()
    {
        var plan = StreamOsu(2_000);
        var driver = new OsuDriver();
        var sink = NullInputSink.Instance;
        driver.Load(plan);
        for (double t = 0; t < 2000; t += 16)
            driver.Tick(t, sink, play: true);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (double t = 2000; t < 4000; t += 16)
            driver.Tick(t, sink, play: true);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    static long TimeManiaTicks(ManiaPlayPlan plan, int ticks)
    {
        var driver = new ManiaDriver();
        var sink = NullInputSink.Instance;
        driver.Load(plan);
        driver.Tick(0, sink, play: true);
        var sw = Stopwatch.StartNew();
        for (int i = 1; i <= ticks; i++)
            driver.Tick(i * 16, sink, play: true);
        sw.Stop();
        return sw.ElapsedTicks * 1_000_000_000 / Stopwatch.Frequency;
    }

    static ManiaHit[] StreamMania(int count, int columns)
    {
        var hits = new ManiaHit[count];
        for (int i = 0; i < count; i++)
        {
            double t = i * 40;
            hits[i] = new ManiaHit(t, t, i % columns, false, false);
        }

        return hits;
    }

    static OsuPlayPlan StreamOsu(int count)
    {
        var builder = new OsuPlanBuilder();
        for (int i = 0; i < count; i++)
            builder.AddCircle(i * 40, new Vec2(i % 512, 192), 600);
        return builder.Build();
    }
}
