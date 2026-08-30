using System.Reflection;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osuTK;

namespace Maniac.Plugin;

/// <summary>
/// osu!standard auto: cursor follows <see cref="IHasPosition"/> objects,
/// sliders via <see cref="IHasPathWithRepeats"/>, spinners by spinning
/// around the playfield centre. Clicks go through
/// <c>KeyBindingContainer.TriggerPressed</c> (LeftButton=0, RightButton=1).
/// </summary>
static class OsuAuto
{
    const double KeyUpDelay = 50;
    const double AlternateGap = 266;
    const double Reaction = 100;
    const float SpinRadius = 50;
    const float SpinRadPerMs = 0.05f;
    static readonly Vector2 SpinnerCentre = new(256, 192);

    public static string Status { get; private set; } = "idle";

    static Note[] notes = [];
    static bool loaded;
    static object? bindings;
    static Type? actionType;
    static MethodInfo? triggerPressed;
    static MethodInfo? triggerReleased;
    static bool left;
    static bool right;
    static double lastTime;
    static int applied;

    readonly record struct Note(
        double Start,
        double End,
        double Preempt,
        Vector2 StartPos,
        bool Slider,
        bool Spinner,
        int Button,
        HitObject Source);

    public static void Reset()
    {
        Release();
        notes = [];
        loaded = false;
        bindings = null;
        actionType = null;
        triggerPressed = null;
        triggerReleased = null;
        lastTime = 0;
        applied = 0;
        Status = "idle";
    }

    public static void Release()
    {
        SetButton(0, false);
        SetButton(1, false);
    }

    public static string Tick(DrawableRuleset drawable, GameplayState state, bool play)
    {
        if (!loaded)
            Load(state);

        double time = drawable.FrameStableClock.CurrentTime;
        if (time + 1 < lastTime)
        {
            Release();
            applied = 0;
        }

        lastTime = time;
        applied = 0;
        for (int i = 0; i < notes.Length; i++)
        {
            if (notes[i].Start <= time)
                applied = i + 1;
        }

        if (!play)
        {
            Release();
            return Describe(time, blocked: true);
        }

        bindings ??= GameplayAccess.KeyBindingContainerOf(drawable);
        if (bindings == null || !EnsureTriggers(bindings))
            return "osu · no KeyBindingContainer";

        var im = GameplayAccess.InputManagerOf(drawable);
        if (im == null)
            return "osu · no InputManager";

        Vector2 pos = PositionAt(time);
        GameInput.MoveMouse(im, drawable.Playfield.GamefieldToScreenSpace(pos));
        ApplyButtons(time);
        return Describe(time, blocked: false);
    }

    static void Load(GameplayState state)
    {
        var objects = state.Beatmap.HitObjects.OrderBy(h => h.StartTime).ToList();
        notes = new Note[objects.Count];
        int button = 0;
        double prevStart = double.NegativeInfinity;

        for (int i = 0; i < objects.Count; i++)
        {
            var hit = objects[i];
            bool slider = hit is IHasPathWithRepeats;
            bool spinner = hit is IHasDuration d && d.Duration > 0 && !slider;
            double start = hit.StartTime;
            double end = hit.GetEndTime();
            double preempt = hit is IHasTimePreempt tp ? tp.TimePreempt : 600;

            if (start - prevStart >= 0 && start - prevStart < AlternateGap)
                button = 1 - button;
            else
                button = 0;

            notes[i] = new Note(start, end, preempt, StackedPos(hit), slider, spinner, button, hit);
            prevStart = start;
        }

        loaded = true;
        applied = 0;
    }

