using Maniac.Core.Mania;
using Xunit;

namespace Maniac.Core.Tests;

public class ManiaPlannerTests
{
    [Fact]
    public void Tap_releases_after_delay()
    {
        var plan = ManiaPlanner.Build([new ManiaHit(100, 100, 0, false, false)]);
        Assert.Equal(2, plan.Events.Length);
        Assert.Equal(100, plan.Events[0].Time);
        Assert.True(plan.Events[0].Down);
        Assert.Equal(120, plan.Events[1].Time);
        Assert.False(plan.Events[1].Down);
        Assert.Equal(1, plan.Columns);
    }

    [Fact]
    public void Hold_releases_at_end()
    {
        var plan = ManiaPlanner.Build([new ManiaHit(0, 400, 2, true, false)]);
        Assert.Equal(0, plan.Events[0].Time);
        Assert.Equal(400, plan.Events[1].Time);
        Assert.Equal(3, plan.Columns);
    }

    [Fact]
    public void Instant_hold_uses_one_ms_delay()
    {
        var plan = ManiaPlanner.Build([new ManiaHit(50, 50, 0, false, true)]);
        Assert.Equal(51, plan.Events[1].Time);
    }

    [Fact]
    public void Stream_shortens_release_to_ninety_percent_gap()
    {
        var plan = ManiaPlanner.Build(
        [
            new ManiaHit(0, 0, 0, false, false),
            new ManiaHit(10, 10, 0, false, false),
        ]);
        Assert.Equal(9, plan.Events[1].Time);
        Assert.False(plan.Events[1].Down);
        Assert.Equal(10, plan.Events[2].Time);
        Assert.True(plan.Events[2].Down);
    }

    [Fact]
    public void Same_time_press_sorts_before_release()
    {
        var plan = ManiaPlanner.Build(
        [
            new ManiaHit(0, 0, 0, false, false),
            new ManiaHit(20, 20, 0, false, false),
        ]);
        int at20 = Array.FindIndex(plan.Events, e => e.Time == 20);
        Assert.True(plan.Events[at20].Down);
    }

    [Fact]
    public void Out_of_range_columns_are_ignored()
    {
        var plan = ManiaPlanner.Build(
        [
            new ManiaHit(0, 0, 99, false, false),
            new ManiaHit(0, 0, -1, false, false),
        ]);
        Assert.Empty(plan.Events);
        Assert.Equal(0, plan.HitCount);
    }

    [Fact]
    public void Next_in_column_is_independent_per_column()
    {
        var plan = ManiaPlanner.Build(
        [
            new ManiaHit(0, 0, 0, false, false),
            new ManiaHit(5, 5, 1, false, false),
            new ManiaHit(10, 10, 0, false, false),
        ]);
        Assert.Equal(9, plan.Events.First(e => !e.Down && e.Column == 0).Time);
        Assert.Equal(25, plan.Events.First(e => !e.Down && e.Column == 1).Time);
    }
}
