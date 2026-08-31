using Maniac.Plugin;
using Xunit;

namespace Maniac.Compatibility.Tests;

public class StartupProbeTests
{
    [Fact]
    public void Required_game_members_resolve()
    {
        var probe = GameBindings.Probe();
        Assert.True(probe.Ok, string.Join(Environment.NewLine, probe.Failures));
        Assert.Contains(probe.Resolved, name => name.Contains("topMostOverlayContent", StringComparison.Ordinal));
        Assert.Contains(probe.Resolved, name => name.Contains("DrawableRuleset", StringComparison.Ordinal));
        Assert.Contains(probe.Resolved, name => name.Contains("UpdateFrame", StringComparison.Ordinal) || name.Contains("Root", StringComparison.Ordinal));
    }
}
