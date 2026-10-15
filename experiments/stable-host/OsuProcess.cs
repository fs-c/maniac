using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StableHost;

static class OsuProcess
{
    public static Process RequireStable()
    {
        var matches = Process.GetProcesses()
            .Where(p => IsOsuName(p.ProcessName))
            .ToArray();
        if (matches.Length == 0)
            throw new InvalidOperationException("osu! is not running.");

        var candidates = matches.Where(IsWow64).ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException("osu! is running, but none of the processes are 32-bit (stable).");

        var withWindow = candidates.Where(p => p.MainWindowHandle != IntPtr.Zero).ToArray();
        if (withWindow.Length == 1)
            return withWindow[0];
        if (withWindow.Length > 1)
            throw new InvalidOperationException("multiple 32-bit osu! windows: " + string.Join(", ", withWindow.Select(p => p.Id)));

        return candidates.OrderByDescending(p => p.WorkingSet64).First();
    }

    static bool IsOsuName(string name) =>
        name.Equals("osu!", StringComparison.OrdinalIgnoreCase)
        || name.Equals("osu", StringComparison.OrdinalIgnoreCase);

    static bool IsWow64(Process process)
    {
        if (!Environment.Is64BitProcess)
            return true;
        if (!IsWow64Process(process.Handle, out bool wow64))
            throw new InvalidOperationException($"IsWow64Process failed for pid {process.Id}");
        return wow64;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool IsWow64Process(IntPtr process, out bool wow64);
}
