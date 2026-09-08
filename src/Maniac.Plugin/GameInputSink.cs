using System.Linq.Expressions;
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
    public static string LastFail { get; private set; } = "";

    GameInputSink(Action<int> press, Action<int> release, int maxAction)
    {
        this.press = press;
        this.release = release;
        held = new bool[maxAction + 1];
    }

    public static GameInputSink? TryCreate(object keyBindingContainer, int maxAction)
    {
        LastFail = "";
        try
        {
            var actionType = GameBindings.KeyBindingActionType(keyBindingContainer);
            if (actionType == null)
            {
                LastFail = "no action type on KeyBindingContainer";
                return null;
            }

            var pressMi = GameBindings.FindTrigger(keyBindingContainer, "TriggerPressed", actionType);
            var releaseMi = GameBindings.FindTrigger(keyBindingContainer, "TriggerReleased", actionType);
            if (pressMi == null || releaseMi == null)
            {
                LastFail = $"missing trigger Pressed={pressMi != null} Released={releaseMi != null} action={actionType.Name}";
                return null;
            }

            var press = BindTrigger(keyBindingContainer, pressMi, actionType, maxAction);
            var release = BindTrigger(keyBindingContainer, releaseMi, actionType, maxAction);
            return new GameInputSink(press, release, maxAction);
        }
        catch (Exception ex)
        {
            LastFail = ex.GetBaseException().Message;
            return null;
        }
    }

    public static Action<int> BindTrigger(object container, MethodInfo method, Type actionType, int maxAction)
    {
        var param = Expression.Parameter(typeof(int), "action");
        var inRange = Expression.LessThan(
            Expression.Convert(param, typeof(uint)),
            Expression.Constant((uint)(maxAction + 1)));
        Expression call = Expression.Call(
            Expression.Constant(container),
            method,
            Expression.Convert(param, actionType));
        if (method.ReturnType != typeof(void))
            call = Expression.Block(typeof(void), call);
        return Expression.Lambda<Action<int>>(Expression.IfThen(inRange, call), param).Compile();
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
}
