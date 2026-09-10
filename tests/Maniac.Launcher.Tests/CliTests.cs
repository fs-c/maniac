using Maniac.Launcher;
using Xunit;

namespace Maniac.Launcher.Tests;

public class CliTests
{
    [Fact]
    public void Parse_help_and_verbose_flags()
    {
        var help = CliArgs.Parse(["--help"]);
        Assert.True(help.Help);
        Assert.Equal("reload", help.Command);

        var shortHelp = CliArgs.Parse(["-h"]);
        Assert.True(shortHelp.Help);

        var verbose = CliArgs.Parse(["-v", "--unload"]);
        Assert.True(verbose.Verbose);
        Assert.Equal("unload", verbose.Command);
        Assert.False(verbose.Help);
    }

    [Fact]
    public void Parse_pid_forms()
    {
        Assert.Equal(1234, CliArgs.Parse(["--pid", "1234"]).Pid);
        Assert.Equal(99, CliArgs.Parse(["--pid=99"]).Pid);
        var ex = Assert.Throws<InvalidOperationException>(() => CliArgs.Parse(["--pid"]));
        Assert.Equal(CliMessages.PidRequired, ex.Message);
        Assert.Throws<InvalidOperationException>(() => CliArgs.Parse(["--pid", "nope"]));
        Assert.Throws<InvalidOperationException>(() => CliArgs.Parse(["--pid=0"]));
    }

    [Fact]
    public void Default_command_is_reload()
    {
        var parsed = CliArgs.Parse([]);
        Assert.Equal("reload", parsed.Command);
        Assert.False(parsed.Verbose);
        Assert.Null(parsed.Pid);
    }

    [Fact]
    public void Success_and_help_copy()
    {
        Assert.Equal("Loaded into osu! (pid 7).", CliMessages.Success("reload", 7));
        Assert.Equal("Removed from osu! (pid 7).", CliMessages.Success("unload", 7));
        Assert.Contains("maniac --unload", CliMessages.Help);
        Assert.DoesNotContain("build the plugin", CliMessages.Help, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_maps_missing_payload_without_build_instructions()
    {
        string text = CliMessages.Format(new PayloadMissingException(), verbose: false, bootstrapLog: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Equal(CliMessages.Incomplete, text);
        Assert.DoesNotContain("build", text, StringComparison.OrdinalIgnoreCase);

        var missing = new FileNotFoundException("Maniac.dll not found. Build the plugin first.", "Maniac.dll");
        Assert.Equal(CliMessages.Incomplete, CliMessages.Format(missing, verbose: false, bootstrapLog: "no-log"));
        string verbose = CliMessages.Format(missing, verbose: true, bootstrapLog: "no-log");
        Assert.Contains(CliMessages.Incomplete, verbose);
        Assert.Contains("Maniac.dll", verbose);
    }

    [Fact]
    public void Format_hides_hostfxr_unless_verbose()
    {
        var ex = new InjectFailedException("bootstrap returned 1 (0x00000001) bootstrap threw");
        string quiet = CliMessages.Format(ex, verbose: false, bootstrapLog: "missing.log");
        Assert.Equal(CliMessages.InjectFailed, quiet);
        Assert.DoesNotContain("0x", quiet);

        string verbose = CliMessages.Format(ex, verbose: true, bootstrapLog: "missing.log");
        Assert.Contains(CliMessages.InjectFailed, verbose);
        Assert.Contains("0x00000001", verbose);
    }

    [Fact]
    public void Format_appends_bootstrap_log_when_present()
    {
        string log = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N") + ".log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.WriteAllText(log, "detail");
        try
        {
            string text = CliMessages.Format(new InjectFailedException("hex"), verbose: false, bootstrapLog: log);
            Assert.Contains(CliMessages.InjectFailed, text);
            Assert.Contains("Details: " + log, text);
            Assert.DoesNotContain("hex", text);
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public void Format_passes_through_osu_errors()
    {
        Assert.Equal(
            CliMessages.OsuNotRunning,
            CliMessages.Format(new InvalidOperationException(CliMessages.OsuNotRunning), verbose: false, bootstrapLog: "x"));
        Assert.Equal(
            CliMessages.MultipleOsu,
            CliMessages.Format(new InvalidOperationException(CliMessages.MultipleOsu), verbose: false, bootstrapLog: "x"));
    }
}
