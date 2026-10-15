using Maniac.Core;
using Maniac.Core.Status;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Maniac.Stable;

public sealed partial class GeneralPage : Page
{
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    bool suppress;

    public GeneralPage()
    {
        InitializeComponent();
        timer.Tick += (_, _) => Refresh();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();
        timer.Start();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        timer.Stop();
        base.OnNavigatedFrom(e);
    }

    void AutoplayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (suppress)
            return;
        if (BuildExpiry.Current.IsExpired())
            return;
        App.Settings.Update(s => s.AutoplayEnabled = AutoplayToggle.IsOn);
    }

    void Refresh()
    {
        var expiry = BuildExpiry.Current;
        bool expired = expiry.IsExpired();
        ExpiryBar.IsOpen = expiry.HasEndTime || expiry.StampInvalid;
        ExpiryBar.Message = expiry.Describe(DateTimeOffset.UtcNow);
        ExpiryBar.Severity = expired ? InfoBarSeverity.Error : InfoBarSeverity.Warning;

        suppress = true;
        AutoplayToggle.IsEnabled = !expired;
        AutoplayToggle.IsOn = App.Settings.Current.AutoplayEnabled && !expired;
        suppress = false;

        var snap = App.Session.Snapshot();
        ProcessText.Text = snap.Attached ? $"osu!stable pid {snap.Pid}" : "not attached";
        LayoutText.Text = snap.LayoutOk ? "matched" : "not matched";
        RulesetText.Text = string.IsNullOrEmpty(snap.Ruleset) ? "—" : snap.Ruleset;
        ScreenText.Text = string.IsNullOrEmpty(snap.Screen) ? "—" : snap.Screen;
        TimeText.Text = OverlayStatus.FormatClock(snap.Time);
        ProgressText.Text = snap.Total == 0 ? "—" : $"{snap.Applied}/{snap.Total}";
        ColumnsText.Text = snap.Columns > 0 ? $"{snap.Columns}K" : "—";
        PausedText.Text = snap.Paused ? "yes" : "no";
        ForegroundText.Text = snap.Foreground ? "osu! is foreground" : "input withheld";
        PlayingText.Text = snap.Playing ? "sending input" : "idle";
        KeysText.Text = string.IsNullOrEmpty(snap.Keys) ? "—" : snap.Keys;
        VersionText.Text = ManiacVersion.Current;
        SettingsText.Text = App.Settings.FilePath;
        StatusSummary.Text = Summary(snap);

        bool error = !string.IsNullOrEmpty(snap.Diagnostic) && !snap.Diagnostic.StartsWith("autoplay", StringComparison.Ordinal)
                     && snap.Diagnostic != "mania driver off"
                     && snap.Diagnostic != "paused"
                     && !snap.Diagnostic.StartsWith("osu! is not", StringComparison.Ordinal)
                     && snap.Diagnostic != "waiting for osu!stable"
                     && snap.Diagnostic != "unsupported ruleset";
        DiagBar.IsOpen = !string.IsNullOrEmpty(snap.Diagnostic);
        DiagBar.Message = string.IsNullOrEmpty(snap.Diagnostic) ? "ok" : snap.Diagnostic;
        DiagBar.Severity = error ? InfoBarSeverity.Error
            : snap.Playing ? InfoBarSeverity.Success
            : InfoBarSeverity.Informational;
    }

    static string Summary(StablePlayStatus snap)
    {
        if (!string.IsNullOrEmpty(snap.Diagnostic))
            return snap.Diagnostic;
        if (!snap.Attached)
            return "not attached";
        return snap.Playing ? "sending input" : "idle";
    }
}
