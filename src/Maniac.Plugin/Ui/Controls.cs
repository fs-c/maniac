using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
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
        Size = new Vector2(UiTheme.StatusDotSize);
        Masking = true;
        BorderThickness = 1.6f;
        BorderColour = UiTheme.DotBorder;
        Child = fill = new Circle
        {
            Size = new Vector2(UiTheme.StatusDotFill),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = UiTheme.Accent,
        };
    }

    public void SetState(bool on, bool paused, string tooltip)
    {
        fill.Alpha = on ? 1 : 0;
        fill.Colour = paused ? UiTheme.Warning : UiTheme.Accent;
        BorderColour = on ? UiTheme.DotBorderOn : UiTheme.DotBorder;
        TooltipText = tooltip;
    }
}

sealed partial class BrandButton : ClickableContainer
{
    readonly Box background;
    bool active;

    public BrandButton(string text, Action action)
    {
        AutoSizeAxes = Axes.Both;
        Action = action;
        Masking = true;
        CornerRadius = UiTheme.ControlRadius;
        Anchor = Anchor.CentreLeft;
        Origin = Anchor.CentreLeft;
        Children = new Drawable[]
        {
            background = new Box { RelativeSizeAxes = Axes.Both, Colour = UiTheme.ChromeIdle },
            new SpriteText
            {
                Text = text,
                Font = UiTheme.Heading,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Padding = new MarginPadding { Horizontal = 6, Vertical = 4 },
            },
        };
    }

    public void SetActive(bool value)
    {
        active = value;
        // background.Colour = UiTheme.ChromeFill(value);
    }

    protected override bool OnHover(HoverEvent e)
    {
        // background.Colour = UiTheme.ChromeFill(active, hovered: true);
        return true;
    }

    // protected override void OnHoverLost(HoverLostEvent e) =>
    //     background.Colour = UiTheme.ChromeFill(active);
}

sealed partial class IconButton : ClickableContainer, IHasTooltip
{
    readonly Box background;
    readonly Drawable icon;
    bool active;

    public LocalisableString TooltipText { get; set; }

    public IconButton(Drawable icon, Action action, string tooltip = "")
    {
        Size = new Vector2(UiTheme.ButtonSize);
        Action = action;
        TooltipText = tooltip;
        this.icon = icon;
        Masking = true;
        CornerRadius = UiTheme.ControlRadius;
        icon.Anchor = Anchor.Centre;
        icon.Origin = Anchor.Centre;
        Children = new Drawable[]
        {
            background = new Box { RelativeSizeAxes = Axes.Both, Colour = UiTheme.ChromeIdle },
            icon,
        };
    }

    public Drawable Icon => icon;

    public void SetActive(bool value)
    {
        active = value;
        background.Colour = UiTheme.ChromeFill(value);
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.Colour = UiTheme.ChromeFill(active, hovered: true);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.Colour = UiTheme.ChromeFill(active);
}

sealed partial class PanelButton : ClickableContainer, IHasTooltip
{
    readonly Box background;
    readonly SpriteText? label;
    bool active;

    public LocalisableString TooltipText { get; set; }

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
        label = new SpriteText
        {
            Text = text,
            Font = UiTheme.Body,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Padding = new MarginPadding { Horizontal = 8, Vertical = 5 },
        };
        background = MakeBackground();
        Finish(label);
    }

    public PanelButton(IconUsage icon, Action action, string tooltip = "")
    {
        Size = new Vector2(UiTheme.ButtonSize);
        Action = action;
        TooltipText = tooltip;
        background = MakeBackground();
        Finish(UiTheme.Icon(icon, UiTheme.IconLarge));
    }

    static Box MakeBackground() => new()
    {
        RelativeSizeAxes = Axes.Both,
        Colour = UiTheme.TabIdle,
    };

    void Finish(Drawable content)
    {
        Masking = true;
        CornerRadius = UiTheme.ControlRadius;
        Children = new Drawable[] { background, content };
    }

    public void SetText(string text)
    {
        if (label != null)
            label.Text = text;
    }

