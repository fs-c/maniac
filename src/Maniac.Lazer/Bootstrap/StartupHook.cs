using Maniac;

/// <summary>
/// Diagnostic IPC / DOTNET_STARTUP_HOOKS entry. Must stay in the global
/// namespace with this type name and signature.
/// Command is MANIAC_COMMAND in the target process ("reload" or "unload").
/// </summary>
public static class StartupHook
{
    public static void Initialize()
    {
        try
        {
            Session.Log("startup hook");
            Session.SetSourceDir(Path.GetDirectoryName(typeof(StartupHook).Assembly.Location) ?? "");
            string command = Environment.GetEnvironmentVariable("MANIAC_COMMAND") ?? "reload";
            Session.Run(command, InjectMethod.StartupHook);
        }
        catch (Exception ex)
        {
            Session.Log(ex);
        }
    }
}
