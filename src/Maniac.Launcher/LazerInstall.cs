using System.Diagnostics;

namespace Maniac.Launcher;

static class LazerInstall
{
    public static IEnumerable<string> CandidateDirectories()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "osulazer", "current");
        yield return Path.Combine(local, "osu!lazer", "current");
        yield return Path.Combine("/Applications", "osu!.app", "Contents", "MacOS");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Applications", "osu!.app", "Contents", "MacOS");
    }

    public static string? FindDirectory()
    {
        foreach (string dir in CandidateDirectories())
        {
            if (File.Exists(Path.Combine(dir, "osu.Game.dll")))
                return dir;
        }

        return null;
    }

    public static string? DirectoryFromProcess(Process process)
    {
        string path = ProcessImagePath.Query(process.Id);
        if (string.IsNullOrEmpty(path))
        {
            try { path = process.MainModule?.FileName ?? ""; }
            catch { path = ""; }
        }

        string? dir = Path.GetDirectoryName(path);
        return dir != null && File.Exists(Path.Combine(dir, "osu.Game.dll")) ? dir : null;
    }

    public static string? ReadVersion(string gameDirectory)
    {
        string deps = Path.Combine(gameDirectory, "osu!.deps.json");
        if (File.Exists(deps))
        {
            foreach (string line in File.ReadLines(deps))
            {
                int idx = line.IndexOf("ppy.osu.Game/", StringComparison.Ordinal);
                if (idx < 0)
                    continue;
                int start = idx + "ppy.osu.Game/".Length;
                int end = start;
                while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] is '.' or '-'))
                    end++;
                if (end > start)
                    return line[start..end];
            }
        }

        foreach (string name in new[] { "osu!.exe", "osu!" })
        {
            string exe = Path.Combine(gameDirectory, name);
            if (File.Exists(exe))
                return FileVersionInfo.GetVersionInfo(exe).ProductVersion;
        }

        return null;
    }
}
