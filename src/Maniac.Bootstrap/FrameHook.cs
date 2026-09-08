using System.Runtime.CompilerServices;
using osu.Framework.Platform;

namespace Maniac;

enum InjectMethod
{
    /// <summary>InjectDotnet remote thread. Native detours are expected to work.</summary>
    RemoteThread,
    /// <summary>Diagnostic IPC / DOTNET_STARTUP_HOOKS. No native rewrite.</summary>
    StartupHook,
}

/// <summary>
/// Chooses how to run a callback on the game update thread, based on how we
/// entered the process — not OS. Remote-thread inject pairs with a Harmony
/// UpdateFrame postfix; startup-hook inject pairs with a managed wrap because
/// that path is used where unsigned native helpers cannot load.
/// </summary>
static class FrameHook
{
    public static void Attach(Action<GameHost> onFrame, InjectMethod method)
    {
        if (method == InjectMethod.StartupHook)
        {
            Session.Log("using managed pump");
            ManagedFrameHook.Attach(onFrame);
            return;
        }

        TryHarmonyThenManaged(onFrame);
    }

    public static void Stop() => ManagedFrameHook.Stop();

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void TryHarmonyThenManaged(Action<GameHost> onFrame)
    {
        if (HarmonyFrameHook.TryAttach(onFrame))
            return;
        Session.Log("using managed pump");
        ManagedFrameHook.Attach(onFrame);
    }
}
