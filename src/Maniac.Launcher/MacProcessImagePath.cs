using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Maniac.Launcher;

[SupportedOSPlatform("macos")]
static class MacProcessImagePath
{
    public static string Query(int pid)
    {
        var buffer = new StringBuilder(4096);
        int written = proc_pidpath(pid, buffer, (uint)buffer.Capacity);
        return written > 0 ? buffer.ToString() : "";
    }

    [DllImport("libproc")]
    static extern int proc_pidpath(int pid, StringBuilder buffer, uint buffersize);
}
