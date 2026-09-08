using System.Diagnostics;
using Maniac.Core;
using Microsoft.Diagnostics.NETCore.Client;

namespace Maniac.Launcher;

static class DiagnosticInject
{
    public static void Apply(Process process, string hookDll, string command)
    {
        Console.WriteLine("method   diagnostic ipc");
        int pid = process.Id;
        try
        {
            string path = Path.GetFullPath(hookDll);
            var client = new DiagnosticsClient(pid);
            client.SetEnvironmentVariable("MANIAC_COMMAND", command);
            client.ApplyStartupHook(path);
        }
        catch (ServerNotAvailableException)
        {
            throw new InvalidOperationException(
                $"pid {pid} has no .NET diagnostic port. Is osu!lazer running, and is DOTNET_EnableDiagnostics enabled?");
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or DiagnosticsClientException)
        {
            string hint = File.Exists(ManiacPaths.BootstrapLog) ? $" See {ManiacPaths.BootstrapLog}" : "";
            throw new InvalidOperationException($"diagnostic inject failed: {ex.Message}.{hint}", ex);
        }

        Thread.Sleep(400);
        try
        {
            var still = Process.GetProcessById(pid);
            if (still.HasExited)
                throw new InvalidOperationException($"osu! exited after inject. See {ManiacPaths.BootstrapLog}");
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException($"osu! exited after inject. See {ManiacPaths.BootstrapLog}");
        }

        if (File.Exists(ManiacPaths.BootstrapLog))
            Console.WriteLine($"details: {ManiacPaths.BootstrapLog}");
    }
}
