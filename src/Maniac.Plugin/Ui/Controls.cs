using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;

namespace Maniac.Plugin.Ui;

sealed partial class PanelButton : ClickableContainer
{
    readonly Box background;
    readonly SpriteText label;
    bool active;

    public PanelButton(string text, Action action, float minWidth = 72)
    {
        AutoSizeAxes = Axes.Y;
        Width = minWidth;
        Action = action;
        Masking = true;
        CornerRadius = 4;
        Children = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Colour4.White.Opacity(0.12f),
            },
            label = new SpriteText
            {
                Text = text,
                Font = FontUsage.Default.With(size: 13),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Padding = new MarginPadding { Horizontal = 8, Vertical = 5 },
            },
        };
    }

    public void SetText(string text) => label.Text = text;

    public void SetActive(bool value)
    {
        active = value;
        background.Colour = Colour4.White.Opacity(value ? 0.28f : 0.12f);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!active)
            background.Colour = Colour4.White.Opacity(0.2f);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.Colour = Colour4.White.Opacity(active ? 0.28f : 0.12f);
    }
}

sealed partial class ToggleRow : CompositeDrawable
{
    readonly SpriteText value;
    readonly Action<bool> changed;
    bool on;

    public ToggleRow(string title, bool initial, Action<bool> onChanged)
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        changed = onChanged;
        on = initial;
        InternalChild = new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            ColumnDimensions = [new Dimension(), new Dimension(GridSizeMode.AutoSize)],
            RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
            Content = new[]
            {
                new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: 13),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    value = new SpriteText
                    {
                        Text = Label(on),
                        Font = FontUsage.Default.With(size: 13, weight: "Bold"),
                        Colour = on ? Colour4.FromHex("7dffb3") : Colour4.White.Opacity(0.6f),
                    },
                },
            },
        };
    }

    protected override bool OnClick(ClickEvent e)
    {
        on = !on;
        value.Text = Label(on);
        value.Colour = on ? Colour4.FromHex("7dffb3") : Colour4.White.Opacity(0.6f);
        changed(on);
        return true;
    }

    public void SetValue(bool valueOn)
    {
        on = valueOn;
        value.Text = Label(on);
        value.Colour = on ? Colour4.FromHex("7dffb3") : Colour4.White.Opacity(0.6f);
    }

    static string Label(bool value) => value ? "ON" : "OFF";
}

sealed partial class KeybindRow : CompositeDrawable
{
    readonly SpriteText value;
    readonly Action<string> changed;
    bool capturing;

    public KeybindRow(string title, string current, Action<string> onChanged)
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        changed = onChanged;
        InternalChild = new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            ColumnDimensions = [new Dimension(), new Dimension(GridSizeMode.AutoSize)],
            RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
            Content = new[]
            {
                new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: 13),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    value = new SpriteText
                    {
                        Text = current,
                        Font = FontUsage.Default.With(size: 13, weight: "Bold"),
                    },
                },
            },
        };
    }

    protected override bool OnClick(ClickEvent e)
    {
        capturing = true;
        value.Text = "...";
        return true;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (!capturing)
            return false;
        capturing = false;
        string name = e.Key.ToString();
        value.Text = name;
        changed(name);
        return true;
    }
}

sealed partial class StatusLine : SpriteText
{
    public StatusLine()
    {
        Font = FontUsage.Default.With(size: 12);
        Colour = Colour4.White.Opacity(0.75f);
        RelativeSizeAxes = Axes.X;
        Truncate = true;
    }
}

static class UiLayout
{
    public static FillFlowContainer Column(params Drawable[] children) => new()
    {
        RelativeSizeAxes = Axes.X,
        AutoSizeAxes = Axes.Y,
        Direction = FillDirection.Vertical,
        Spacing = new Vector2(0, 8),
        Children = children,
    };

    public static FillFlowContainer Row(params Drawable[] children) => new()
    {
        RelativeSizeAxes = Axes.X,
        AutoSizeAxes = Axes.Y,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(6, 0),
        Children = children,
    };

    public static SpriteText Heading(string text) => new()
    {
        Text = text,
        Font = FontUsage.Default.With(size: 16, weight: "Bold"),
    };

    public static SpriteText Hint(string text) => new()
    {
        Text = text,
        Font = FontUsage.Default.With(size: 11),
        Colour = Colour4.White.Opacity(0.55f),
        RelativeSizeAxes = Axes.X,
    };
}
