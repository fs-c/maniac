using Maniac.Core.Mania;
using Maniac.Core.Settings;
using osu.Framework.Graphics;

namespace Maniac.Plugin.Ui;

static class HumanSliderLayout
{
    public static Drawable[] BuildColumn(
        SettingsStore settings,
        ManiaHumanParams resolved,
        ManiaHumanOverrides human,
        out HumanSliderBinding[] bindings)
    {
        var specs = ManiaHumanFields.All;
        bindings = new HumanSliderBinding[specs.Length];
        var children = new List<Drawable>();
        string group = "";
        for (int i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            if (spec.Group != group)
            {
                group = spec.Group;
                children.Add(UiLayout.Section(group));
            }

            double initial = spec.Read(resolved);
            bool custom = spec.OverrideOf(human) != null;
            var row = new AutoSliderRow(
                spec.Title, spec.Min, spec.Max, spec.Step, initial, custom,
                v => settings.Update(s => spec.Assign(s.Mania.Human, v)),
                () => settings.Update(s => spec.Assign(s.Mania.Human, null)),
                spec.Format,
                spec.Tooltip);
            bindings[i] = new HumanSliderBinding(row, spec);
            children.Add(row);
        }

        children.Add(new QuietLink(
            "Reset advanced to Auto",
            () => settings.Update(s => s.Mania.Human.Clear()),
            "Clear custom advanced values so they follow skill again."));
        return children.ToArray();
    }
}

readonly record struct HumanSliderBinding(AutoSliderRow Row, ManiaHumanField Spec)
{
    public void Refresh(ManiaHumanParams resolved, ManiaHumanOverrides human) =>
        Row.SetValue(Spec.Read(resolved), Spec.OverrideOf(human) != null);
}
