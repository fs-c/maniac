using System.Text;

namespace Maniac.Core.Status;

public static class OverlayStatus
{
    public static string ScreenLabel(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "";
        if (typeName == "no screen")
            return "no screen";

        if (typeName.EndsWith("PlayerLoader", StringComparison.Ordinal))
            return "loading";
        if (typeName.Contains("Spectator", StringComparison.Ordinal))
            return "spectating";
        if (typeName.Contains("ReplayPlayer", StringComparison.Ordinal))
            return "replay";
        if (typeName == "EditorPlayer")
            return "testing";
        if (typeName.EndsWith("Player", StringComparison.Ordinal))
            return "playing";
        if (typeName.Contains("Results", StringComparison.Ordinal))
            return "results";
        if (typeName.Contains("Playlists", StringComparison.Ordinal))
            return "playlists";
        if (typeName.Contains("Multiplayer", StringComparison.Ordinal))
            return "multiplayer";
        if (typeName.Contains("SongSelect", StringComparison.Ordinal))
            return "song select";
        if (typeName is "Editor" or "EditorLoader")
            return "editor";
        if (typeName == "MainMenu")
            return "menu";
        if (typeName.Contains("DailyChallenge", StringComparison.Ordinal))
            return "daily";

        return Humanize(typeName);
    }

    public static string FormatClock(double timeMs)
    {
        bool neg = timeMs < 0;
        long total = (long)Math.Abs(timeMs) / 1000;
        long hours = total / 3600;
        int minutes = (int)(total / 60 % 60);
        int seconds = (int)(total % 60);
        string body = hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
        return neg ? "-" + body : body;
    }

    static string Humanize(string typeName)
    {
        string name = typeName;
        if (name.EndsWith("Screen", StringComparison.Ordinal) && name.Length > 6)
            name = name[..^6];
        if (name.StartsWith("Screen", StringComparison.Ordinal) && name.Length > 6 && char.IsUpper(name[6]))
            name = name[6..];

        var sb = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                sb.Append(' ');
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
