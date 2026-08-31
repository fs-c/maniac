using Maniac.Core.Geometry;
using Maniac.Core.Osu;
using Xunit;

namespace Maniac.Core.Tests;

public class OsuPlannerTests
{
    [Fact]
    public void Alternates_on_close_notes()
    {
        var builder = new OsuPlanBuilder();
        builder.AddCircle(0, new Vec2(100, 100), 600);
        builder.AddCircle(100, new Vec2(120, 100), 600);
        builder.AddCircle(400, new Vec2(140, 100), 600);
        var plan = builder.Build();
        Assert.Equal(0, plan.Notes[0].Button);
        Assert.Equal(1, plan.Notes[1].Button);
        Assert.Equal(0, plan.Notes[2].Button);
    }

    [Fact]
    public void Slider_keeps_sampled_path()
    {
        var builder = new OsuPlanBuilder();
        builder.AddSlider(0, 200, new Vec2(0, 0), 600, [new Vec2(0, 0), new Vec2(50, 0), new Vec2(100, 0)]);
        var plan = builder.Build();
        Assert.Equal(OsuNoteKind.Slider, plan.Notes[0].Kind);
        Assert.Equal(3, plan.Notes[0].PathLength);
        Assert.Equal(100, plan.Notes[0].EndPos.X);
    }

    [Fact]
    public void Spinner_is_lasting()
    {
        var builder = new OsuPlanBuilder();
        builder.AddSpinner(0, 1000, Vec2.PlayfieldCentre, 600);
        var plan = builder.Build();
        Assert.True(plan.Notes[0].IsLasting);
        Assert.Equal(2, plan.ButtonEvents.Length);
        Assert.Equal(OsuPlanBuilder.KeyUpDelay + 1000, plan.ButtonEvents[1].Time);
    }

    [Fact]
    public void MoveStart_uses_preempt_minus_reaction()
    {
        var builder = new OsuPlanBuilder();
        builder.AddCircle(1000, new Vec2(10, 10), 600);
        var plan = builder.Build();
        Assert.Equal(500, plan.Notes[0].MoveStart);
    }
}
