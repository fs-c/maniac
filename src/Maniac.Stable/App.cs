using System.Windows;
using Maniac.Core;
using Maniac.Core.Settings;

namespace Maniac.Stable;

sealed class App : Application
{
    public static SettingsStore Settings { get; private set; } = null!;
    public static StableSession Session { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Hide() must not exit. The window hotkey hides the window while the play thread keeps running.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Settings = new SettingsStore(ManiacPaths.StableSettingsFile);
        if (BuildExpiry.Current.IsExpired())
        {
            Settings.Update(s => s.AutoplayEnabled = false);
            Settings.Flush();
        }

        Session = new StableSession(Settings);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        Hotkeys.Start(window);
        base.OnStartup(e);
    }
}
