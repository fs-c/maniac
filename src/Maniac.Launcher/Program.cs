using System.Diagnostics;
using Maniac.Core;

namespace Maniac.Launcher;

static class Program
{
    static int Main(string[] args)
    {
        try
        {
            string command = ParseCommand(args);
            if (command != "unload" && BuildExpiry.Current.IsExpired())
            {
                Console.Error.WriteLine(BuildExpiry.Current.Describe(DateTimeOffset.UtcNow));
                return 1;
            }

            int? pid = ParsePid(args);
            var process = OsuProcess.Find(pid);
            string pluginDir = Path.Combine(AppContext.BaseDirectory, "plugin");
            string dll = Path.Combine(pluginDir, "Maniac.dll");
            if (!File.Exists(dll))
                throw new FileNotFoundException("Maniac.dll not found next to the launcher. Build the plugin first.", dll);

            string? install = LazerInstall.DirectoryFromProcess(process) ?? LazerInstall.FindDirectory();
            string? version = install == null ? null : LazerInstall.ReadVersion(install);

            Console.WriteLine($"pid {process.Id}");
            if (install != null)
                Console.WriteLine($"lazer   {install}");
            if (version != null)
                Console.WriteLine($"version {version}");
            Console.WriteLine($"injecting {dll}");
            Console.WriteLine($"command   {command}");

            GameInject.Apply(process, pluginDir, dll, command);

            Console.WriteLine(command == "unload" ? "unload requested." : "reload requested.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static string ParseCommand(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg is "--help" or "-h")
            {
                Console.WriteLine("maniac [--reload] [--unload] [--pid <id>]");
                Environment.Exit(0);
            }
            if (arg is "--unload")
                return "unload";
            if (arg is "--reload")
                return "reload";
        }

        return "reload";
    }

    static int? ParsePid(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--pid" && i + 1 < args.Length)
                return int.Parse(args[++i]);
            if (args[i].StartsWith("--pid=", StringComparison.Ordinal))
                return int.Parse(args[i]["--pid=".Length..]);
        }

        return null;
    }
}
