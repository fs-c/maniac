using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Maniac.Stable;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        var titleBar = AppWindow.TitleBar;
        var clear = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        titleBar.BackgroundColor = clear;
        titleBar.InactiveBackgroundColor = clear;
        titleBar.ButtonBackgroundColor = clear;
        titleBar.ButtonInactiveBackgroundColor = clear;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 840));
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
            return;

        Type page = tag switch
        {
            "general" => typeof(GeneralPage),
            "mania" => typeof(ManiaPage),
            _ => throw new InvalidOperationException($"unknown page '{tag}'")
        };
        ContentFrame.Navigate(page);
    }
}
