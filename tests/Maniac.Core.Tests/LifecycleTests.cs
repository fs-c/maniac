using Maniac.Core.Input;
using Maniac.Core.Mania;
using Maniac.Core.Planning;
using Xunit;

namespace Maniac.Core.Tests;

public class LifecycleTests
{
    [Fact]
    public void Reload_equivalent_load_releases_previous_plan()
    {
        var driver = new ManiaDriver();
        var sink = new RecordingInputSink();
        driver.Load(ManiaPlanner.Build([new ManiaHit(0, 400, 0, true, false)]));
        driver.Tick(10, sink, play: true);
        driver.Unload(sink);
        driver.Load(ManiaPlanner.Build([new ManiaHit(0, 0, 2, false, false)]));
        driver.Tick(0, sink, play: true);
        Assert.Contains(sink.Actions, a => a is { Action: 0, Down: false });
        Assert.Contains(sink.Actions, a => a is { Action: 2, Down: true });
    }

    [Fact]
    public void Identity_transform_does_not_change_events()
    {
        var plan = ManiaPlanner.Build([new ManiaHit(0, 0, 0, false, false)]);
        var transformed = PlayPlanTransforms.ApplyAll(plan, new[] { new IdentityManiaTransform() });
        Assert.Same(plan, transformed);
    }

    sealed class IdentityManiaTransform : IPlayPlanTransform<ManiaPlayPlan>
    {
        public ManiaPlayPlan Apply(ManiaPlayPlan plan) => plan;
    }
}
