using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osuTK;

namespace Maniac.Plugin.Ui;

static class UiTheme
{
    public static readonly Colour4 OverlayBackground = Colour4.Black.Opacity(0.95f);
    public static readonly Colour4 OverlayContentBackground = Colour4.White.Opacity(0.05f);
    public static readonly Colour4 TooltipBackground = Colour4.Black.Opacity(0.94f);
    public static readonly Colour4 InsetBackground = Colour4.White.Opacity(0.08f);
    public static readonly Colour4 Well = Colour4.White.Opacity(0.12f);

    public static readonly Colour4 Text = Colour4.White;
    public static readonly Colour4 TextEmphasis = Colour4.White.Opacity(0.9f);
    public static readonly Colour4 TextBody = Colour4.White.Opacity(0.75f);
    public static readonly Colour4 TextSecondary = Colour4.White.Opacity(0.7f);
    public static readonly Colour4 TextInactive = Colour4.White.Opacity(0.6f);
    public static readonly Colour4 TextMuted = Colour4.White.Opacity(0.55f);
    public static readonly Colour4 TextFaint = Colour4.White.Opacity(0.45f);
    public static readonly Colour4 TextQuiet = Colour4.White.Opacity(0.4f);

    public static readonly Colour4 Accent = Colour4.FromHex("7dffb3");
    public static readonly Colour4 AccentEmphasis = Accent.Opacity(0.9f);
    public static readonly Colour4 AccentFill = Accent.Opacity(0.85f);
    public static readonly Colour4 AccentMuted = Accent.Opacity(0.45f);
    public static readonly Colour4 Warning = Colour4.FromHex("ffd36a");
    public static readonly Colour4 IconMuted = TextFaint;

    public static readonly Colour4 ChromeIdle = Colour4.White.Opacity(0);
    public static readonly Colour4 ChromeHover = Colour4.White.Opacity(0.1f);
    public static readonly Colour4 ChromeActive = Colour4.White.Opacity(0.18f);
    public static readonly Colour4 TabIdle = Well;
    public static readonly Colour4 TabHover = Colour4.White.Opacity(0.2f);
    public static readonly Colour4 TabActive = Colour4.White.Opacity(0.28f);

    public static readonly Colour4 DotBorder = Colour4.White.Opacity(0.5f);
    public static readonly Colour4 DotBorderOn = Colour4.White.Opacity(0.35f);

    public static FontUsage Heading => FontUsage.Default.With(size: 16, weight: FontWeight.Bold);
    public static FontUsage Body => FontUsage.Default.With(size: 13);
    public static FontUsage BodyBold => FontUsage.Default.With(size: 13, weight: FontWeight.Bold);
    public static FontUsage BodySemiBold => FontUsage.Default.With(size: 13, weight: FontWeight.SemiBold);
    public static FontUsage Small => FontUsage.Default.With(size: 12);
    public static FontUsage SmallBold => FontUsage.Default.With(size: 12, weight: FontWeight.Bold);
    public static FontUsage SmallSemiBold => FontUsage.Default.With(size: 12, weight: FontWeight.SemiBold);
    public static FontUsage Caption => FontUsage.Default.With(size: 11);
    public static FontUsage CaptionBold => FontUsage.Default.With(size: 11, weight: FontWeight.Bold);

    public const float OverlayRadius = 8;
    public const float InsetRadius = 6;
    public const float ControlRadius = 4;
    public const float SliderRadius = 3;
    public const float ButtonSize = 28;
    public const float IconSmall = 12;
    public const float IconMedium = 16;
    public const float IconLarge = 18;
    public const float SliderHeight = 10;
    public const float StatusDotSize = 11;
    public const float StatusDotFill = 7;

    public static Colour4 ChromeFill(bool active, bool hovered = false) =>
        active ? ChromeActive : hovered ? ChromeHover : ChromeIdle;

    public static Colour4 TabFill(bool active, bool hovered = false) =>
        active ? TabActive : hovered ? TabHover : TabIdle;

    public static Colour4 OnOff(bool on) => on ? Accent : TextInactive;

    public static Colour4 SliderFill(bool emphasized = true) => emphasized ? AccentFill : AccentMuted;

    public static SpriteIcon Icon(IconUsage glyph, float size, Colour4? colour = null) => new()
    {
        Icon = glyph,
        Size = new Vector2(size),
        Colour = colour ?? Text,
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
    };
}
