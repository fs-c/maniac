using System.Collections;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Screens.Play;

namespace Maniac.Plugin;

public sealed class ProbeResult
{
    public bool Ok => Failures.Count == 0;
    public List<string> Failures { get; } = [];
    public List<string> Resolved { get; } = [];
}

/// <summary>
/// Single reflection boundary. Accessors are resolved once at startup.
/// </summary>
public static class GameBindings
{
    public static ProbeResult LastProbe { get; private set; } = new();

    static PropertyInfo? hostRoot;
    static FieldInfo? internalChildren;
    static FieldInfo? topMostOverlay;
    static FieldInfo? screenStackField;
    static PropertyInfo? drawableRuleset;
    static MethodInfo? getContainingInputManager;

    public static ProbeResult Probe()
    {
        var result = new ProbeResult();
        hostRoot = Require(result, typeof(GameHost), "Root", isProperty: true, required: true);
        internalChildren = RequireField(result, typeof(CompositeDrawable), "internalChildren", required: true);
        topMostOverlay = RequireField(result, typeof(OsuGame), "topMostOverlayContent", required: true);
        screenStackField = RequireField(result, typeof(ScreenStack), "stack", required: false);
        drawableRuleset = Require(result, typeof(Player), "DrawableRuleset", isProperty: true, required: true);
        getContainingInputManager = RequireMethod(result, typeof(Drawable), "GetContainingInputManager", required: true);
        _ = RequireMethod(result, typeof(GameHost), "UpdateFrame", required: true);
        LastProbe = result;
        return result;
    }

    public static Drawable? RootOf(GameHost host) => hostRoot?.GetValue(host) as Drawable;

    public static IEnumerable? InternalChildrenOf(CompositeDrawable drawable) =>
        internalChildren?.GetValue(drawable) as IEnumerable;

    public static Container? OverlayOf(OsuGame game) =>
        topMostOverlay?.GetValue(game) as Container;

    public static object? DrawableRulesetOf(Player player) => drawableRuleset?.GetValue(player);

    public static InputManager? InputManagerOf(Drawable drawable) =>
        getContainingInputManager?.Invoke(drawable, null) as InputManager;

    public static IEnumerable<object?> ScreenStackItems(ScreenStack screens)
    {
        object? stack = screenStackField?.GetValue(screens);
        if (stack is not IEnumerable list)
            yield break;
        foreach (object? item in list)
            yield return item;
    }

    public static object? Member(object obj, string name)
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

    public static Type? KeyBindingActionType(object keyBindingContainer)
    {
        for (var type = keyBindingContainer.GetType(); type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.Name.Contains("KeyBindingContainer", StringComparison.Ordinal))
                return type.GetGenericArguments()[0];
        }

        return null;
    }

    public static MethodInfo? FindTrigger(object keyBindingContainer, string name, Type actionType)
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (var type = keyBindingContainer.GetType(); type != null; type = type.BaseType)
        {
            foreach (var method in type.GetMethods(flags))
            {
                if (method.Name != name)
                    continue;
                var args = method.GetParameters();
                if (args.Length == 1 && args[0].ParameterType == actionType)
                    return method;
            }
        }

        return null;
    }

    public static object? KeyBindingContainerOf(object drawableRuleset)
    {
        object? manager = Member(drawableRuleset, "KeyBindingInputManager");
        return manager == null ? null : Member(manager, "KeyBindingContainer");
    }

    static PropertyInfo? Require(ProbeResult result, Type type, string name, bool isProperty, bool required)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var member = isProperty ? (MemberInfo?)type.GetProperty(name, flags) : type.GetField(name, flags);
        if (member != null)
        {
            result.Resolved.Add($"{type.Name}.{name}");
            return member as PropertyInfo;
        }

        if (required)
            result.Failures.Add($"missing {(isProperty ? "property" : "field")} {type.FullName}.{name}");
        return null;
    }

    static FieldInfo? RequireField(ProbeResult result, Type type, string name, bool required)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var field = type.GetField(name, flags);
        if (field != null)
        {
            result.Resolved.Add($"{type.Name}.{name}");
            return field;
        }

        if (required)
            result.Failures.Add($"missing field {type.FullName}.{name}");
        return null;
    }

    static MethodInfo? RequireMethod(ProbeResult result, Type type, string name, bool required)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var method = type.GetMethod(name, flags, binder: null, types: Type.EmptyTypes, modifiers: null)
                     ?? type.GetMethod(name, flags);
        if (method != null)
        {
            result.Resolved.Add($"{type.Name}.{name}");
            return method;
        }

        if (required)
            result.Failures.Add($"missing method {type.FullName}.{name}");
        return null;
    }
}
