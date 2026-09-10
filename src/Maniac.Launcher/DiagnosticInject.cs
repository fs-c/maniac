using System.Diagnostics;
using Maniac.Core;
using Microsoft.Diagnostics.NETCore.Client;

namespace Maniac.Launcher;

static class DiagnosticInject
{
    public static void Apply(Process process, string hookDll, string command)
    {
        Cli.WriteVerbose("method    diagnostic ipc");
        int pid = process.Id;
        try
        {
            string path = Path.GetFullPath(hookDll);
            var client = new DiagnosticsClient(pid);
            client.SetEnvironmentVariable("MANIAC_COMMAND", command);
            client.ApplyStartupHook(path);
        }
        catch (ServerNotAvailableException ex)
        {
            throw new InjectFailedException(
                $"pid {pid} has no .NET diagnostic port. Is osu!lazer running, and is DOTNET_EnableDiagnostics enabled?",
                ex);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or DiagnosticsClientException)
        {
            throw new InjectFailedException($"diagnostic inject failed: {ex.Message}", ex);
        }

        Thread.Sleep(400);
        try
        {
            var still = Process.GetProcessById(pid);
            if (still.HasExited)
                throw new InjectFailedException($"osu! exited after inject. See {ManiacPaths.BootstrapLog}");
        }
        catch (ArgumentException)
        {
            throw new InjectFailedException($"osu! exited after inject. See {ManiacPaths.BootstrapLog}");
        }

        if (File.Exists(ManiacPaths.BootstrapLog))
            Cli.WriteVerbose($"details   {ManiacPaths.BootstrapLog}");
    }
}