    static Vector2 PositionAt(double time)
    {
        Note? lasting = null;
        Note? tapping = null;
        Note? next = null;
        Note? prev = null;

        for (int i = 0; i < notes.Length; i++)
        {
            var n = notes[i];
            if (n.Start <= time)
                prev = n;
            if (n.Start <= time && time <= n.End && (n.Slider || n.Spinner))
                lasting = n;
            if (!n.Slider && !n.Spinner && n.Start <= time && time <= n.Start + KeyUpDelay)
                tapping = n;
            if (next == null && n.Start > time)
                next = n;
        }

        if (lasting != null)
            return Follow(lasting.Value, time);
        if (tapping != null)
            return tapping.Value.StartPos;
        if (next == null)
            return prev == null ? SpinnerCentre : Follow(prev.Value, prev.Value.End);

        Vector2 from = prev == null ? SpinnerCentre : Follow(prev.Value, prev.Value.End);
        double moveStart = next.Value.Start - Math.Max(0, next.Value.Preempt - Reaction);
        if (time <= moveStart || next.Value.Start <= moveStart)
            return from;

        float t = (float)((time - moveStart) / (next.Value.Start - moveStart));
        t = Math.Clamp(t, 0, 1);
        t = 1 - (1 - t) * (1 - t);
        return from + (next.Value.StartPos - from) * t;
    }

    static Vector2 Follow(Note n, double time)
    {
        if (n.Spinner)
        {
            float angle = (float)((Math.Clamp(time, n.Start, n.End) - n.Start) * SpinRadPerMs);
            return SpinnerCentre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * SpinRadius;
        }

        if (n.Slider && n.Source is IHasPathWithRepeats path && n.End > n.Start)
        {
            double progress = Math.Clamp((time - n.Start) / (n.End - n.Start), 0, 1);
            return n.StartPos + path.CurvePositionAt(progress);
        }

        return n.StartPos;
    }

    static Vector2 StackedPos(HitObject hit)
    {
        var prop = hit.GetType().GetProperty("StackedPosition");
        if (prop?.GetValue(hit) is Vector2 stacked)
            return stacked;
        return hit is IHasPosition p ? p.Position : SpinnerCentre;
    }

    static void ApplyButtons(double time)
    {
        bool wantLeft = false;
        bool wantRight = false;
        for (int i = 0; i < notes.Length; i++)
        {
            var n = notes[i];
            if (time < n.Start || time >= n.End + KeyUpDelay)
                continue;
            if (n.Button == 0)
                wantLeft = true;
            else
                wantRight = true;
        }

        SetButton(0, wantLeft);
        SetButton(1, wantRight);
    }

    static void SetButton(int button, bool down)
    {
        ref bool held = ref button == 0 ? ref left : ref right;
        if (held == down)
            return;
        if (bindings == null || actionType == null)
            return;

        held = down;
        object action = Enum.ToObject(actionType, button);
        (down ? triggerPressed : triggerReleased)?.Invoke(bindings, [action]);
    }

    static string Describe(double time, bool blocked)
    {
        string mode = !ManiaAuto.Enabled ? "auto off" : blocked ? "auto paused" : "auto on";
        return $"{mode} · osu {applied}/{notes.Length} · t={time:0}";
    }

    static bool EnsureTriggers(object kbc)
    {
        if (triggerPressed != null && triggerReleased != null && actionType != null)
            return true;

        actionType = ActionTypeOf(kbc);
        if (actionType == null)
            return false;

        triggerPressed = FindTrigger(kbc, "TriggerPressed", actionType);
        triggerReleased = FindTrigger(kbc, "TriggerReleased", actionType);
        return triggerPressed != null && triggerReleased != null;
    }

    static Type? ActionTypeOf(object kbc)
    {
        for (var type = kbc.GetType(); type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.Name.Contains("KeyBindingContainer", StringComparison.Ordinal))
                return type.GetGenericArguments()[0];
        }

        return null;
    }

    static MethodInfo? FindTrigger(object kbc, string name, Type action)
    {
        for (var type = kbc.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == name && m.GetParameters() is { Length: 1 } p && p[0].ParameterType == action);
            if (method != null)
                return method;
        }

        return null;
    }
}
