using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using Maniac.Core;
using Maniac.Core.Settings;
using Maniac.Core.Status;

namespace Maniac.Stable;

sealed class GeneralView : StackPanel
{
    readonly Border expiryBox;
    readonly TextBlock expiryText = new() { TextWrapping = TextWrapping.Wrap };
    readonly CheckBox autoplay = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly KeybindRow panelKey;
    readonly KeybindRow autoKey;
    readonly TextBlock summary = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock process = Value();
    readonly TextBlock layout = Value();
    readonly TextBlock ruleset = Value();
    readonly TextBlock screen = Value();
    readonly TextBlock time = Value();
    readonly TextBlock progress = Value();
    readonly TextBlock columns = Value();
    readonly TextBlock paused = Value();
    readonly TextBlock foreground = Value();
    readonly TextBlock playing = Value();
    readonly TextBlock keys = Value();
    readonly TextBlock settingsPath = Value();
    bool suppress;

    public GeneralView(Action<KeybindRow> beginCapture)
    {
        Margin = new Thickness(32, 28, 32, 36);

        panelKey = new KeybindRow(beginCapture, (s, name) => s.PanelHotkey = name);
        autoKey = new KeybindRow(beginCapture, (s, name) => s.AutoplayHotkey = name);
        expiryBox = new Border
        {
            Background = Theme.DangerSoft,
            BorderBrush = Theme.Danger,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 16),
            Child = expiryText,
            Visibility = Visibility.Collapsed,
        };
        expiryText.Foreground = Theme.Danger;

        Children.Add(Title("General"));
        Children.Add(expiryBox);
        Children.Add(ToggleRow(
            "Autoplay",
            "Master switch for sending autoplay input. Keys are sent only while the osu! window is already in the foreground.",
            autoplay));
        Children.Add(HotkeyRow(
            "Toggle window",
            "Hotkey that hides or shows this window.",
            panelKey));
        Children.Add(HotkeyRow(
            "Toggle autoplay",
            "Hotkey that turns autoplay on or off.",
            autoKey));
        Children.Add(Section("Status"));
        Children.Add(summary);

        var grid = new Grid { Margin = new Thickness(0, 10, 0, 18) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        AddStatus(grid, 0, "Process", process);
        AddStatus(grid, 1, "Layout", layout);
        AddStatus(grid, 2, "Ruleset", ruleset);
        AddStatus(grid, 3, "Screen", screen);
        AddStatus(grid, 4, "Audio time", time);
        AddStatus(grid, 5, "Progress", progress);
        AddStatus(grid, 6, "Columns", columns);
        AddStatus(grid, 7, "Paused", paused);
        AddStatus(grid, 8, "Foreground", foreground);
        AddStatus(grid, 9, "Playing", playing);
        AddStatus(grid, 10, "Keys", keys);
        AddStatus(grid, 11, "Settings", settingsPath);
        Children.Add(grid);
        Children.Add(Credit());

        autoplay.Checked += (_, _) => SetAutoplay(true);
        autoplay.Unchecked += (_, _) => SetAutoplay(false);
    }

    public void Refresh()
    {
        var settings = App.Settings.Current;
        var expiry = BuildExpiry.Current;
        bool expired = expiry.IsExpired();
        bool showExpiry = expiry.HasEndTime || expiry.StampInvalid;
        expiryBox.Visibility = showExpiry ? Visibility.Visible : Visibility.Collapsed;
        if (showExpiry)
            expiryText.Text = expiry.Describe(DateTimeOffset.UtcNow);

        suppress = true;
        autoplay.IsEnabled = !expired;
        autoplay.IsChecked = settings.AutoplayEnabled && !expired;
        suppress = false;

        panelKey.Show(settings.PanelHotkey);
        autoKey.Show(settings.AutoplayHotkey);

        var snap = App.Session.Snapshot();
        summary.Text = Summary(snap);
        summary.Foreground = IsError(snap) ? Theme.Danger : snap.Playing ? Theme.Ok : Theme.Ink;
        process.Text = snap.Attached ? $"osu!stable pid {snap.Pid}" : "not attached";
        layout.Text = snap.LayoutOk ? "matched" : "not matched";
        ruleset.Text = string.IsNullOrEmpty(snap.Ruleset) ? "—" : snap.Ruleset;
        screen.Text = string.IsNullOrEmpty(snap.Screen) ? "—" : snap.Screen;
        time.Text = OverlayStatus.FormatClock(snap.Time);
        progress.Text = snap.Total == 0 ? "—" : $"{snap.Applied}/{snap.Total}";
        columns.Text = snap.Columns > 0 ? $"{snap.Columns}K" : "—";
        paused.Text = snap.Paused ? "yes" : "no";
        foreground.Text = snap.Foreground ? "osu! is foreground" : "input withheld";
        playing.Text = snap.Playing ? "sending input" : "idle";
        keys.Text = string.IsNullOrEmpty(snap.Keys) ? "—" : snap.Keys;
        settingsPath.Text = App.Settings.FilePath;
    }

