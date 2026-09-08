using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Maniac.Launcher;

static class WindowsOsuWindows
{
    public static IReadOnlyList<int> FindPids()
    {
        var pids = new HashSet<int>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;
            GetWindowThreadProcessId(hwnd, out uint windowPid);
            if (windowPid == 0)
                return true;
            try
            {
                using var process = Process.GetProcessById((int)windowPid);
                if (OsuProcess.IsOsuProcess(process))
                    pids.Add((int)windowPid);
            }
            catch { /* exited */ }

            return true;
        }, IntPtr.Zero);
        return pids.OrderBy(id => id).ToList();
    }

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc callback, IntPtr extra);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr extra);
}
