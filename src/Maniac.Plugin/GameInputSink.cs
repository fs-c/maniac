using System.Reflection;
using Maniac.Core.Input;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Game.Rulesets.UI;
using osuTK;

namespace Maniac.Plugin;

sealed class GameInputSink : IInputSink
{
    readonly Action<int> press;
    readonly Action<int> release;
    readonly bool[] held;
    InputManager? mouse;
    DrawableRuleset? ruleset;

    public bool HasMouse => mouse != null;

    GameInputSink(Action<int> press, Action<int> release, int maxAction)
    {
        this.press = press;
        this.release = release;
        held = new bool[maxAction + 1];
    }

    public static GameInputSink? TryCreate(object keyBindingContainer, int maxAction)
    {
        var actionType = GameBindings.KeyBindingActionType(keyBindingContainer);
        if (actionType == null)
            return null;
        var pressMi = GameBindings.FindTrigger(keyBindingContainer, "TriggerPressed", actionType);
        var releaseMi = GameBindings.FindTrigger(keyBindingContainer, "TriggerReleased", actionType);
        if (pressMi == null || releaseMi == null)
            return null;

        var wrapperType = typeof(TypedTriggers<>).MakeGenericType(actionType);
        if (Activator.CreateInstance(wrapperType, keyBindingContainer, pressMi, releaseMi, maxAction) is not IInputSink typed)
            return null;

        return new GameInputSink(typed.Press, typed.Release, maxAction);
    }

    public void BindMouse(InputManager? inputManager, DrawableRuleset? drawableRuleset)
    {
        mouse = inputManager;
        ruleset = drawableRuleset;
    }

    public void Press(int action)
    {
        if ((uint)action >= (uint)held.Length || held[action])
            return;
        held[action] = true;
        press(action);
    }

    public void Release(int action)
    {
        if ((uint)action >= (uint)held.Length || !held[action])
            return;
        held[action] = false;
        release(action);
    }

    public void MoveMouse(float x, float y)
    {
        if (mouse == null || ruleset == null)
            return;
        var screen = ruleset.Playfield.GamefieldToScreenSpace(new Vector2(x, y));
        new MousePositionAbsoluteInput { Position = screen }.Apply(mouse.CurrentState, mouse);
    }

    public void ReleaseHeld()
    {
        for (int i = 0; i < held.Length; i++)
        {
            if (!held[i])
                continue;
            held[i] = false;
            release(i);
        }
    }

    sealed class TypedTriggers<T> : IInputSink where T : struct, Enum
    {
        readonly Func<T, bool> press;
        readonly Func<T, bool> release;
        readonly T[] values;

        public TypedTriggers(object container, MethodInfo pressMethod, MethodInfo releaseMethod, int maxAction)
        {
            press = pressMethod.CreateDelegate<Func<T, bool>>(container);
            release = releaseMethod.CreateDelegate<Func<T, bool>>(container);
            values = new T[maxAction + 1];
            for (int i = 0; i <= maxAction; i++)
                values[i] = (T)Enum.ToObject(typeof(T), i);
        }

        public void Press(int action)
        {
            if ((uint)action < (uint)values.Length)
                press(values[action]);
        }

        public void Release(int action)
        {
            if ((uint)action < (uint)values.Length)
                release(values[action]);
        }

        public void MoveMouse(float x, float y) { }
        public void ReleaseHeld() { }
    }
}
