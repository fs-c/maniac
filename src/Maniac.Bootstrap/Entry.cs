using System.Runtime.InteropServices;

namespace Maniac;

public static class Entry
{
    /// <summary>
    /// InjectDotnet entry point. Signature must stay (IntPtr, int).
    /// Argument is UTF-16: "reload" (default) or "unload".
    /// </summary>
    public static int Bootstrap(IntPtr argument, int size)
    {
        try
        {
            Session.SetSourceDir(Path.GetDirectoryName(typeof(Entry).Assembly.Location) ?? "");
            Session.Run(ReadArg(argument, size), InjectMethod.RemoteThread);
            return 0;
        }
        catch (Exception ex)
        {
            Session.Log(ex);
            return 1;
        }
    }

    static string ReadArg(IntPtr argument, int size)
    {
        if (argument == IntPtr.Zero || size < 2)
            return "reload";
        return Marshal.PtrToStringUni(argument, size / 2)?.TrimEnd('\0').Trim() ?? "reload";
    }
}
