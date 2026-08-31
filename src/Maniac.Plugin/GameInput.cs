using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Framework.Threading;
using osuTK;
using osuTK.Input;

namespace Maniac.Plugin;

static class GameInput
{
    public static void Apply(InputManager manager, IInput input) =>
        input.Apply(manager.CurrentState, manager);

    public static void TapKey(InputManager manager, Scheduler scheduler, Key key, double holdMs = 50)
    {
        Apply(manager, new KeyboardKeyInput(key, true));
        scheduler.AddDelayed(() => Apply(manager, new KeyboardKeyInput(key, false)), holdMs);
    }

    public static void MoveMouse(InputManager manager, Vector2 screenSpace) =>
        Apply(manager, new MousePositionAbsoluteInput { Position = screenSpace });
}