    void SetAutoplay(bool on)
    {
        if (suppress || BuildExpiry.Current.IsExpired())
            return;
        App.Settings.Update(s => s.AutoplayEnabled = on);
    }

    static string Summary(StablePlayStatus snap)
    {
        if (!string.IsNullOrEmpty(snap.Diagnostic))
            return snap.Diagnostic;
        if (!snap.Attached)
            return "not attached";
        return snap.Playing ? "sending input" : "idle";
    }

    static bool IsError(StablePlayStatus snap)
    {
        string diag = snap.Diagnostic;
        if (string.IsNullOrEmpty(diag))
            return false;
        if (diag.StartsWith("autoplay", StringComparison.Ordinal))
            return false;
        if (diag is "mania driver off" or "paused" or "waiting for osu!stable" or "unsupported ruleset")
            return false;
        if (diag.StartsWith("osu! is not", StringComparison.Ordinal))
            return false;
        return true;
    }

    static TextBlock Title(string text) => new()
    {
        Text = text,
        FontSize = 22,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 16),
    };

    static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 8, 0, 8),
    };

    static TextBlock Value() => new()
    {
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 3, 0, 3),
    };

    static Grid ToggleRow(string title, string description, CheckBox box)
    {
        var grid = Row(title, description);
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        return grid;
    }

    static Grid HotkeyRow(string title, string description, KeybindRow row)
    {
        var grid = Row(title, description);
        Grid.SetColumn(row, 1);
        grid.Children.Add(row);
        return grid;
    }

    static Grid Row(string title, string description)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
        });
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(text);
        return grid;
    }

    static void AddStatus(Grid grid, int row, string label, TextBlock value)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var name = new TextBlock
        {
            Text = label,
            Foreground = Theme.Muted,
            Margin = new Thickness(0, 3, 16, 3),
        };
        Grid.SetRow(name, row);
        Grid.SetRow(value, row);
        Grid.SetColumn(value, 1);
        grid.Children.Add(name);
        grid.Children.Add(value);
    }

    static TextBlock Credit()
    {
        var link = new Hyperlink(new Run("discord.gg/Wenkmg7"))
        {
            NavigateUri = new Uri("https://discord.gg/Wenkmg7"),
            Foreground = Theme.Accent,
        };
        link.RequestNavigate += (_, e) =>
        {
            Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
            e.Handled = true;
        };
        var text = new TextBlock
        {
            Foreground = Theme.Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        text.Inlines.Add(new Run("Made by fsoc, "));
        text.Inlines.Add(link);
        return text;
    }
}

sealed class KeybindRow : Button
{
    readonly Action<KeybindRow> beginCapture;
    readonly Action<ManiacSettings, string> assign;
    public bool Capturing { get; private set; }

    public KeybindRow(Action<KeybindRow> beginCapture, Action<ManiacSettings, string> assign)
    {
        this.beginCapture = beginCapture;
        this.assign = assign;
        MinWidth = 88;
        Padding = new Thickness(10, 4, 10, 4);
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(24, 0, 0, 0);
        Click += (_, _) =>
        {
            Capturing = true;
            Content = "Press a key";
            beginCapture(this);
        };
    }

    public void Show(string name)
    {
        if (!Capturing)
            Content = name;
    }

    public void StopCapture() => Capturing = false;

    public void Commit(string name)
    {
        Capturing = false;
        Content = name;
        App.Settings.Update(s => assign(s, name));
    }
}
