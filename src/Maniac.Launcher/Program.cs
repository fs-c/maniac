using System.Diagnostics;
using Maniac.Core;

namespace Maniac.Launcher;

static class Program
{
    static int Main(string[] args)
    {
        CliArgs cli;
        try
        {
            cli = CliArgs.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(CliMessages.Format(ex, verbose: false));
            return 1;
        }

        if (cli.Help)
        {
            Console.WriteLine(CliMessages.Help);
            return 0;
        }

        Cli.Verbose = cli.Verbose;

        try
        {
            if (cli.Command != "unload" && BuildExpiry.Current.IsExpired())
            {
                Console.Error.WriteLine(BuildExpiry.Current.Describe(DateTimeOffset.UtcNow));
                return 1;
            }

            var process = OsuProcess.Find(cli.Pid);
            string pluginDir = PluginPayload.Resolve(AppContext.BaseDirectory, typeof(Program).Assembly);
            string dll = Path.Combine(pluginDir, "Maniac.dll");

            string? install = LazerInstall.DirectoryFromProcess(process) ?? LazerInstall.FindDirectory();
            string? version = install == null ? null : LazerInstall.ReadVersion(install);

            Cli.WriteVerbose($"pid {process.Id} ({process.ProcessName})");
            if (install != null)
                Cli.WriteVerbose($"lazer     {install}");
            if (version != null)
                Cli.WriteVerbose($"version   {version}");
            Cli.WriteVerbose($"plugin    {pluginDir}");
            Cli.WriteVerbose($"command   {cli.Command}");

            GameInject.Apply(process, pluginDir, dll, cli.Command);
            Console.WriteLine(CliMessages.Success(cli.Command, process.Id));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(CliMessages.Format(ex, cli.Verbose));
            return 1;
        }
    }
}
