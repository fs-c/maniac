using Maniac.Core.Mania;
using Maniac.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Maniac.Stable;

public sealed partial class HumanSliderControl : UserControl
{
    readonly ManiaHumanField spec;
    bool suppress;
    bool dragging;

    public HumanSliderControl()
    {
        InitializeComponent();
        spec = ManiaHumanFields.All[0];
    }

    public HumanSliderControl(ManiaHumanField spec)
    {
        this.spec = spec;
        InitializeComponent();
        TitleText.Text = spec.Title;
        ToolTipService.SetToolTip(this, spec.Tooltip);
        Slider.Minimum = spec.Min;
        Slider.Maximum = spec.Max;
        Slider.StepFrequency = spec.Step;
        Slider.Loaded += Slider_Loaded;
    }

    public void Refresh(ManiaHumanParams resolved, ManiaHumanOverrides human)
    {
        if (dragging)
            return;
        suppress = true;
        bool custom = spec.OverrideOf(human) != null;
        Slider.Value = spec.Read(resolved);
        ValueText.Text = spec.Format(Slider.Value);
        StateButton.Content = custom ? "Custom" : "Auto";
        ToolTipService.SetToolTip(StateButton, custom
            ? $"{spec.Tooltip} Click Custom to follow skill again."
            : spec.Tooltip);
        suppress = false;
    }

    void Slider_Loaded(object sender, RoutedEventArgs e)
    {
        if (FindThumb(Slider) is not Thumb thumb)
            return;
        thumb.DragStarted += (_, _) => dragging = true;
        thumb.DragCompleted += (_, _) =>
        {
            dragging = false;
            Commit(Slider.Value);
        };
    }

    void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppress)
            return;
        ValueText.Text = spec.Format(e.NewValue);
        if (!dragging)
            Commit(e.NewValue);
    }

    void StateButton_Click(object sender, RoutedEventArgs e)
    {
        var human = App.Settings.Current.Mania.Human;
        if (spec.OverrideOf(human) != null)
        {
            App.Settings.Update(s => spec.Assign(s.Mania.Human, null));
            return;
        }

        App.Settings.Update(s => spec.Assign(s.Mania.Human, Slider.Value));
    }

    void Commit(double value) =>
        App.Settings.Update(s => spec.Assign(s.Mania.Human, value));

    static Thumb? FindThumb(DependencyObject root)
    {
        int n = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is Thumb thumb)
                return thumb;
            var nested = FindThumb(child);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
