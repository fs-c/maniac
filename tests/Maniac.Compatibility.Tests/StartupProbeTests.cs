using System.Reflection;
using Maniac.Plugin;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;
using Xunit;

namespace Maniac.Compatibility.Tests;

public class StartupProbeTests
{
    [Fact]
    public void Required_game_members_resolve()
    {
        var probe = GameBindings.Probe();
        Assert.True(probe.Ok, string.Join(Environment.NewLine, probe.Failures));
        Assert.Contains(probe.Resolved, name => name.Contains("topMostOverlayContent", StringComparison.Ordinal));
        Assert.Contains(probe.Resolved, name => name.Contains("DrawableRuleset", StringComparison.Ordinal));
        Assert.Contains(probe.Resolved, name => name.Contains("UpdateFrame", StringComparison.Ordinal) || name.Contains("Root", StringComparison.Ordinal));
    }

    [Fact]
    public void Trigger_methods_are_bool_or_void()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = typeof(KeyBindingContainer<GlobalAction>);
        string dump = string.Join("; ", type.GetMethods(flags)
            .Where(m => m.Name is "TriggerPressed" or "TriggerReleased")
            .Select(m => $"{m.DeclaringType?.Name}.{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})->{m.ReturnType.Name}"));
        var pressed = type.GetMethods(flags).Where(m => m.Name == "TriggerPressed" && m.GetParameters() is { Length: 1 } p && p[0].ParameterType == typeof(GlobalAction)).ToArray();
        var released = type.GetMethods(flags).Where(m => m.Name == "TriggerReleased" && m.GetParameters() is { Length: 1 } p && p[0].ParameterType == typeof(GlobalAction)).ToArray();
        Assert.True(pressed.Length > 0, dump);
        Assert.True(released.Length > 0, dump);
        Assert.All(pressed, m => Assert.True(
            m.ReturnType == typeof(bool) || m.ReturnType == typeof(void) || typeof(Drawable).IsAssignableFrom(m.ReturnType),
            dump));
        Assert.All(released, m => Assert.True(
            m.ReturnType == typeof(bool) || m.ReturnType == typeof(void) || typeof(Drawable).IsAssignableFrom(m.ReturnType),
            dump));
    }

    [Fact]
    public void FindTrigger_invoke_works_for_drawable_and_void()
    {
        var box = new DummyContainer();
        var pressed = GameBindings.FindTrigger(box, "TriggerPressed", typeof(DummyAction));
        var released = GameBindings.FindTrigger(box, "TriggerReleased", typeof(DummyAction));
        Assert.NotNull(pressed);
        Assert.NotNull(released);
        pressed!.Invoke(box, [DummyAction.Key1]);
        released!.Invoke(box, [DummyAction.Key1]);
        Assert.Equal(1, box.Presses);
        Assert.Equal(1, box.Releases);
    }

    [Fact]
    public void BindTrigger_compiles_press_and_release()
    {
        var box = new DummyContainer();
        var pressed = GameBindings.FindTrigger(box, "TriggerPressed", typeof(DummyAction));
        var released = GameBindings.FindTrigger(box, "TriggerReleased", typeof(DummyAction));
        var press = GameInputSink.BindTrigger(box, pressed!, typeof(DummyAction), 1);
        var release = GameInputSink.BindTrigger(box, released!, typeof(DummyAction), 1);
        press(0);
        release(0);
        press(8);
        Assert.Equal(1, box.Presses);
        Assert.Equal(1, box.Releases);
    }

    enum DummyAction { Key1 }

    sealed class DummyContainer
    {
        public int Presses;
        public int Releases;
        public Drawable TriggerPressed(DummyAction action) { Presses++; return null!; }
        public void TriggerReleased(DummyAction action) => Releases++;
    }
}
