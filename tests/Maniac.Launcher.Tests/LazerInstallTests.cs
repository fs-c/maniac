using Maniac.Launcher;
using Xunit;

namespace Maniac.Launcher.Tests;

public class LazerInstallTests
{
    [Fact]
    public void Candidate_directories_include_desktop_installs()
    {
        var dirs = LazerInstall.CandidateDirectories()
            .Select(d => d.Replace('\\', '/'))
            .ToList();

        Assert.Contains(dirs, d => d.Contains("/osulazer/current", StringComparison.Ordinal)
            || d.Contains("/osu!lazer/current", StringComparison.Ordinal));
        Assert.Contains(dirs, d => d.EndsWith("osu!.app/Contents/MacOS", StringComparison.Ordinal));
    }

    [Fact]
    public void Process_image_path_for_self_is_rooted_when_available()
    {
        string path = ProcessImagePath.Query(Environment.ProcessId);
        if (path.Length == 0)
            return;
        Assert.True(Path.IsPathRooted(path), path);
    }
}
