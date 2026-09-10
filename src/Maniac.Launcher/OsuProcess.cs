using System.Diagnostics;

namespace Maniac.Launcher;

static class OsuProcess
{
    public static Process Find(int? pid)
    {
        if (pid is int requested)
        {
            try
            {
                return Process.GetProcessById(requested);
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException(CliMessages.ProcessNotFound(requested));
            }
        }

        var windowPids = FindWindowPids();
        if (windowPids.Count == 1)
            return Process.GetProcessById(windowPids[0]);
        if (windowPids.Count > 1)
        {
            Cli.WriteVerbose($"pids {string.Join(", ", windowPids)}");
            throw new InvalidOperationException(CliMessages.MultipleOsu);
        }

        var matches = Process.GetProcesses()
            .Where(IsOsuProcess)
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException(CliMessages.OsuNotRunning);
        if (matches.Count > 1)
        {
            Cli.WriteVerbose($"pids {string.Join(", ", matches.Select(p => p.Id))}");
            throw new InvalidOperationException(CliMessages.MultipleOsu);
        }

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
