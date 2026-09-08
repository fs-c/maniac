using System.Diagnostics;

namespace Maniac.Launcher;

static class OsuProcess
{
    public static Process Find(int? pid)
    {
        if (pid is int requested)
            return Process.GetProcessById(requested);

        var windowPids = FindWindowPids();
        if (windowPids.Count == 1)
            return Process.GetProcessById(windowPids[0]);
        if (windowPids.Count > 1)
            throw new InvalidOperationException($"Multiple osu! windows: pids {string.Join(", ", windowPids)}. Pass --pid.");

        var matches = Process.GetProcesses()
            .Where(IsOsuProcess)
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException("No osu! window or process found. Start osu!lazer and retry.");
        if (matches.Count > 1)
            throw new InvalidOperationException($"Multiple osu! processes: {string.Join(", ", matches.Select(p => p.Id))}. Pass --pid.");

        return matches[0];
    }

    public static bool IsOsuProcess(Process process)
    {
        try
        {
            return process.ProcessName.Equals("osu!", StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Equals("osu", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    static IReadOnlyList<int> FindWindowPids()
    {
#if WINDOWS
        return WindowsOsuWindows.FindPids();
#else
        return [];
#endif
    }
}
