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

    [Fact]
    public void Stable_settings_file_is_under_app_data()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "maniac-stable",
            "settings.json");
        Assert.Equal(expected, ManiacPaths.StableSettingsFile);
    }
}
