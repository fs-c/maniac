using Maniac.Core.Mania;
using Maniac.Core.Settings;
using Xunit;

namespace Maniac.Core.Tests;

public class ManiaHumanFieldsTests
{
    [Fact]
    public void Fields_cover_every_override()
    {
        var fields = ManiaHumanFields.All;
        Assert.Equal(13, fields.Length);

        var human = new ManiaHumanOverrides();
        foreach (var field in fields)
        {
            Assert.False(string.IsNullOrWhiteSpace(field.Group));
            Assert.False(string.IsNullOrWhiteSpace(field.Title));
            Assert.True(field.Max > field.Min);
            Assert.True(field.Step > 0);
            field.Assign(human, field.Min);
            Assert.Equal(field.Min, field.OverrideOf(human));
            field.Assign(human, null);
            Assert.Null(field.OverrideOf(human));
        }

        var resolved = ManiaHumanProfile.Auto(ManiaHumanLimits.SkillDefault);
        Assert.InRange(fields[0].Read(resolved), fields[0].Min, fields[0].Max);
    }
}
