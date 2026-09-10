using Maniac.Core.Status;
using Xunit;

namespace Maniac.Core.Tests;

public class OverlayStatusTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("no screen", "no screen")]
    [InlineData("MainMenu", "menu")]
    [InlineData("SoloSongSelect", "song select")]
    [InlineData("PlaylistsSongSelect", "playlists")]
    [InlineData("MultiplayerMatchSongSelect", "multiplayer")]
    [InlineData("PlayerLoader", "loading")]
    [InlineData("MultiplayerPlayerLoader", "loading")]
    [InlineData("SoloPlayer", "playing")]
    [InlineData("MultiplayerPlayer", "playing")]
    [InlineData("ReplayPlayer", "replay")]
    [InlineData("SoloSpectatorPlayer", "spectating")]
    [InlineData("EditorPlayer", "testing")]
    [InlineData("SoloResultsScreen", "results")]
    [InlineData("Editor", "editor")]
    [InlineData("DailyChallenge", "daily")]
    [InlineData("ScreenQueue", "queue")]
    [InlineData("BeatmapSubmissionScreen", "beatmap submission")]
    public void Screen_label(string? typeName, string expected) =>
        Assert.Equal(expected, OverlayStatus.ScreenLabel(typeName));

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(999, "0:00")]
    [InlineData(1000, "0:01")]
    [InlineData(23_000, "0:23")]
    [InlineData(83_400, "1:23")]
    [InlineData(3_600_000, "1:00:00")]
    [InlineData(-1500, "-0:01")]
    public void Clock(double timeMs, string expected) =>
        Assert.Equal(expected, OverlayStatus.FormatClock(timeMs));
}
