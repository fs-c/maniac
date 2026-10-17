using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace Maniac.Stable;

static class Hotkeys
{
    static DispatcherTimer? timer;
    static MainWindow? window;
    static bool autoDown;
    static bool panelDown;

    static bool primed;

    public static bool Suspended { get; set; }

    public static void Start(MainWindow target)
    {
        window = target;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) => Poll();
        timer.Start();
    }

    public static void Stop()
    {
        if (timer == null)
            return;
        timer.Stop();
        timer = null;
        window = null;
    }

    static void Poll()
    {
        var target = window;
        if (target == null)
            return;

        var settings = App.Settings.Current;
        bool auto = HotkeyNames.IsDown(settings.AutoplayHotkey);
        bool panel = HotkeyNames.IsDown(settings.PanelHotkey);
        if (!primed)
        {
            autoDown = auto;
            panelDown = panel;
            primed = true;
            return;
        }

        if (!Suspended)
        {
            if (auto && !autoDown)
                target.ToggleAutoplay();
            if (panel && !panelDown)
                target.ToggleShown();
        }

        autoDown = auto;
        panelDown = panel;
    }
}

static class HotkeyNames
{
    readonly record struct Binding(string Name, Key Key, int VirtualKey);

    static readonly Dictionary<Key, string> names;
    static readonly Dictionary<string, int> virtualKeys;

    static HotkeyNames()
    {
        var all = new List<Binding>
        {
            new("LShift", Key.LeftShift, 0xA0),
            new("RShift", Key.RightShift, 0xA1),
            new("LControl", Key.LeftCtrl, 0xA2),
            new("RControl", Key.RightCtrl, 0xA3),
            new("LAlt", Key.LeftAlt, 0xA4),
            new("RAlt", Key.RightAlt, 0xA5),
            new("LWin", Key.LWin, 0x5B),
            new("RWin", Key.RWin, 0x5C),
            new("Up", Key.Up, 0x26),
            new("Down", Key.Down, 0x28),
            new("Left", Key.Left, 0x25),
            new("Right", Key.Right, 0x27),
            new("Enter", Key.Return, 0x0D),
            new("Escape", Key.Escape, 0x1B),
            new("Space", Key.Space, 0x20),
            new("Tab", Key.Tab, 0x09),
            new("BackSpace", Key.Back, 0x08),
            new("Insert", Key.Insert, 0x2D),
            new("Delete", Key.Delete, 0x2E),
            new("PageUp", Key.PageUp, 0x21),
            new("PageDown", Key.PageDown, 0x22),
            new("Home", Key.Home, 0x24),
            new("End", Key.End, 0x23),
            new("CapsLock", Key.CapsLock, 0x14),
            new("ScrollLock", Key.Scroll, 0x91),
            new("PrintScreen", Key.PrintScreen, 0x2C),
            new("Pause", Key.Pause, 0x13),
            new("NumLock", Key.NumLock, 0x90),
            new("KeypadDivide", Key.Divide, 0x6F),
            new("KeypadMultiply", Key.Multiply, 0x6A),
            new("KeypadSubtract", Key.Subtract, 0x6D),
            new("KeypadAdd", Key.Add, 0x6B),
            new("KeypadDecimal", Key.Decimal, 0x6E),
            new("Tilde", Key.OemTilde, 0xC0),
            new("Minus", Key.OemMinus, 0xBD),
            new("Plus", Key.OemPlus, 0xBB),
            new("BracketLeft", Key.OemOpenBrackets, 0xDB),
            new("BracketRight", Key.OemCloseBrackets, 0xDD),
            new("Semicolon", Key.OemSemicolon, 0xBA),
            new("Quote", Key.OemQuotes, 0xDE),
            new("Comma", Key.OemComma, 0xBC),
            new("Period", Key.OemPeriod, 0xBE),
            new("Slash", Key.OemQuestion, 0xBF),
            new("BackSlash", Key.OemBackslash, 0xDC),
        };
        for (int i = 0; i < 24; i++)
            all.Add(new Binding($"F{i + 1}", Key.F1 + i, 0x70 + i));
        for (int i = 0; i < 26; i++)
            all.Add(new Binding($"{(char)('A' + i)}", Key.A + i, 0x41 + i));
        for (int i = 0; i < 10; i++)
        {
            all.Add(new Binding($"Number{i}", Key.D0 + i, 0x30 + i));
            all.Add(new Binding($"Keypad{i}", Key.NumPad0 + i, 0x60 + i));
        }

        names = new Dictionary<Key, string>(all.Count);
        virtualKeys = new Dictionary<string, int>(all.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var binding in all)
        {
            names.Add(binding.Key, binding.Name);
            virtualKeys.Add(binding.Name, binding.VirtualKey);
        }
    }

    public static string? FromKey(Key key) => names.TryGetValue(key, out string? name) ? name : null;

    public static bool IsDown(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !virtualKeys.TryGetValue(name, out int virtualKey))
            return false;
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int virtualKey);
}
