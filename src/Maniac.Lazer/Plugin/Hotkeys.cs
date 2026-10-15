using osuTK.Input;

namespace Maniac.Plugin;

static class Hotkeys
{
    public static bool TryParse(string? name, out Key key)
    {
        key = Key.Unknown;
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return Enum.TryParse(name.Trim(), ignoreCase: true, out key) && key != Key.Unknown;
    }

    public static bool WasPressed(osu.Framework.Input.InputManager manager, Key key, ref bool wasDown)
    {
        bool down = manager.CurrentState.Keyboard.Keys.IsPressed(key);
        bool edge = down && !wasDown;
        wasDown = down;
        return edge;
    }
}
