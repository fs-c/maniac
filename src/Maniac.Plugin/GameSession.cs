using Maniac.Core.Input;
using Maniac.Core.Mania;
using Maniac.Core.Osu;
using Maniac.Core.Planning;
using Maniac.Core.Settings;
using Maniac.Core.Status;
using Maniac.Plugin.Adapters;
using osu.Framework.Input;
using osu.Game;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace Maniac.Plugin;

sealed class GameSession
{
    readonly SettingsStore settings;
    readonly ManiaDriver mania = new();
    readonly OsuDriver osu = new();
    readonly IReadOnlyList<IPlayPlanTransform<ManiaPlayPlan>> maniaTransforms;
    readonly IReadOnlyList<IPlayPlanTransform<OsuPlayPlan>> osuTransforms;

    Player? player;
    DrawableRuleset? drawable;
    GameInputSink? sink;
    string boundRuleset = "";
    object? lastScreen;
    bool chartLoaded;
    string lastError = "";

    public AutoplayStatus Status;
    public string LastError => lastError;
    IInputSink ActiveSink => sink ?? (IInputSink)NullInputSink.Instance;

    public GameSession(SettingsStore settings)
    {
        this.settings = settings;
        maniaTransforms = [];
        osuTransforms = [];
        Status.ProbeOk = GameBindings.LastProbe.Ok;
    }

    public void Stop()
    {
        sink?.ReleaseHeld();
        mania.Unload(ActiveSink);
        osu.Unload(ActiveSink);
        player = null;
        drawable = null;
        sink = null;
        chartLoaded = false;
        boundRuleset = "";
        lastScreen = null;
    }

    public void SetError(Exception ex)
    {
        var inner = ex.GetBaseException();
        lastError = $"{inner.GetType().Name}: {inner.Message}";
        Status.Diagnostic = lastError;
        sink?.ReleaseHeld();
    }

    public void Tick(OsuGame game)
    {
        var cfg = settings.Current;
        Status.Enabled = cfg.AutoplayEnabled;
        Status.ProbeOk = GameBindings.LastProbe.Ok;

        object? screen = game.ScreenStack?.CurrentScreen;
        if (!ReferenceEquals(screen, lastScreen))
        {
            lastScreen = screen;
            Status.Screen = screen == null ? "no screen" : screen.GetType().Name;
        }

        var current = FindPlayer(game);
        if (current == null || current.GameplayState == null)
        {
            if (player != null)
                ResetPlay();
            Status.Bound = false;
            Status.Ruleset = "";
            return;
        }

        if (!ReferenceEquals(current, player))
        {
            ResetPlay();
            player = current;
        }

        var state = player.GameplayState;
        string ruleset = state.Ruleset.ShortName;
        Status.Ruleset = ruleset;

        drawable ??= GameBindings.DrawableRulesetOf(player) as DrawableRuleset;
        if (drawable == null)
        {
            Status.Diagnostic = "no DrawableRuleset";
            return;
        }

        double time = drawable.FrameStableClock.CurrentTime;
        Status.Time = time;
        bool blocked = drawable.IsPaused.Value
                       || drawable.HasReplayLoaded.Value
                       || state.HasCompleted;
        Status.Blocked = blocked;
        bool play = cfg.AutoplayEnabled && !blocked && GameBindings.LastProbe.Ok;

        if (ruleset.Equals("osu", StringComparison.OrdinalIgnoreCase))
        {
            TickOsu(state, play, cfg.Osu.Enabled);
            return;
        }

        if (ruleset.Equals("mania", StringComparison.OrdinalIgnoreCase))
        {
            TickMania(state, play, cfg.Mania.Enabled);
            return;
        }

        sink?.ReleaseHeld();
        Status.Diagnostic = "unsupported ruleset";
        Status.Applied = 0;
        Status.Total = 0;
    }

    void TickMania(osu.Game.Screens.Play.GameplayState state, bool play, bool rulesetEnabled)
    {
        osu.Unload(ActiveSink);
        if (!chartLoaded || boundRuleset != "mania")
        {
            BindInput(ManiaPlanner.MaxColumn);
            var plan = PlayPlanTransforms.ApplyAll(ManiaAdapter.Build(state), maniaTransforms);
            mania.Load(plan);
            boundRuleset = "mania";
            chartLoaded = true;
        }

        bool run = play && rulesetEnabled && sink != null;
        mania.Tick(Status.Time, ActiveSink, run);
        Status.Bound = sink != null;
        Status.Applied = mania.Applied;
        Status.Total = mania.HitCount;
        Status.Columns = mania.Columns;
        Status.Diagnostic = sink == null ? "no KeyBindingContainer" : "";
    }

    void TickOsu(osu.Game.Screens.Play.GameplayState state, bool play, bool rulesetEnabled)
    {
        mania.Unload(ActiveSink);
        if (!chartLoaded || boundRuleset != "osu")
        {
            BindInput(1);
            var plan = PlayPlanTransforms.ApplyAll(OsuAdapter.Build(state), osuTransforms);
            osu.Load(plan);
            boundRuleset = "osu";
            chartLoaded = true;
        }

        if (sink != null && drawable != null && !sink.HasMouse)
            sink.BindMouse(GameBindings.InputManagerOf(drawable), drawable);

        bool run = play && rulesetEnabled && sink != null;
        osu.Tick(Status.Time, ActiveSink, run);
        Status.Bound = sink != null;
        Status.Applied = osu.Applied;
        Status.Total = osu.Total;
        Status.Columns = 0;
        Status.Diagnostic = sink == null ? "no KeyBindingContainer" : "";
    }

    void BindInput(int maxAction)
    {
        sink?.ReleaseHeld();
        sink = null;
        if (drawable == null)
            return;
        object? kbc = GameBindings.KeyBindingContainerOf(drawable);
        if (kbc == null)
            return;
        sink = GameInputSink.TryCreate(kbc, maxAction);
        if (sink != null)
            sink.BindMouse(GameBindings.InputManagerOf(drawable), drawable);
    }

    void ResetPlay()
    {
        sink?.ReleaseHeld();
        mania.Unload(NullInputSink.Instance);
        osu.Unload(NullInputSink.Instance);
        player = null;
        drawable = null;
        sink = null;
        chartLoaded = false;
        boundRuleset = "";
        Status.Applied = 0;
        Status.Total = 0;
        Status.Columns = 0;
    }

    static Player? FindPlayer(OsuGame game)
    {
        var screen = game.ScreenStack?.CurrentScreen;
        if (screen is Player player)
            return player;
        if (screen is PlayerLoader loader)
            return loader.CurrentPlayer;
        return null;
    }
}
