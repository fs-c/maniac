using System.Reflection;
using osu.Game;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace Maniac.Plugin;

/// <summary>
/// Reads the in-play <see cref="osu.Game.Beatmaps.IBeatmap"/> and sends mania
/// column actions through <c>KeyBindingContainer.TriggerPressed/Released</c>.
/// </summary>
static class ManiaAuto
{
    const double ReleaseDelay = 20;
    const int MaxColumn = 19;

    public static bool Enabled { get; private set; }
    public static string Status { get; private set; } = "idle";

    static Player? player;
    static object? bindings;
    static Type? actionType;
    static MethodInfo? triggerPressed;
    static MethodInfo? triggerReleased;
    static Event[] events = [];
    static bool chartLoaded;
    static int cursor;
    static readonly bool[] held = new bool[MaxColumn + 1];
    static int columns;
    static int applied;

    readonly record struct Event(double Time, int Column, bool Down);

    public static void Fail(Exception ex)
    {
        var inner = ex.GetBaseException();
        Status = $"tick: {inner.GetType().Name}: {inner.Message}";
    }

    public static void Toggle()
    {
        Enabled = !Enabled;
        if (!Enabled)
        {
            ReleaseAll();
            OsuAuto.Release();
        }
    }

    public static void Reset()
    {
        Enabled = false;
        ReleaseAll();
        OsuAuto.Reset();
        player = null;
        bindings = null;
        events = [];
        chartLoaded = false;
        cursor = 0;
        applied = 0;
        columns = 0;
        Status = "idle";
    }

    public static void Tick(OsuGame game)
    {
        var current = GameplayAccess.FindPlayer(game);
        if (current == null || current.GameplayState == null)
        {
            if (player != null)
                ResetPlay();

            string screen = GameplayAccess.ScreenName(game);
            string mode = Enabled ? "auto on" : "idle";
            if (current != null)
            {
                Status = $"{mode} · {current.GetType().Name} (loading)";
                return;
            }

            string? mismatch = GameplayAccess.PlayerTypeMismatch(game);
            Status = mismatch == null
                ? $"{mode} · {screen}"
                : $"{mode} · {mismatch} (type mismatch)";
            return;
        }

        if (!ReferenceEquals(current, player))
        {
            ReleaseAll();
            OsuAuto.Reset();
            player = current;
            bindings = null;
            triggerPressed = null;
            triggerReleased = null;
            actionType = null;
            events = [];
            chartLoaded = false;
            cursor = 0;
            applied = 0;
        }

        var state = player.GameplayState;
        string ruleset = state.Ruleset.ShortName;
        var drawable = GameplayAccess.DrawableRulesetOf(player);
        if (drawable == null)
        {
            Status = $"{ruleset} · no DrawableRuleset";
            return;
        }

        double time = drawable.FrameStableClock.CurrentTime;
        bool blocked = drawable.IsPaused.Value
                       || drawable.HasReplayLoaded.Value
                       || state.HasCompleted;

        if (ruleset.Equals("osu", StringComparison.OrdinalIgnoreCase))
        {
            ReleaseAll();
            Status = OsuAuto.Tick(drawable, state, Enabled && !blocked);
            return;
        }

        OsuAuto.Release();

        if (!ruleset.Equals("mania", StringComparison.OrdinalIgnoreCase))
        {
            ReleaseAll();
            Status = $"{ruleset} · unsupported";
            return;
        }

        if (!chartLoaded)
            BuildEvents(state);

        if (!Enabled || blocked)
        {
            ReleaseAll();
            Status = Describe(time, blocked);
            return;
        }

        bindings ??= GameplayAccess.KeyBindingContainerOf(drawable);
        if (bindings == null || !EnsureTriggers(bindings))
        {
            Status = "mania · no KeyBindingContainer";
            return;
        }

        if (time + 1 < (cursor > 0 ? events[cursor - 1].Time : 0))
        {
            ReleaseAll();
            cursor = 0;
            applied = 0;
        }

        while (cursor < events.Length && events[cursor].Time <= time)
        {
            Apply(events[cursor]);
            cursor++;
            applied++;
        }

        Status = Describe(time, blocked: false);
    }

    static string Describe(double time, bool blocked)
    {
        string mode = !Enabled ? "auto off" : blocked ? "auto paused" : "auto on";
        string keys = columns > 0 ? $"{columns}k" : "?k";
        return $"{mode} · {keys} {applied}/{events.Length} · t={time:0}";
    }

    static void BuildEvents(GameplayState state)
    {
        var objects = state.Beatmap.HitObjects
            .Where(h => h is IHasColumn)
            .OrderBy(h => h.StartTime)
            .ToList();

        columns = objects.Count == 0
            ? 0
            : objects.Max(h => ((IHasColumn)h).Column) + 1;

        var byColumn = objects
            .Select((h, i) => (Hit: h, Index: i))
            .ToLookup(x => ((IHasColumn)x.Hit).Column);

        var list = new List<Event>(objects.Count * 2);
        for (int i = 0; i < objects.Count; i++)
        {
            var hit = objects[i];
            int column = ((IHasColumn)hit).Column;
            if (column is < 0 or > MaxColumn)
                continue;

            double start = hit.StartTime;
            double end = hit.GetEndTime();
            double release = ReleaseTime(hit, end, NextInColumn(byColumn, column, i));
            list.Add(new Event(start, column, Down: true));
            list.Add(new Event(release, column, Down: false));
        }

        events = list
            .OrderBy(e => e.Time)
            .ThenBy(e => e.Down ? 0 : 1)
            .ToArray();
        cursor = 0;
        applied = 0;
        chartLoaded = true;
    }

    static HitObject? NextInColumn(ILookup<int, (HitObject Hit, int Index)> byColumn, int column, int index)
    {
        foreach (var (hit, i) in byColumn[column])
        {
            if (i > index)
                return hit;
        }

        return null;
    }

    static double ReleaseTime(HitObject current, double endTime, HitObject? next)
    {
        if (current is IHasDuration hold && hold.Duration > 0)
            return endTime;

        double delay = current is IHasDuration { Duration: 0 } ? 1 : ReleaseDelay;
        if (next == null || next.StartTime > endTime + delay)
            return endTime + delay;
        return endTime + (next.StartTime - endTime) * 0.9;
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

    static void Apply(Event e)
    {
        if (held[e.Column] == e.Down || bindings == null || actionType == null)
            return;

        held[e.Column] = e.Down;
        object action = Enum.ToObject(actionType, e.Column);
        (e.Down ? triggerPressed : triggerReleased)?.Invoke(bindings, [action]);
    }

    static void ReleaseAll()
    {
        if (bindings == null || actionType == null || triggerReleased == null)
        {
            Array.Clear(held);
            return;
        }

        for (int c = 0; c <= MaxColumn; c++)
        {
            if (!held[c])
                continue;
            held[c] = false;
            triggerReleased.Invoke(bindings, [Enum.ToObject(actionType, c)]);
        }
    }

    static void ResetPlay()
    {
        ReleaseAll();
        OsuAuto.Reset();
        player = null;
        bindings = null;
        events = [];
        chartLoaded = false;
        cursor = 0;
        applied = 0;
        columns = 0;
    }
}
