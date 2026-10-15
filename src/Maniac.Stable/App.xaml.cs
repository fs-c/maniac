using Maniac.Core;
using Maniac.Core.Settings;
using Microsoft.UI.Xaml;

namespace Maniac.Stable;

public partial class App : Application
{
    public static SettingsStore Settings { get; private set; } = null!;
    public static StableSession Session { get; private set; } = null!;

    Window? window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Settings = new SettingsStore(ManiacPaths.StableSettingsFile);
        if (BuildExpiry.Current.IsExpired())
        {
            Settings.Update(s => s.AutoplayEnabled = false);
            Settings.Flush();
        }

        Session = new StableSession(Settings);
        window = new MainWindow();
        window.Closed += (_, _) =>
        {
            Session.Dispose();
            Settings.Flush();
        };
        window.Activate();
    }
}
