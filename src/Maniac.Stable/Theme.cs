using System.Windows;
using System.Windows.Media;

namespace Maniac.Stable;

static class Theme
{
    public static readonly SolidColorBrush Canvas = Freeze(0xF6, 0xF6, 0xF4);
    public static readonly SolidColorBrush Panel = Freeze(0xFF, 0xFF, 0xFF);
    public static readonly SolidColorBrush Line = Freeze(0xE4, 0xE4, 0xE0);
    public static readonly SolidColorBrush Ink = Freeze(0x1C, 0x1C, 0x1A);
    public static readonly SolidColorBrush Muted = Freeze(0x6B, 0x6B, 0x66);
    public static readonly SolidColorBrush Accent = Freeze(0xC4, 0x3B, 0x78);
    public static readonly SolidColorBrush AccentSoft = Freeze(0xFF, 0xEC, 0xF4);
    public static readonly SolidColorBrush Hover = Freeze(0xF3, 0xF3, 0xF0);
    public static readonly SolidColorBrush Danger = Freeze(0xB0, 0x30, 0x30);
    public static readonly SolidColorBrush DangerSoft = Freeze(0xFD, 0xEE, 0xEE);
    public static readonly SolidColorBrush Ok = Freeze(0x1E, 0x7A, 0x3E);

    public static readonly FontFamily Font = new("Segoe UI");

    static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
