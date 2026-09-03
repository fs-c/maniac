using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osuTK;

namespace Maniac.Plugin.Ui;

sealed partial class BlockingBox : Box
{
    protected override bool OnHover(HoverEvent e) => true;

    protected override bool OnMouseDown(MouseDownEvent e) => true;

    protected override bool OnClick(ClickEvent e) => true;

    protected override bool OnDragStart(DragStartEvent e) => true;

    protected override bool OnScroll(ScrollEvent e) => true;
}

sealed partial class StatusDot : CircularContainer, IHasTooltip
{
    readonly Circle fill;

    public LocalisableString TooltipText { get; private set; }

    public StatusDot()
    {
        Size = new Vector2(11);
        Masking = true;
        BorderThickness = 1.6f;
        BorderColour = Colour4.White.Opacity(0.5f);
        Child = fill = new Circle
        {
            Size = new Vector2(7),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = Colour4.FromHex("7dffb3"),
        };
    }

    public void SetState(bool on, bool paused, string tooltip)
    {
        fill.Alpha = on ? 1 : 0;
        fill.Colour = paused ? Colour4.FromHex("ffd36a") : Colour4.FromHex("7dffb3");
        BorderColour = Colour4.White.Opacity(on ? 0.35f : 0.5f);
        TooltipText = tooltip;
    }
}

sealed partial class ChevronIcon : CompositeDrawable
{
    public ChevronIcon()
    {
        Size = new Vector2(12, 7);
        Origin = Anchor.Centre;
        Anchor = Anchor.Centre;
        InternalChildren = new Drawable[]
        {
            Arm(-1),
            Arm(1),
        };
    }

    static Box Arm(int side) => new()
    {
        Size = new Vector2(8, 1.8f),
        Origin = Anchor.Centre,
        Anchor = Anchor.TopCentre,
        X = side * 2.7f,
        Y = 3.6f,
        Rotation = side * 38,
        Colour = Colour4.White,
    };
}

sealed partial class GearIcon : CompositeDrawable
{
    public GearIcon()
    {
        Size = new Vector2(14);
        Origin = Anchor.Centre;
        Anchor = Anchor.Centre;
        InternalChildren = new Drawable[]
        {
            Spoke(0),
            Spoke(45),
            Spoke(90),
            Spoke(135),
            new CircularContainer
            {
                Size = new Vector2(8),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Masking = true,
                BorderThickness = 1.7f,
                BorderColour = Colour4.White,
                Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0 },
            },
        };
    }

    static Drawable Spoke(float rotation) => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Rotation = rotation,
        Child = new Box
        {
            Size = new Vector2(2.4f, 12),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = Colour4.White,
        },
    };
}

sealed partial class IconButton : ClickableContainer, IHasTooltip
{
    readonly Box background;
    readonly Drawable icon;
    bool active;

    public LocalisableString TooltipText { get; set; }

    public IconButton(Drawable icon, Action action, string tooltip)
    {
        Size = new Vector2(28);
        Action = action;
        TooltipText = tooltip;
        this.icon = icon;
        Masking = true;
        CornerRadius = 4;
        icon.Anchor = Anchor.Centre;
        icon.Origin = Anchor.Centre;
        Children = new Drawable[]
        {
            background = new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.White.Opacity(0) },
            icon,
        };
    }

    public Drawable Icon => icon;

    public void SetActive(bool value)
    {
        active = value;
        background.Colour = Colour4.White.Opacity(value ? 0.18f : 0);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!active)
            background.Colour = Colour4.White.Opacity(0.1f);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.Colour = Colour4.White.Opacity(active ? 0.18f : 0);
}

sealed partial class PanelButton : ClickableContainer
{
    readonly Box background;
    readonly SpriteText label;
    bool active;

