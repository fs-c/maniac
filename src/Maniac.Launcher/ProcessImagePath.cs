namespace Maniac.Launcher;

static class ProcessImagePath
{
    public static string Query(int pid)
    {
#if WINDOWS
        return WindowsProcessImagePath.Query(pid);
#else
        return OperatingSystem.IsMacOS() ? MacProcessImagePath.Query(pid) : "";
#endif
    }
}
