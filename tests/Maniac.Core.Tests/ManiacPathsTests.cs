using Maniac.Core;
using Xunit;

namespace Maniac.Core.Tests;

public class ManiacPathsTests
{
    [Fact]
    public void Payload_directory_is_under_local_app_data()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "maniac-lazer",
            "payload");
        Assert.Equal(expected, ManiacPaths.PayloadDirectory);
    }
}
