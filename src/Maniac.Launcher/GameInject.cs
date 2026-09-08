using System.Diagnostics;

namespace Maniac.Launcher;

/// <summary>
/// Chooses how to load Maniac into a running osu! process.
/// InjectDotnet is a Windows x64 native helper; every other OS/arch uses diagnostic IPC.
/// </summary>
static class GameInject
{
    public static void Apply(Process process, string pluginDir, string dll, string command)
    {
#if USE_INJECTDOTNET
        WindowsRemoteInject.Apply(process, pluginDir, dll, command);
#else
        DiagnosticInject.Apply(process, dll, command);
#endif
    }
}
