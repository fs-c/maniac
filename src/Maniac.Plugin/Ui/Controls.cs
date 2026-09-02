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
        public static FillFlowContainer Column(params Drawable?[] children) => new()
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 6),
            Children = children.OfType<Drawable>().ToArray(),
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

sealed partial class SliderRow : CompositeDrawable
{
    readonly SpriteText valueText;
    readonly Box fill;
    readonly Container track;
    readonly Action<double> commit;
    readonly Func<double, string> format;
    readonly double min;
    readonly double max;
    readonly double step;
    double value;
    double saved;
    bool dragging;

    public SliderRow(string title, double min, double max, double step, double initial, Action<double> onCommit, Func<double, string> format)
    {
        this.min = min;
        this.max = max;
        this.step = step;
        commit = onCommit;
        this.format = format;
        value = Snap(initial);
        saved = value;
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;

        valueText = new SpriteText
        {
            Text = format(value),
            Font = FontUsage.Default.With(size: 13, weight: "Bold"),
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
        };

        fill = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Width = 0,
            Colour = Colour4.FromHex("7dffb3").Opacity(0.85f),
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
        };

        var bar = new SliderTrack
        {
            RelativeSizeAxes = Axes.X,
            Height = 10,
            Masking = true,
            CornerRadius = 3,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.White.Opacity(0.12f) },
                fill,
            },
        };
        bar.Pointer = ApplyPointer;
        bar.Released = EndDrag;
        track = bar;

        InternalChild = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 4),
            Children = new Drawable[]
            {
                new GridContainer
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
                            valueText,
                        },
                    },
                },
                track,
            },
        };

        Refresh();
    }

    public void SetValue(double next)
    {
        if (dragging)
            return;
        value = Snap(next);
        saved = value;
        Refresh();
    }

    void ApplyPointer(Vector2 screen)
    {
        dragging = true;
        float width = track.DrawWidth;
        if (width <= 0)
            return;
        float t = Math.Clamp(track.ToLocalSpace(screen).X / width, 0, 1);
        value = Snap(min + t * (max - min));
        Refresh();
    }

    void EndDrag()
    {
        if (!dragging)
            return;
        dragging = false;
        if (value == saved)
            return;
        saved = value;
        commit(value);
    }

    void Refresh()
    {
        valueText.Text = format(value);
        float t = max <= min ? 0 : (float)((value - min) / (max - min));
        fill.Width = Math.Clamp(t, 0, 1);
    }

    double Snap(double v)
    {
        v = Math.Clamp(v, min, max);
        if (step <= 0)
            return v;
        return Math.Clamp(min + Math.Round((v - min) / step) * step, min, max);
    }

    sealed partial class SliderTrack : Container
    {
        public Action<Vector2>? Pointer;
        public Action? Released;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            Pointer?.Invoke(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => true;

        protected override void OnDrag(DragEvent e) => Pointer?.Invoke(e.ScreenSpaceMousePosition);

        protected override void OnDragEnd(DragEndEvent e) => Released?.Invoke();

        protected override void OnMouseUp(MouseUpEvent e) => Released?.Invoke();
    }
}
