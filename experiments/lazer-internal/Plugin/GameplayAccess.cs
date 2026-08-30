using System.Collections;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace Maniac.Plugin;

static class GameplayAccess
{
    static readonly FieldInfo? stackField =
        typeof(ScreenStack).GetField("stack", BindingFlags.Instance | BindingFlags.NonPublic);

    public static string ScreenName(OsuGame game)
    {
        object? current = game.ScreenStack?.CurrentScreen;
        return current == null ? "no screen" : current.GetType().Name;
    }

    public static string? PlayerTypeMismatch(OsuGame game)
    {
        foreach (object? item in Candidates(game))
        {
            if (item is null || item is Player || !IsPlayerType(item.GetType()))
                continue;
            return item.GetType().FullName;
        }

        return null;
    }

    public static Player? FindPlayer(OsuGame game)
    {
        foreach (object? item in Candidates(game))
        {
            if (item is Player player)
                return player;
        }

        return null;
    }

    static IEnumerable<object?> Candidates(OsuGame game)
    {
        var screens = game.ScreenStack;
        if (screens == null)
            yield break;

        yield return screens.CurrentScreen;

        foreach (object? item in StackItems(screens))
        {
            yield return item;
            if (item is PlayerLoader loader)
                yield return loader.CurrentPlayer;
        }
    }

    static object?[] StackItems(ScreenStack screens)
    {
        object? stack = stackField?.GetValue(screens);
        if (stack is not IEnumerable list)
            return [];

        var items = new List<object?>();
        foreach (object? item in list)
            items.Add(item);
        return items.ToArray();
    }

    static bool IsPlayerType(Type type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.FullName == "osu.Game.Screens.Play.Player")
                return true;
        }

        return false;
    }

    public static DrawableRuleset? DrawableRulesetOf(Player player) =>
        Member(player, "DrawableRuleset") as DrawableRuleset;

    public static object? KeyBindingContainerOf(DrawableRuleset ruleset)
    {
        object? manager = Member(ruleset, "KeyBindingInputManager");
        return manager == null ? null : Member(manager, "KeyBindingContainer");
    }

    public static InputManager? InputManagerOf(Drawable drawable)
    {
        var method = typeof(Drawable).GetMethod(
            "GetContainingInputManager",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return method?.Invoke(drawable, null) as InputManager;
    }

    static object? Member(object obj, string name)
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var prop = type.GetProperty(name, flags);
            if (prop != null)
                return prop.GetValue(obj);

            var field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(obj);
        }

        return null;
    }
}
