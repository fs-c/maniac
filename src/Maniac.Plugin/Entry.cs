using Maniac.Core;
using Maniac.Core.Settings;
using Maniac.Plugin.Ui;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osuTK.Input;

namespace Maniac.Plugin;

public static class Entry
{
    static OsuGame? game;
    static Container? overlay;
    static ManiacShell? panel;
    static GameSession? session;
    static SettingsStore? settings;
    static InputManager? input;
    static bool panelHotkeyDown;
    static bool autoHotkeyDown;

    public static void Start()
    {
        settings = new SettingsStore();
        if (BuildExpiry.Current.IsExpired())
            settings.Update(s => s.AutoplayEnabled = false);
        var probe = GameBindings.Probe();
        session = new GameSession(settings);
        Session.SetTick(OnTick);
        Logger.Log(probe.Ok
            ? "maniac: plugin started."
            : "maniac: plugin started with failed probe; autoplay disabled.");
    }

    public static void Stop()
    {
        Session.SetTick(null);
        session?.Stop();
        if (overlay != null && panel != null)
        {
            overlay.Remove(panel, true);
            overlay = null;
            panel = null;
        }

        game = null;
        input = null;
        session = null;
        settings = null;
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
            if (game == null || session == null || settings == null)
                return;

            if (panel == null)
            {
                overlay = GameBindings.OverlayOf(game);
                if (overlay == null)
                    return;
                panel = new ManiacShell(settings, () => session.Status, () => session.LastError);
                overlay.Add(panel);
                Logger.Log("maniac: overlay attached.");
            }

            input ??= GameBindings.InputManagerOf(game) ?? GameBindings.InputManagerOf(panel);
            HandleHotkeys();
            session.Tick(game);
        }
        catch (Exception ex)
        {
            LogTick(ex);
            session?.SetError(ex);
        }
    }

    static void HandleHotkeys()
    {
        if (input == null || settings == null || panel == null)
            return;

        if (Hotkeys.TryParse(settings.Current.PanelHotkey, out Key panelKey)
            && Hotkeys.WasPressed(input, panelKey, ref panelHotkeyDown))
            panel.ToggleVisible();

        if (Hotkeys.TryParse(settings.Current.AutoplayHotkey, out Key autoKey)
            && Hotkeys.WasPressed(input, autoKey, ref autoHotkeyDown))
            panel.ToggleAuto();
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

    static OsuGame? FindOsuGame(GameHost host)
    {
        var root = GameBindings.RootOf(host);
        return root == null ? null : FindOsuGame(root);
    }

    static OsuGame? FindOsuGame(Drawable drawable)
    {
        if (drawable is OsuGame found)
            return found;
        if (drawable is not CompositeDrawable composite)
            return null;

        var children = GameBindings.InternalChildrenOf(composite);
        if (children == null)
            return null;

        foreach (object? child in children)
        {
            if (child is not Drawable d)
                continue;
            var nested = FindOsuGame(d);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
