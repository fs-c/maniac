using Maniac.Core.Geometry;
using Maniac.Core.Input;
using Maniac.Core.Osu;
using Xunit;

namespace Maniac.Core.Tests;

public class OsuDriverTests
{
    [Fact]
    public void Cursor_sits_on_circle_then_eases_to_next()
    {
        var builder = new OsuPlanBuilder();
        builder.AddCircle(100, new Vec2(0, 0), 100);
        builder.AddCircle(400, new Vec2(100, 0), 100);
        var plan = builder.Build();
        var sink = new RecordingInputSink();
        var driver = new OsuDriver();
        driver.Load(plan);
        driver.Tick(100, sink, play: true);
        Assert.Equal(0, sink.LastMouse.X, 1);
        driver.Tick(400, sink, play: true);
        Assert.Equal(100, sink.LastMouse.X, 1);
        Assert.True(sink.Actions.Exists(a => a.Down));
    }

    [Fact]
    public void Slider_follow_uses_path_samples()
    {
        var builder = new OsuPlanBuilder();
        builder.AddSlider(0, 100, new Vec2(0, 0), 400, [new Vec2(0, 0), new Vec2(100, 0)]);
        var plan = builder.Build();
        var sink = new RecordingInputSink();
        var driver = new OsuDriver();
        driver.Load(plan);
        driver.Tick(50, sink, play: true);
        Assert.Equal(50, sink.LastMouse.X, 1);
    }

    [Fact]
    public void Pause_releases_buttons()
    {
        var builder = new OsuPlanBuilder();
        builder.AddCircle(0, Vec2.PlayfieldCentre, 400);
        var driver = new OsuDriver();
        var sink = new RecordingInputSink();
        driver.Load(builder.Build());
        driver.Tick(10, sink, play: true);
        driver.Tick(20, sink, play: false);
        Assert.Contains(sink.Actions, a => !a.Down);
    }

    [Fact]
    public void Seek_back_does_not_leave_buttons_stuck()
    {
        var builder = new OsuPlanBuilder();
        builder.AddCircle(0, new Vec2(0, 0), 400);
        builder.AddCircle(500, new Vec2(50, 0), 400);
        var driver = new OsuDriver();
        var sink = new RecordingInputSink();
        driver.Load(builder.Build());
        driver.Tick(520, sink, play: true);
        driver.Tick(10, sink, play: true);
        int held = 0;
        foreach (var action in sink.Actions)
            held += action.Down ? 1 : -1;
        Assert.True(held >= 0);
        Assert.True(held <= 2);
    }
}
