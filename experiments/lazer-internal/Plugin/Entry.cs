using System.Collections;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;

namespace Maniac.Plugin;

public static class Entry
{
    static OsuGame? game;
    static Container? overlay;
    static Drawable? panel;

    public static void Start()
    {
        Session.SetTick(OnTick);
        Logger.Log("maniac: plugin started.");
    }

    public static void Stop()
    {
        Session.SetTick(null);
        ManiaAuto.Reset();
        if (overlay != null && panel != null)
        {
            overlay.Remove(panel, true);
            overlay = null;
            panel = null;
        }

        game = null;
        Logger.Log("maniac: plugin stopped.");
    }

    static void OnTick(GameHost host)
    {
        try
        {
            int cmd = Session.ConsumePending();
            if (cmd is Session.Unload or Session.Reload)
            {
                Session.PrepareContinue(cmd);
                var next = host.UpdateThread?.Scheduler;
                Stop();
                next?.Add(Session.ContinueAfterStop);
                return;
            }

            game ??= FindOsuGame(host);
            if (game == null)
                return;

            if (panel == null)
            {
                overlay = OverlayOf(game);
                if (overlay == null)
                    return;
                panel = new ManiacPanel();
                overlay.Add(panel);
                Logger.Log("maniac: overlay attached.");
            }

            ManiaAuto.Tick(game);
        }
        catch (Exception ex)
        {
            LogTick(ex);
            ManiaAuto.Fail(ex);
        }
    }

    static string? lastTickLog;
    static long lastTickLogAt;

    static void LogTick(Exception ex)
    {
        string text = ex.ToString();
        long now = DateTime.UtcNow.Ticks;
        if (text == lastTickLog && now - lastTickLogAt < TimeSpan.FromSeconds(30).Ticks)
            return;
        lastTickLog = text;
        lastTickLogAt = now;

        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "bootstrap.log"), $"{DateTime.Now:O} {ex}\n");
        }
        catch
        {
            // nowhere to write
        }
    }

    static Container? OverlayOf(OsuGame game) =>
        typeof(OsuGame)
            .GetField("topMostOverlayContent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(game) as Container;

    static OsuGame? FindOsuGame(GameHost host)
    {
        var root = typeof(GameHost)
            .GetProperty("Root", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(host) as Drawable;
        return root == null ? null : FindOsuGame(root);
    }

    static OsuGame? FindOsuGame(Drawable drawable)
    {
        if (drawable is OsuGame game)
            return game;
        if (drawable is not CompositeDrawable)
            return null;

        var children = typeof(CompositeDrawable)
            .GetField("internalChildren", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(drawable) as IEnumerable;
        if (children == null)
            return null;

        foreach (object? child in children)
        {
            if (child is not Drawable d)
                continue;
            var found = FindOsuGame(d);
            if (found != null)
                return found;
        }

        return null;
    }
}
