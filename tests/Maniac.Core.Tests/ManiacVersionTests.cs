using Maniac.Core;
using Xunit;

namespace Maniac.Core.Tests;

public class ManiacVersionTests
{
    [Theory]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("0.1.0+abc123", "0.1.0")]
    [InlineData("1.2.3-beta.1+deadbeef", "1.2.3-beta.1")]
    public void Display_strips_source_revision(string informational, string expected) =>
        Assert.Equal(expected, ManiacVersion.Display(informational));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+abc")]
    public void Display_rejects_empty_version(string informational) =>
        Assert.Throws<InvalidOperationException>(() => ManiacVersion.Display(informational));

    [Fact]
    public void Current_is_the_product_version()
    {
        Assert.Equal("0.1.0", ManiacVersion.Current);
        Assert.Equal(ManiacVersion.FromAssembly(typeof(ManiacVersion).Assembly), ManiacVersion.Current);
    }
}
