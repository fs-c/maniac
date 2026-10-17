using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Maniac.Core.Mania;
using Maniac.Core.Settings;

namespace Maniac.Stable;

sealed class ManiaView : StackPanel
{
    readonly CheckBox driver = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly Slider skill = new();
    readonly TextBlock skillValue = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    readonly Expander advanced = new() { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    readonly HumanSliderRow[] rows;
    bool suppress;
    bool skillDragging;
    double? skillPreview;

    public ManiaView()
    {
        Margin = new Thickness(32, 28, 32, 36);
        skill.Minimum = ManiaHumanLimits.SkillMin;
        skill.Maximum = ManiaHumanLimits.SkillMax;
        skill.TickFrequency = 1;
        skill.IsSnapToTickEnabled = true;
        skill.Margin = new Thickness(0, 8, 0, 0);

        var specs = ManiaHumanFields.All;
        rows = new HumanSliderRow[specs.Length];
        var advancedBody = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        advancedBody.Children.Add(new TextBlock
        {
            Text = "These follow skill until you customize a value.",
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
        });
        string group = "";
        for (int i = 0; i < specs.Length; i++)
        {
            if (specs[i].Group != group)
            {
                group = specs[i].Group;
                advancedBody.Children.Add(new TextBlock
                {
                    Text = group,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, i == 0 ? 4 : 14, 0, 6),
                });
            }

            rows[i] = new HumanSliderRow(specs[i]);
            advancedBody.Children.Add(rows[i]);
        }

        var reset = new Button
        {
            Content = "Reset advanced to Auto",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            ToolTip = "Clear custom advanced values so they follow skill again.",
        };
        reset.Click += (_, _) => App.Settings.Update(s => s.Mania.Human.Clear());
        advancedBody.Children.Add(reset);
        advanced.Content = advancedBody;
        advanced.Header = "Advanced";

        Children.Add(new TextBlock
        {
            Text = "Mania",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16),
        });
        Children.Add(DriverRow());
        Children.Add(SkillBlock());
        Children.Add(advanced);

        driver.Checked += (_, _) => SetDriver(true);
        driver.Unchecked += (_, _) => SetDriver(false);
        skill.ValueChanged += SkillChanged;
        skill.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => skillDragging = true));
        skill.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            skillDragging = false;
            skillPreview = null;
            App.Settings.Update(s => s.Mania.Skill = skill.Value);
            Refresh();
        }));
        advanced.Expanded += (_, _) => SetAdvanced(true);
        advanced.Collapsed += (_, _) => SetAdvanced(false);
    }

    public void Refresh()
    {
        var mania = App.Settings.Current.Mania;
        suppress = true;
        driver.IsChecked = mania.Enabled;
        if (!skillDragging)
        {
            skill.Value = mania.Skill;
            skillValue.Text = $"{mania.Skill:0}";
        }

        if (advanced.IsExpanded != mania.ShowAdvanced)
            advanced.IsExpanded = mania.ShowAdvanced;
        suppress = false;
        RefreshSliders();
    }

    void RefreshSliders()
    {
        var mania = App.Settings.Current.Mania;
        var resolved = skillPreview is double preview
            ? ManiaHumanProfile.Resolve(new ManiaSettings { Skill = preview, Human = mania.Human })
            : ManiaHumanParams.From(mania);
        foreach (var row in rows)
            row.Refresh(resolved, mania.Human);
    }

    void SetDriver(bool on)
    {
        if (suppress)
            return;
        App.Settings.Update(s => s.Mania.Enabled = on);
    }

    void SetAdvanced(bool show)
    {
        if (suppress)
            return;
        App.Settings.Update(s => s.Mania.ShowAdvanced = show);
    }

    void SkillChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (suppress)
            return;
        skillValue.Text = $"{skill.Value:0}";
        if (skillDragging)
        {
            skillPreview = skill.Value;
            RefreshSliders();
            return;
        }

        App.Settings.Update(s => s.Mania.Skill = skill.Value);
    }

    Grid DriverRow()
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = "Mania driver", FontSize = 14, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock
        {
            Text = "When off, mania charts are not autoplayed.",
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
        });
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(driver, 1);
        grid.Children.Add(text);
        grid.Children.Add(driver);
        return grid;
    }

    StackPanel SkillBlock()
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = "Skill", FontSize = 14, FontWeight = FontWeights.SemiBold });
        Grid.SetColumn(skillValue, 1);
        header.Children.Add(skillValue);
        var block = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        block.Children.Add(header);
        block.Children.Add(new TextBlock
        {
            Text = "Overall performance level. 100 is tighter than typical top-level play; 0 is well below mid-tier. Advanced Auto values follow this until you customize them.",
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
        });
        block.Children.Add(skill);
        return block;
    }
}

