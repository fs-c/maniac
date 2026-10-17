using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Maniac.Core;

namespace Maniac.Stable;

sealed class MainWindow : Window
{
    readonly GeneralView general;
    readonly ManiaView mania;
    readonly ScrollViewer scroller;
    readonly Button generalNav;
    readonly Button maniaNav;
    readonly DispatcherTimer timer;
    KeybindRow? capture;

    public MainWindow()
    {
        Title = "maniac";
        Width = 860;
        Height = 760;
        MinWidth = 680;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.Canvas;
        FontFamily = Theme.Font;
        FontSize = 13;
        Foreground = Theme.Ink;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        general = new GeneralView(BeginCapture);
        mania = new ManiaView();
        scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = general,
        };

        generalNav = NavButton("General");
        maniaNav = NavButton("Mania");
        generalNav.Click += (_, _) => Show(generalNav, general);
        maniaNav.Click += (_, _) => Show(maniaNav, mania);

        var nav = new StackPanel { Margin = new Thickness(8, 8, 8, 8) };
        nav.Children.Add(generalNav);
        nav.Children.Add(maniaNav);

        var brand = new TextBlock
        {
            Text = "maniac",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(20, 18, 16, 4),
        };
        var version = new TextBlock
        {
            Text = ManiacVersion.Current,
            Foreground = Theme.Muted,
            FontSize = 12,
            Margin = new Thickness(20, 0, 16, 16),
        };
        DockPanel.SetDock(brand, Dock.Top);
        DockPanel.SetDock(version, Dock.Bottom);
        var rail = new DockPanel { LastChildFill = true };
        rail.Children.Add(brand);
        rail.Children.Add(version);
        rail.Children.Add(nav);

        var shell = new Grid();
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(188) });
        shell.ColumnDefinitions.Add(new ColumnDefinition());
        var railBorder = new Border
        {
            Background = Theme.Panel,
            BorderBrush = Theme.Line,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = rail,
        };
        Grid.SetColumn(scroller, 1);
        shell.Children.Add(railBorder);
        shell.Children.Add(scroller);
        Content = shell;

        Show(generalNav, general);
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Refresh();
    }

    public void ToggleAutoplay()
    {
        if (BuildExpiry.Current.IsExpired())
            return;
        App.Settings.Update(s => s.AutoplayEnabled = !s.AutoplayEnabled);
    }

    public void ToggleShown()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        Show();
        Activate();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (capture == null)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        if (e.IsRepeat)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        string? name = HotkeyNames.FromKey(key);
        if (name == null)
            return;

        var row = capture;
        EndCapture();
        row.Commit(name);
    }

    protected override void OnClosed(EventArgs e)
    {
        timer.Stop();
        EndCapture();
        Hotkeys.Stop();
        App.Session.Dispose();
        App.Settings.Flush();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    void BeginCapture(KeybindRow row)
    {
        if (capture != null && capture != row)
            capture.StopCapture();
        capture = row;
        Hotkeys.Suspended = true;
        general.Refresh();
    }

    void EndCapture()
    {
        capture = null;
        Hotkeys.Suspended = false;
    }

    void Refresh()
    {
        general.Refresh();
        mania.Refresh();
    }

    void Show(Button selected, UIElement page)
    {
        if (capture != null)
        {
            capture.StopCapture();
            EndCapture();
            general.Refresh();
        }

        StyleNav(generalNav, selected == generalNav);
        StyleNav(maniaNav, selected == maniaNav);
        scroller.Content = page;
        scroller.ScrollToTop();
    }

    static void StyleNav(Button button, bool selected)
    {
        button.Background = selected ? Theme.AccentSoft : Brushes.Transparent;
        button.Foreground = selected ? Theme.Accent : Theme.Ink;
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        button.Tag = selected;
    }

    static Button NavButton(string text)
    {
        var button = new Button
        {
            Content = text,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(8, 2, 8, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = FlatTemplate(),
        };
        button.MouseEnter += (_, _) =>
        {
            if (button.Tag is not true)
                button.Background = Theme.Hover;
        };
        button.MouseLeave += (_, _) =>
        {
            if (button.Tag is not true)
                button.Background = Brushes.Transparent;
        };
        return button;
    }

    static ControlTemplate FlatTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }
}
