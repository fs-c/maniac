using Maniac.Core;

namespace Maniac.Launcher;

sealed class PayloadMissingException : Exception
{
    public PayloadMissingException() : base(CliMessages.Incomplete) { }
}

sealed class InjectFailedException : Exception
{
    public InjectFailedException(string verboseDetail, Exception? inner = null)
        : base(CliMessages.InjectFailed, inner)
    {
        VerboseDetail = verboseDetail;
    }

    public string VerboseDetail { get; }
}

static class Cli
{
    public static bool Verbose { get; set; }

    public static void WriteVerbose(string line)
    {
        if (Verbose)
            Console.WriteLine(line);
    }
}

readonly record struct CliArgs(bool Help, bool Verbose, string Command, int? Pid)
{
    public static CliArgs Parse(string[] args)
    {
        bool help = false;
        bool verbose = false;
        string command = "reload";
        int? pid = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    help = true;
                    break;
                case "-v":
                case "--verbose":
                    verbose = true;
                    break;
                case "--unload":
                    command = "unload";
                    break;
                case "--reload":
                    command = "reload";
                    break;
                case "--pid":
                    if (i + 1 >= args.Length)
                        throw new InvalidOperationException(CliMessages.PidRequired);
                    pid = ParsePidValue(args[++i]);
                    break;
                default:
                    if (arg.StartsWith("--pid=", StringComparison.Ordinal))
                        pid = ParsePidValue(arg["--pid=".Length..]);
                    break;
            }
        }

        return new CliArgs(help, verbose, command, pid);
    }

    static int ParsePidValue(string raw)
    {
        if (!int.TryParse(raw, out int pid) || pid <= 0)
            throw new InvalidOperationException(CliMessages.PidRequired);
        return pid;
    }
}

static class CliMessages
{
    public const string Incomplete = "This copy of maniac is incomplete.";
    public const string OsuNotRunning = "osu!lazer is not running.";
    public const string MultipleOsu = "Several osu! instances are running. Close the extras, or pass --pid <id>.";
    public const string InjectFailed = "Could not load into osu!.";
    public const string PidRequired = "maniac --pid needs a process id.";
    public const string Help =
        """
        maniac - load into a running osu!lazer

        Usage:
          maniac              load or reload
          maniac --unload     remove
          maniac --pid <id>   pick a process
          maniac --verbose    extra detail
        """;

    public static string Success(string command, int pid) =>
        command == "unload"
            ? $"Removed from osu! (pid {pid})."
            : $"Loaded into osu! (pid {pid}).";

    public static string ProcessNotFound(int pid) => $"No process with id {pid}.";

    public static string Format(Exception ex, bool verbose, string? bootstrapLog = null)
    {
        bootstrapLog ??= ManiacPaths.BootstrapLog;
        string text = UserMessage(ex);
        if (verbose)
        {
            string extra = VerboseExtra(ex);
            if (extra.Length > 0 && extra != text)
                text += Environment.NewLine + extra;
        }

        if (NeedsLogHint(ex) && File.Exists(bootstrapLog))
            text += Environment.NewLine + "Details: " + bootstrapLog;

        return text;
    }

    static string UserMessage(Exception ex) => ex switch
    {
        PayloadMissingException => Incomplete,
        InjectFailedException => InjectFailed,
        FileNotFoundException => Incomplete,
        _ => ex.Message,
    };

    static string VerboseExtra(Exception ex) => ex switch
    {
        InjectFailedException inject => inject.VerboseDetail,
        FileNotFoundException missing => missing.FileName is { Length: > 0 } name
            ? name
            : missing.Message,
        PayloadMissingException => "",
        _ => ex.InnerException?.Message ?? "",
    };

    static bool NeedsLogHint(Exception ex) =>
        ex is InjectFailedException;
}