    public PanelButton(string text, Action action, float minWidth = 72, bool fillWidth = false)
    {
        AutoSizeAxes = Axes.Y;
        if (fillWidth)
        {
            RelativeSizeAxes = Axes.X;
            Margin = new MarginPadding { Horizontal = 3 };
        }
        else
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

sealed partial class ToggleRow : CompositeDrawable, IHasTooltip
{
    readonly SpriteText value;
    readonly Action<bool> changed;
    bool on;

    public LocalisableString TooltipText { get; }

    public ToggleRow(string title, bool initial, Action<bool> onChanged, string tooltip = "")
    {
        TooltipText = tooltip;
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

sealed partial class DisclosureRow : CompositeDrawable
{
    readonly Box background;
    readonly SpriteText action;
    readonly SpriteText titleElement;

    readonly Action<bool> changed;
    bool open;

    public DisclosureRow(string title, bool initial, Action<bool> onChanged)
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        changed = onChanged;
        open = initial;
        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Colour4.Transparent,
            },
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
                        titleElement = new SpriteText
                        {
                            Text = title,
                            Font = FontUsage.Default.With(size: 13, weight: FontWeight.SemiBold),
                            Colour = Colour4.White.Opacity(0.9f),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        action = new SpriteText
                        {
                            Text = ActionLabel(open),
                            Font = FontUsage.Default.With(size: 12, weight: FontWeight.SemiBold),
                            Colour = Colour4.FromHex("7dffb3").Opacity(0.9f),
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                        },
                    },
                },
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        titleElement.Font = FontUsage.Default.With(size: 13, weight: FontWeight.Bold);
        action.Font = FontUsage.Default.With(size: 12, weight: FontWeight.Bold);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        titleElement.Font = FontUsage.Default.With(size: 13, weight: FontWeight.SemiBold);
        action.Font = FontUsage.Default.With(size: 12, weight: FontWeight.SemiBold);
    }

    protected override bool OnClick(ClickEvent e)
    {
        SetOpen(!open, notify: true);
        return true;
    }

    public void SetOpen(bool value) => SetOpen(value, notify: false);

    void SetOpen(bool value, bool notify)
    {
        if (open == value)
            return;
        open = value;
        action.Text = ActionLabel(open);
        if (notify)
            changed(open);
    }

    static string ActionLabel(bool value) => value ? "Hide" : "Show";
}

sealed partial class QuietLink : ClickableContainer, IHasTooltip
{
    readonly SpriteText label;

    public LocalisableString TooltipText { get; }

    public QuietLink(string text, Action action, string tooltip = "")
    {
        AutoSizeAxes = Axes.Both;
        Action = action;
        TooltipText = tooltip;
        Margin = new MarginPadding { Top = 4 };
        Child = label = new SpriteText
        {
            Text = text,
            Font = FontUsage.Default.With(size: 12),
            Colour = Colour4.White.Opacity(0.4f),
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        label.Colour = Colour4.White.Opacity(0.7f);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) => label.Colour = Colour4.White.Opacity(0.4f);
}

sealed partial class KeybindRow : CompositeDrawable, IHasTooltip
{
    readonly SpriteText value;
    readonly Action<string> changed;
    bool capturing;

    public LocalisableString TooltipText { get; }

