using Maniac;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Maniac.Plugin;

public partial class ManiacPanel : CompositeDrawable
{
    SpriteText status = null!;

    public ManiacPanel()
    {
        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        Margin = new MarginPadding(12);
        AutoSizeAxes = Axes.Both;
        AlwaysPresent = true;
        Depth = float.MinValue;
        Name = "maniac-overlay";
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        string built = File.GetLastWriteTime(typeof(ManiacPanel).Assembly.Location).ToString("HH:mm:ss");

        InternalChild = new Container
        {
            AutoSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 8,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black.Opacity(0.82f),
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(12),
                    Spacing = new Vector2(0, 6),
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Text = "maniac",
                            Font = FontUsage.Default.With(size: 18, weight: "Bold"),
                        },
                        status = new SpriteText
                        {
                            Text = $"plugin {built}",
                            Font = FontUsage.Default.With(size: 12),
                            Colour = Colour4.White.Opacity(0.7f),
                        },
                        row(
                            button("Auto", ManiaAuto.Toggle),
                            button("Reload", Session.RequestReload),
                            button("Unload", Session.RequestUnload)),
                        row(
                            button("Z", () => tap(Key.Z)),
                            button("X", () => tap(Key.X)),
                            button("Space", () => tap(Key.Space))),
                        row(
                            button("Click", click),
                            button("Nudge", nudge)),
                    },
                },
            },
        };
    }

    static FillFlowContainer row(params Drawable[] children) => new()
    {
        AutoSizeAxes = Axes.Both,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(6, 0),
        Children = children,
    };

    PanelButton button(string label, Action action) => new(label, action);

    void tap(Key key)
    {
        var im = GetContainingInputManager();
        if (im == null)
        {
            setStatus("no InputManager");
            return;
        }

        GameInput.TapKey(im, Scheduler, key);
        setStatus($"key {key}");
    }

    void click()
    {
        var im = GetContainingInputManager();
        if (im == null)
        {
            setStatus("no InputManager");
            return;
        }

        GameInput.Click(im, Scheduler);
        setStatus("mouse left");
    }

    void nudge()
    {
        var im = GetContainingInputManager();
        if (im == null)
        {
            setStatus("no InputManager");
            return;
        }

        GameInput.NudgeMouse(im, new Vector2(48, 24));
        setStatus("mouse +48,+24");
    }

    void setStatus(string text) => status.Text = text;

    protected override void Update()
    {
        base.Update();
        if (status != null)
            status.Text = ManiaAuto.Status;
    }

    protected override bool Handle(osu.Framework.Input.Events.UIEvent e)
    {
        return e is MouseEvent && base.Handle(e);
    }

    sealed partial class PanelButton : ClickableContainer
    {
        public PanelButton(string label, Action action)
        {
            AutoSizeAxes = Axes.Both;
            Action = action;
            Child = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 4,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White.Opacity(0.12f),
                    },
                    new SpriteText
                    {
                        Text = label,
                        Font = FontUsage.Default.With(size: 14),
                        Padding = new MarginPadding { Horizontal = 10, Vertical = 5 },
                    },
                },
            };
        }
    }
}
