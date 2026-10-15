using Maniac.Core;
using Maniac.Core.Input;
using Maniac.Core.Mania;
using Maniac.Core.Osu;
using Maniac.Core.Planning;
using Maniac.Core.Settings;
using Maniac.Core.Status;
using Maniac.Plugin.Adapters;
using osu.Game;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace Maniac.Plugin;

sealed class GameSession
{
    const int BindRetryInterval = 30;

    readonly SettingsStore settings;
    readonly ManiaDriver mania = new();
    readonly OsuDriver osu = new();
    readonly PlayPlanPipeline<ManiaPlayPlan> maniaPipeline = new();
    readonly PlayPlanPipeline<OsuPlayPlan> osuPipeline = new();

    Player? player;
    DrawableRuleset? drawable;
    GameInputSink? sink;
    string boundRuleset = "";
    object? lastScreen;
    bool chartLoaded;
    bool inputProbed;
    string lastError = "";
    int maniaSeed;
    ManiaHumanParams? maniaHumanKey;
    int bindFailStreak;

    public AutoplayStatus Status;
    public string LastError
    {
        get
        {
            if (!string.IsNullOrEmpty(lastError))
                return lastError;
            return settings.LastLoadError;
        }
    }

    IInputSink ActiveSink => sink ?? (IInputSink)NullInputSink.Instance;

    public GameSession(SettingsStore settings)
    {
        this.settings = settings;
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
        inputProbed = false;
        maniaPipeline.Clear();
        osuPipeline.Clear();
        maniaHumanKey = null;
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

        double time;
        try
        {
            var clock = drawable.FrameStableClock;
            if (clock == null)
            {
                Status.Diagnostic = "no clock";
                return;
            }

            time = clock.CurrentTime;
        }
        catch (Exception ex)
        {
            Status.Diagnostic = $"no clock: {ex.GetType().Name}";
            return;
        }
        Status.Time = time;
        bool blocked = drawable.IsPaused.Value
                       || drawable.HasReplayLoaded.Value
                       || state.HasCompleted;
        Status.Blocked = blocked;
        bool play = cfg.AutoplayEnabled && !blocked && GameBindings.LastProbe.Ok;

        if (ruleset.Equals("osu", StringComparison.OrdinalIgnoreCase))
        {
            // Features.OsuStandard is a compile-time flag - at the time of writing we set it
            // to false because the feature isn't fleshed out yet, but we wanted to already
            // release demo builds
#pragma warning disable CS0162
            if (Features.OsuStandard)
                TickOsu(state, play, cfg.Osu.Enabled);
            else
            {
                sink?.ReleaseHeld();
                Status.Bound = false;
                Status.Applied = 0;
                Status.Total = 0;
                Status.Columns = 0;
                Status.Diagnostic = "osu!standard is not available yet";
            }
#pragma warning restore CS0162
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

    void TickMania(GameplayState state, bool play, bool rulesetEnabled)
    {
        if (!chartLoaded || boundRuleset != "mania")
        {
            osu.Unload(ActiveSink);
            osuPipeline.Clear();
            maniaSeed = Random.Shared.Next();
            maniaPipeline.SetSource(ManiaAdapter.Build(state));
            maniaHumanKey = null;
            boundRuleset = "mania";
            chartLoaded = true;
        }

        TryBindInput(ManiaPlanner.MaxColumn);
        ProbeInputOnce();
        RebuildManiaIfNeeded(ManiaHumanParams.From(settings.Current.Mania));

        bool run = play && rulesetEnabled && sink != null;
        mania.Tick(Status.Time, ActiveSink, run);
        Status.Bound = sink != null;
        Status.Applied = mania.Applied;
        Status.Total = mania.HitCount;
        Status.Columns = mania.Columns;
        Status.Diagnostic = BindDiagnostic();
    }

    void TickOsu(GameplayState state, bool play, bool rulesetEnabled)
    {
        if (!chartLoaded || boundRuleset != "osu")
        {
            mania.Unload(ActiveSink);
            maniaPipeline.Clear();
            maniaHumanKey = null;
            var plan = osuPipeline.SetSource(OsuAdapter.Build(state), OsuTransforms());
            osu.Load(plan);
            osu.SnapTo(Status.Time, ActiveSink, play: false);
            boundRuleset = "osu";
            chartLoaded = true;
        }

        TryBindInput(1);
        ProbeInputOnce();

        if (sink != null && drawable != null && !sink.HasMouse)
            sink.BindMouse(GameBindings.InputManagerOf(drawable), drawable);

        bool run = play && rulesetEnabled && sink != null;
        osu.Tick(Status.Time, ActiveSink, run);
        Status.Bound = sink != null;
        Status.Applied = osu.Applied;
        Status.Total = osu.Total;
        Status.Columns = 0;
        Status.Diagnostic = BindDiagnostic();
    }

    void RebuildManiaIfNeeded(ManiaHumanParams key)
    {
        if (maniaHumanKey == key || !maniaPipeline.HasSource)
            return;

        var plan = maniaPipeline.Rebuild(ManiaTransforms(key, maniaSeed));
        mania.Load(plan);
        mania.SnapTo(Status.Time, ActiveSink, play: false);
        maniaHumanKey = key;
    }

    static IPlayPlanTransform<ManiaPlayPlan>[] ManiaTransforms(ManiaHumanParams key, int seed) =>
        [new ManiaHumanizer(key, seed)];

    static IPlayPlanTransform<OsuPlayPlan>[] OsuTransforms() => [];

    void TryBindInput(int maxAction)
    {
        if (sink != null)
        {
            bindFailStreak = 0;
            return;
        }

        if (bindFailStreak != 0 && bindFailStreak % BindRetryInterval != 0)
        {
            bindFailStreak++;
            return;
        }

        BindInput(maxAction);
        bindFailStreak = sink == null ? bindFailStreak + 1 : 0;
    }

    void ProbeInputOnce()
    {
        if (inputProbed || drawable == null)
            return;
        if (GameBindings.KeyBindingContainerOf(drawable) == null)
            return;
        inputProbed = true;
        GameBindings.ProbeInput(drawable);
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

    string BindDiagnostic()
    {
        if (sink != null)
            return "";
        if (!string.IsNullOrEmpty(GameInputSink.LastFail))
            return GameInputSink.LastFail;
        if (!GameBindings.LastInputProbe.Ok && GameBindings.LastInputProbe.Failures.Count > 0)
            return GameBindings.LastInputProbe.Failures[0];
        return "no KeyBindingContainer";
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
        inputProbed = false;
        bindFailStreak = 0;
        maniaPipeline.Clear();
        osuPipeline.Clear();
        maniaHumanKey = null;
        Status.Applied = 0;
        Status.Total = 0;
        Status.Columns = 0;
        Status.Time = 0;
        Status.Blocked = false;
    }

    static Player? FindPlayer(OsuGame game)
    {
        var screen = game.ScreenStack?.CurrentScreen;
        if (screen is Player p)
            return p;
        if (screen is PlayerLoader loader)
            return loader.CurrentPlayer;
        return null;
    }
}
