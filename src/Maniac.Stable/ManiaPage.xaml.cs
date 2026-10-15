using Maniac.Core.Mania;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Maniac.Stable;

public sealed partial class ManiaPage : Page
{
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly List<HumanSliderControl> sliders = [];
    bool suppress;
    bool skillDragging;
    double? skillPreview;

    public ManiaPage()
    {
        InitializeComponent();
        SkillSlider.Minimum = ManiaHumanLimits.SkillMin;
        SkillSlider.Maximum = ManiaHumanLimits.SkillMax;
        BuildAdvanced();
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

    void BuildAdvanced()
    {
        AdvancedHost.Children.Clear();
        sliders.Clear();
        string group = "";
        bool firstGroup = true;
        foreach (var spec in ManiaHumanFields.All)
        {
            if (spec.Group != group)
            {
                group = spec.Group;
                AdvancedHost.Children.Add(new TextBlock
                {
                    Text = group,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                    Margin = new Thickness(0, firstGroup ? 4 : 12, 0, 0),
                });
                firstGroup = false;
            }

            var row = new HumanSliderControl(spec);
            sliders.Add(row);
            AdvancedHost.Children.Add(row);
        }

        var reset = new HyperlinkButton { Content = "Reset advanced to Auto" };
        ToolTipService.SetToolTip(reset, "Clear custom advanced values so they follow skill again.");
        reset.Click += (_, _) => App.Settings.Update(s => s.Mania.Human.Clear());
        AdvancedHost.Children.Add(reset);
    }

    void DriverToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (suppress)
            return;
        App.Settings.Update(s => s.Mania.Enabled = DriverToggle.IsOn);
    }

    void SkillSlider_Loaded(object sender, RoutedEventArgs e)
    {
        if (FindThumb(SkillSlider) is not Thumb thumb)
            return;
        thumb.DragStarted += (_, _) => skillDragging = true;
        thumb.DragCompleted += (_, _) => CommitSkill();
    }

    void SkillSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppress)
            return;
        if (skillDragging)
        {
            skillPreview = e.NewValue;
            SkillValue.Text = $"{e.NewValue:0}";
            RefreshSliders();
            return;
        }

        App.Settings.Update(s => s.Mania.Skill = e.NewValue);
    }

    void CommitSkill()
    {
        skillDragging = false;
        skillPreview = null;
        App.Settings.Update(s => s.Mania.Skill = SkillSlider.Value);
    }

    void Refresh()
    {
        var mania = App.Settings.Current.Mania;
        suppress = true;
        DriverToggle.IsOn = mania.Enabled;
        if (!skillDragging)
        {
            SkillSlider.Value = mania.Skill;
            SkillValue.Text = $"{mania.Skill:0}";
        }

        suppress = false;
        RefreshSliders();
    }

    void RefreshSliders()
    {
        var mania = App.Settings.Current.Mania;
        var resolved = skillPreview is double skill
            ? ManiaHumanProfile.Resolve(new Maniac.Core.Settings.ManiaSettings { Skill = skill, Human = mania.Human })
            : ManiaHumanParams.From(mania);
        foreach (var slider in sliders)
            slider.Refresh(resolved, mania.Human);
    }

    static Thumb? FindThumb(DependencyObject root)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Thumb thumb)
                return thumb;
            var nested = FindThumb(child);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
