using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Maniac.Stable;

static class OsuProcess
{
    public static bool TryFind(out Process? process, out string? error)
    {
        process = null;
        error = null;
        Process[] matches = Process.GetProcesses()
            .Where(p => IsOsuName(p.ProcessName))
            .ToArray();
        try
        {
            if (matches.Length == 0)
                return false;

            var candidates = matches.Where(IsWow64).ToArray();
            if (candidates.Length == 0)
            {
                error = "osu! is running, but none of the processes are 32-bit (stable).";
                return false;
            }

            var withWindow = candidates.Where(p => p.MainWindowHandle != IntPtr.Zero).ToArray();
            if (withWindow.Length > 1)
            {
                error = "multiple 32-bit osu! windows: " + string.Join(", ", withWindow.Select(p => p.Id));
                return false;
            }

            process = withWindow.Length == 1
                ? withWindow[0]
                : candidates.OrderByDescending(p => p.WorkingSet64).First();
            return true;
        }
        finally
        {
            foreach (var p in matches)
            {
                if (!ReferenceEquals(p, process))
                    p.Dispose();
            }
        }
    }

    public static bool IsForeground(Process process)
    {
        nint hwnd = process.MainWindowHandle;
        return hwnd != IntPtr.Zero && GetForegroundWindow() == hwnd;
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

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
}
