using System.Diagnostics;
using InjectDotnet;
using Maniac.Core;

namespace Maniac.Launcher;

static class WindowsRemoteInject
{
    public static void Apply(Process process, string pluginDir, string dll, string command)
    {
        string runtimeConfig = Path.Combine(pluginDir, "Maniac.runtimeconfig.json");
        if (!File.Exists(runtimeConfig))
            throw new FileNotFoundException("Maniac.runtimeconfig.json not found. Build the plugin first.", runtimeConfig);

        Console.WriteLine($"runtime   {runtimeConfig}");
        Console.WriteLine("method    injectdotnet");

        int? result = process.Inject(
            runtimeConfig,
            dll,
            "Maniac.Entry, Maniac",
            "Bootstrap",
            argument: command,
            waitForReturn: true);

        if (result is not 0)
        {
            string log = ManiacPaths.BootstrapLog;
            string details = File.Exists(log) ? $" details: {log}" : "";
            throw new InvalidOperationException($"bootstrap returned {DescribeHostResult(result)}.{details}");
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
}
