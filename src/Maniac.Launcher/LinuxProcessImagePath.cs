using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Maniac.Launcher;

[SupportedOSPlatform("linux")]
static class LinuxProcessImagePath
{
    public static string Query(int pid)
    {
        var buffer = new byte[4096];
        int written = readlink($"/proc/{pid}/exe", buffer, buffer.Length);
        return written > 0 ? Encoding.UTF8.GetString(buffer, 0, written) : "";
    }

    [DllImport("libc", SetLastError = true)]
    static extern int readlink(string path, byte[] buf, int bufsiz);
}