    public KeybindRow(string title, string current, Action<string> onChanged, string tooltip = "")
    {
        TooltipText = tooltip;
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

    public static GridContainer Nav(params Drawable[] children) => new()
    {
        RelativeSizeAxes = Axes.X,
        AutoSizeAxes = Axes.Y,
        ColumnDimensions = children.Select(_ => new Dimension()).ToArray(),
        RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
        Content = new[] { children },
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

    public static SpriteText Section(string text) => new()
    {
        Text = text,
        Font = FontUsage.Default.With(size: 12, weight: "Bold"),
        Colour = Colour4.White.Opacity(0.7f),
        Margin = new MarginPadding { Top = 4 },
    };
}

sealed partial class SliderRow : CompositeDrawable, IHasTooltip
{
    readonly SpriteText valueText;
    readonly Box fill;
    readonly Container track;
    readonly Action<double> commit;
    readonly Action<double>? preview;
    readonly Action? released;
    readonly Func<double, string> format;
    readonly double min;
    readonly double max;
    readonly double step;
    double value;
    double saved;
    bool dragging;

    public LocalisableString TooltipText { get; }

    public SliderRow(
        string title,
        double min,
        double max,
        double step,
        double initial,
        Action<double> onCommit,
        Func<double, string> format,
        string tooltip = "",
        Action<double>? onPreview = null,
        Action? onRelease = null)
    {
        this.min = min;
        this.max = max;
        this.step = step;
        commit = onCommit;
        preview = onPreview;
        released = onRelease;
        this.format = format;
        TooltipText = tooltip;
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
        preview?.Invoke(value);
    }

    void EndDrag()
    {
        if (!dragging)
            return;
        dragging = false;
        if (value != saved)
        {
            saved = value;
            commit(value);
        }

        released?.Invoke();
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
}

sealed partial class AutoSliderRow : CompositeDrawable, IHasTooltip
{
    readonly SpriteText valueText;
    readonly SpriteText stateText;
    readonly Box fill;
    readonly Container track;
    readonly Action<double> commit;
    readonly Action reset;
    readonly Func<double, string> format;
    readonly string tooltip;
    readonly double min;
    readonly double max;
    readonly double step;
    double value;
    double saved;
    bool custom;
    bool startedCustom;
    bool dragging;

    public LocalisableString TooltipText =>
        custom ? $"{tooltip} Click Custom to follow skill again." : tooltip;

    public AutoSliderRow(
        string title,
        double min,
        double max,
        double step,
        double initial,
        bool custom,
        Action<double> onCommit,
        Action onReset,
        Func<double, string> format,
        string tooltip = "")
    {
        this.min = min;
        this.max = max;
        this.step = step;
        commit = onCommit;
        reset = onReset;
        this.format = format;
        this.tooltip = tooltip;
        this.custom = custom;
        value = Snap(initial);
        saved = value;
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;

        valueText = new SpriteText
        {
            Font = FontUsage.Default.With(size: 13, weight: "Bold"),
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
        };

        stateText = new SpriteText
        {
            Font = FontUsage.Default.With(size: 11, weight: "Bold"),
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

        var stateHit = new ClickableContainer
        {
            AutoSizeAxes = Axes.Both,
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            Action = ToggleState,
            Child = stateText,
        };

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
                    ColumnDimensions =
                    [
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.Absolute, 52),
                    ],
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
                            stateHit,
                        },
                    },
                },
                track,
            },
        };

        Refresh();
    }

    public void SetValue(double next, bool isCustom)
    {
        if (dragging)
            return;
        custom = isCustom;
        value = Snap(next);
        saved = value;
        Refresh();
    }

    void ToggleState()
    {
        if (custom)
        {
            reset();
            return;
        }

        custom = true;
        saved = value;
        commit(value);
        Refresh();
    }

    void ApplyPointer(Vector2 screen)
    {
        if (!dragging)
            startedCustom = custom;
        dragging = true;
        float width = track.DrawWidth;
        if (width <= 0)
            return;
        float t = Math.Clamp(track.ToLocalSpace(screen).X / width, 0, 1);
        value = Snap(min + t * (max - min));
        custom = true;
        Refresh();
    }

    void EndDrag()
    {
        if (!dragging)
            return;
        dragging = false;
        if (value == saved && startedCustom)
            return;
        saved = value;
        custom = true;
        commit(value);
    }

    void Refresh()
    {
        valueText.Text = format(value);
        stateText.Text = custom ? "Custom" : "Auto";
        stateText.Colour = custom ? Colour4.FromHex("7dffb3") : Colour4.White.Opacity(0.45f);
        fill.Colour = Colour4.FromHex("7dffb3").Opacity(custom ? 0.85f : 0.45f);
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

sealed partial class OverlayTooltipContainer : TooltipContainer
{
    protected override double AppearDelay => 280;

    protected override ITooltip CreateTooltip() => new OverlayTooltip();
}

sealed partial class OverlayTooltip : VisibilityContainer, ITooltip
{
    readonly TextFlowContainer text;

    public OverlayTooltip()
    {
        Width = 250;
        AutoSizeAxes = Axes.Y;
        BypassAutoSizeAxes = Axes.Both;
        Masking = true;
        CornerRadius = 4;
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Colour4.Black.Opacity(0.94f),
            },
            text = new TextFlowContainer(t =>
            {
                t.Font = FontUsage.Default.With(size: 12);
                t.Colour = Colour4.White.Opacity(0.9f);
            })
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding(8),
                Spacing = new Vector2(0, 2),
            },
        };
    }

    public void SetContent(object content)
    {
        text.Text = content is LocalisableString local ? local : content?.ToString() ?? "";
    }

    public void Move(Vector2 pos) => Position = pos;

    protected override void PopIn() => this.FadeIn(80);

    protected override void PopOut() => this.FadeOut(80);
}
