using System.Diagnostics;
using System.Runtime.InteropServices;
using InjectDotnet;
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
            var process = FindOsu(pid);
            string pluginDir = Path.Combine(AppContext.BaseDirectory, "plugin");
            string dll = Path.Combine(pluginDir, "Maniac.dll");
            if (!File.Exists(dll))
                throw new FileNotFoundException("Maniac.dll not found next to the launcher. Build the plugin first.", dll);

            string runtimeConfig = Path.Combine(pluginDir, "Maniac.runtimeconfig.json");
            if (!File.Exists(runtimeConfig))
                throw new FileNotFoundException("Maniac.runtimeconfig.json not found. Build the plugin first.", runtimeConfig);

            string? install = LazerInstall.DirectoryFromProcess(process) ?? LazerInstall.FindDirectory();
            string? version = install == null ? null : LazerInstall.ReadVersion(install);

            Console.WriteLine($"pid {process.Id}");
            if (install != null)
                Console.WriteLine($"lazer   {install}");
            if (version != null)
                Console.WriteLine($"version {version}");
            Console.WriteLine($"injecting {dll}");
            Console.WriteLine($"runtime   {runtimeConfig}");
            Console.WriteLine($"command   {command}");

            int? result = process.Inject(
                runtimeConfig,
                dll,
                "Maniac.Entry, Maniac",
                "Bootstrap",
                argument: command,
                waitForReturn: true);

            if (result is not 0)
            {
                Console.Error.WriteLine($"bootstrap returned {DescribeHostResult(result)}");
                string log = ManiacPaths.BootstrapLog;
                if (File.Exists(log))
                    Console.Error.WriteLine($"details: {log}");
                return 1;
            }

            Console.WriteLine(command == "unload" ? "unload requested." : "reload requested.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static string DescribeHostResult(int? result)
    {
        if (result is null)
            return "null";

        int code = result.Value;
        string name = (uint)code switch
        {
            0x00000001 => "bootstrap threw (not hostfxr Success_HostAlreadyInitialized)",
            0x00000002 => "Success_DifferentRuntimeProperties",
            0x80008081 => "InvalidArgFailure",
            0x80008082 => "CoreHostLibLoadFailure",
            0x80008083 => "CoreHostLibMissingFailure",
            0x80008084 => "CoreHostEntryPointFailure",
            0x80008093 => "InvalidConfigFile (self-contained runtimeconfig cannot be used for component load)",
            0x80008096 => "FrameworkMissingFailure",
            0x800080a5 => "HostIncompatibleConfig (component frameworks incompatible with the loaded runtime)",
            0xC0000005 => "STATUS_ACCESS_VIOLATION (entry point signature must be int Method(IntPtr, int))",
            _ => "unknown",
        };
        return $"{code} (0x{code:X8}) {name}";
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

    static Process FindOsu(int? pid)
    {
        if (pid is int requested)
            return Process.GetProcessById(requested);

        var windowPids = FindOsuWindowPids();
        if (windowPids.Count == 1)
            return Process.GetProcessById(windowPids[0]);
        if (windowPids.Count > 1)
            throw new InvalidOperationException($"Multiple osu! windows: pids {string.Join(", ", windowPids)}. Pass --pid.");

        var matches = Process.GetProcesses()
            .Where(p =>
            {
                try
                {
                    return p.ProcessName.Equals("osu!", StringComparison.OrdinalIgnoreCase)
                        || p.ProcessName.Equals("osu", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            })
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException("No osu! window or process found. Start osu!lazer and retry.");
        if (matches.Count > 1)
            throw new InvalidOperationException($"Multiple osu! processes: {string.Join(", ", matches.Select(p => p.Id))}. Pass --pid.");

        return matches[0];
    }

    static List<int> FindOsuWindowPids()
    {
        var pids = new HashSet<int>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;
            GetWindowThreadProcessId(hwnd, out uint windowPid);
            if (windowPid == 0)
                return true;
            try
            {
                using var process = Process.GetProcessById((int)windowPid);
                if (process.ProcessName.Equals("osu!", StringComparison.OrdinalIgnoreCase)
                    || process.ProcessName.Equals("osu", StringComparison.OrdinalIgnoreCase))
                    pids.Add((int)windowPid);
            }
            catch { /* exited */ }

            return true;
        }, IntPtr.Zero);
        return pids.OrderBy(id => id).ToList();
    }

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc callback, IntPtr extra);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr extra);
}