    public void SetActive(bool value)
    {
        active = value;
        background.Colour = UiTheme.TabFill(value);
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.Colour = UiTheme.TabFill(active, hovered: true);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.Colour = UiTheme.TabFill(active);
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
                        Font = UiTheme.Body,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    value = new SpriteText
                    {
                        Text = Label(on),
                        Font = UiTheme.BodyBold,
                        Colour = UiTheme.OnOff(on),
                    },
                },
            },
        };
    }

    protected override bool OnClick(ClickEvent e)
    {
        on = !on;
        value.Text = Label(on);
        value.Colour = UiTheme.OnOff(on);
        changed(on);
        return true;
    }

    public void SetValue(bool valueOn)
    {
        on = valueOn;
        value.Text = Label(on);
        value.Colour = UiTheme.OnOff(on);
    }

    static string Label(bool value) => value ? "ON" : "OFF";
}

static class SliderMath
{
    public static double Snap(double v, double min, double max, double step)
    {
        v = Math.Clamp(v, min, max);
        if (step <= 0)
            return v;
        return Math.Clamp(min + Math.Round((v - min) / step) * step, min, max);
    }
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
                            Font = UiTheme.BodySemiBold,
                            Colour = UiTheme.TextEmphasis,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        action = new SpriteText
                        {
                            Text = ActionLabel(open),
                            Font = UiTheme.SmallSemiBold,
                            Colour = UiTheme.AccentEmphasis,
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
        titleElement.Font = UiTheme.BodyBold;
        action.Font = UiTheme.SmallBold;
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        titleElement.Font = UiTheme.BodySemiBold;
        action.Font = UiTheme.SmallSemiBold;
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
            Font = UiTheme.Small,
            Colour = UiTheme.TextQuiet,
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        label.Colour = UiTheme.TextSecondary;
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) => label.Colour = UiTheme.TextQuiet;
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
                        Font = UiTheme.Body,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    value = new SpriteText
                    {
                        Text = current,
                        Font = UiTheme.BodyBold,
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
        Font = UiTheme.Small;
        Colour = UiTheme.TextBody;
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
        Font = UiTheme.Heading,
    };

    public static SpriteText Hint(string text) => new()
    {
        Text = text,
        Font = UiTheme.Caption,
        Colour = UiTheme.TextMuted,
        RelativeSizeAxes = Axes.X,
    };

    public static SpriteText Section(string text) => new()
    {
        Text = text,
        Font = UiTheme.SmallBold,
        Colour = UiTheme.TextSecondary,
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
        value = SliderMath.Snap(initial, min, max, step);
        saved = value;
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;

        valueText = new SpriteText
        {
            Text = format(value),
            Font = UiTheme.BodyBold,
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
        };

        fill = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Width = 0,
            Colour = UiTheme.SliderFill(),
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
        };

        var bar = new SliderTrack
        {
            RelativeSizeAxes = Axes.X,
            Height = UiTheme.SliderHeight,
            Masking = true,
            CornerRadius = UiTheme.SliderRadius,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = UiTheme.Well },
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
                                Font = UiTheme.Body,
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
        value = SliderMath.Snap(next, min, max, step);
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
        value = SliderMath.Snap(min + t * (max - min), min, max, step);
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
        value = SliderMath.Snap(initial, min, max, step);
        saved = value;
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;

        valueText = new SpriteText
        {
            Font = UiTheme.BodyBold,
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
        };

        stateText = new SpriteText
        {
            Font = UiTheme.CaptionBold,
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
        };

        fill = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Width = 0,
            Colour = UiTheme.SliderFill(),
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
        };

        var bar = new SliderTrack
        {
            RelativeSizeAxes = Axes.X,
            Height = UiTheme.SliderHeight,
            Masking = true,
            CornerRadius = UiTheme.SliderRadius,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = UiTheme.Well },
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
                                Font = UiTheme.Body,
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
        value = SliderMath.Snap(next, min, max, step);
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
        value = SliderMath.Snap(min + t * (max - min), min, max, step);
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
        stateText.Colour = custom ? UiTheme.Accent : UiTheme.TextFaint;
        fill.Colour = UiTheme.SliderFill(custom);
        float t = max <= min ? 0 : (float)((value - min) / (max - min));
        fill.Width = Math.Clamp(t, 0, 1);
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
        CornerRadius = UiTheme.ControlRadius;
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = UiTheme.TooltipBackground,
            },
            text = new TextFlowContainer(t =>
            {
                t.Font = UiTheme.Small;
                t.Colour = UiTheme.TextEmphasis;
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
