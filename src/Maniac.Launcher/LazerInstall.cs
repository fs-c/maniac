using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Maniac.Launcher;

static class LazerInstall
{
    public static IEnumerable<string> CandidateDirectories()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "osulazer", "current");
        yield return Path.Combine(local, "osu!lazer", "current");
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
        string path = QueryImagePath(process.Id);
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

        string exe = Path.Combine(gameDirectory, "osu!.exe");
        if (File.Exists(exe))
            return FileVersionInfo.GetVersionInfo(exe).ProductVersion;
        return null;
    }

    static string QueryImagePath(int pid)
    {
        var handle = OpenProcess(0x1000, false, pid);
        if (handle == IntPtr.Zero)
            return "";
        try
        {
            var buffer = new StringBuilder(32768);
            int size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : "";
        }
        finally { CloseHandle(handle); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(int access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder path, ref int size);
}
