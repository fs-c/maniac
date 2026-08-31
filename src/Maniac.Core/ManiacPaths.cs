namespace Maniac.Core;

public static class ManiacPaths
{
    public static string LogDirectory => Path.Combine(Path.GetTempPath(), "maniac-lazer");
    public static string BootstrapLog => Path.Combine(LogDirectory, "bootstrap.log");
}