sealed class HumanSliderRow : StackPanel
{
    readonly ManiaHumanField spec;
    readonly Slider slider = new();
    readonly TextBlock valueText = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0) };
    readonly Button state = new() { MinWidth = 72, Padding = new Thickness(8, 2, 8, 2) };
    bool suppress;
    bool dragging;
    bool wasCustom;
    double saved;

    public HumanSliderRow(ManiaHumanField spec)
    {
        this.spec = spec;
        Margin = new Thickness(0, 0, 0, 10);
        slider.Minimum = spec.Min;
        slider.Maximum = spec.Max;
        slider.SmallChange = spec.Step;
        slider.LargeChange = spec.Step * 10;
        slider.TickFrequency = spec.Step;
        slider.Margin = new Thickness(0, 4, 0, 0);
        ToolTip = spec.Tooltip;

        var title = new TextBlock
        {
            Text = spec.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(valueText, 1);
        Grid.SetColumn(state, 2);
        header.Children.Add(title);
        header.Children.Add(valueText);
        header.Children.Add(state);
        Children.Add(header);
        Children.Add(slider);

        slider.ValueChanged += (_, _) =>
        {
            if (suppress)
                return;
            Snap();
            valueText.Text = spec.Format(slider.Value);
            if (dragging)
            {
                state.Content = "Custom";
                return;
            }

            saved = slider.Value;
            App.Settings.Update(s => spec.Assign(s.Mania.Human, slider.Value));
        };
        slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) =>
        {
            dragging = true;
            wasCustom = spec.OverrideOf(App.Settings.Current.Mania.Human) != null;
        }));
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            dragging = false;
            if (wasCustom && slider.Value == saved)
                return;
            saved = slider.Value;
            App.Settings.Update(s => spec.Assign(s.Mania.Human, slider.Value));
        }));
        state.Click += (_, _) =>
        {
            var human = App.Settings.Current.Mania.Human;
            if (spec.OverrideOf(human) != null)
            {
                App.Settings.Update(s => spec.Assign(s.Mania.Human, null));
                return;
            }

            App.Settings.Update(s => spec.Assign(s.Mania.Human, slider.Value));
        };
    }

    public void Refresh(ManiaHumanParams resolved, ManiaHumanOverrides human)
    {
        if (dragging)
            return;
        bool custom = spec.OverrideOf(human) != null;
        double value = SnapTo(spec.Read(resolved), spec.Min, spec.Max, spec.Step);
        suppress = true;
        slider.Value = value;
        suppress = false;
        saved = value;
        valueText.Text = spec.Format(value);
        state.Content = custom ? "Custom" : "Auto";
        state.ToolTip = custom ? $"{spec.Tooltip} Click Custom to follow skill again." : spec.Tooltip;
    }

    void Snap()
    {
        double snapped = SnapTo(slider.Value, spec.Min, spec.Max, spec.Step);
        if (snapped == slider.Value)
            return;
        suppress = true;
        slider.Value = snapped;
        suppress = false;
    }

    static double SnapTo(double value, double min, double max, double step)
    {
        value = Math.Clamp(value, min, max);
        if (step <= 0)
            return value;
        return Math.Clamp(min + Math.Round((value - min) / step) * step, min, max);
    }
}
