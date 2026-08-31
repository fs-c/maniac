using Maniac.Core.Input;
using Maniac.Core.Mania;
using Xunit;

namespace Maniac.Core.Tests;

public class ManiaDriverTests
{
    [Fact]
    public void Tick_presses_and_releases_in_order()
    {
        var plan = ManiaPlanner.Build([new ManiaHit(100, 100, 0, false, false)]);
        var sink = new RecordingInputSink();
        var driver = new ManiaDriver();
        driver.Load(plan);
        driver.Tick(99, sink, play: true);
        Assert.Empty(sink.Actions);
        driver.Tick(100, sink, play: true);
        Assert.Equal(new RecordingInputSink.RecordedAction(0, true), sink.Actions[0]);
        driver.Tick(120, sink, play: true);
        Assert.Equal(new RecordingInputSink.RecordedAction(0, false), sink.Actions[1]);
    }

    [Fact]
    public void Pause_releases_held_keys()
    {
        var plan = ManiaPlanner.Build([new ManiaHit(0, 400, 1, true, false)]);
        var sink = new RecordingInputSink();
        var driver = new ManiaDriver();
        driver.Load(plan);
        driver.Tick(10, sink, play: true);
        Assert.Contains(sink.Actions, a => a is { Action: 1, Down: true });
        driver.Tick(20, sink, play: false);
        Assert.Equal(new RecordingInputSink.RecordedAction(1, false), sink.Actions[^1]);
    }

    [Fact]
    public void Seek_back_restores_holds_without_replaying_intermediates()
    {
        var plan = ManiaPlanner.Build([new ManiaHit(0, 1000, 0, true, false)]);
        var sink = new RecordingInputSink();
        var driver = new ManiaDriver();
        driver.Load(plan);
        driver.Tick(500, sink, play: true);
        int afterForward = sink.Actions.Count;
        driver.Tick(100, sink, play: true);
        Assert.True(sink.Actions.Count > afterForward);
        Assert.Equal(new RecordingInputSink.RecordedAction(0, false), sink.Actions[^2]);
        Assert.Equal(new RecordingInputSink.RecordedAction(0, true), sink.Actions[^1]);
    }

    [Fact]
    public void Unload_releases_and_stops()
    {
        var plan = ManiaPlanner.Build([new ManiaHit(0, 800, 3, true, false)]);
        var sink = new RecordingInputSink();
        var driver = new ManiaDriver();
        driver.Load(plan);
        driver.Tick(10, sink, play: true);
        driver.Unload(sink);
        Assert.Equal(new RecordingInputSink.RecordedAction(3, false), sink.Actions[^1]);
        driver.Tick(20, sink, play: true);
        Assert.Equal(2, sink.Actions.Count);
    }
}
