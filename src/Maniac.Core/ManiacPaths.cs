namespace Maniac.Core;

public static class ManiacPaths
{
    public static string LogDirectory => Path.Combine(Path.GetTempPath(), "maniac-lazer");
    public static string BootstrapLog => Path.Combine(LogDirectory, "bootstrap.log");

    public static string PayloadDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "maniac-lazer",
        "payload");

    public static string StableSettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "maniac-stable");

    public static string StableSettingsFile => Path.Combine(StableSettingsDirectory, "settings.json");
}
