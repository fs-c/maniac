namespace Maniac.Launcher;

static class ProcessImagePath
{
    public static string Query(int pid)
    {
#if WINDOWS
        return WindowsProcessImagePath.Query(pid);
#else
        if (OperatingSystem.IsMacOS())
            return MacProcessImagePath.Query(pid);
        if (OperatingSystem.IsLinux())
            return LinuxProcessImagePath.Query(pid);
        return "";
#endif
    }
}
