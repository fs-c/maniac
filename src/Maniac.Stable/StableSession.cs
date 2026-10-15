using System.Diagnostics;
using Maniac.Core;
using Maniac.Core.Input;
using Maniac.Core.Mania;
using Maniac.Core.Planning;
using Maniac.Core.Settings;

namespace Maniac.Stable;

public sealed class StableSession : IDisposable
{
    readonly SettingsStore settings;
    readonly CancellationTokenSource cts = new();
    readonly Thread thread;
    readonly object gate = new();
    readonly ManiaDriver driver = new();
    readonly PlayPlanPipeline<ManiaPlayPlan> pipeline = new();

    IInputSink sink = NullInputSink.Instance;
    bool chartLoaded;
    int maniaSeed;
    ManiaHumanParams? maniaHumanKey;
    string keys = "";
    StablePlayStatus status;

    public StableSession(SettingsStore settings)
    {
        this.settings = settings;
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "stable-play",
        };
        thread.Start();
    }

    public StablePlayStatus Snapshot()
    {
        lock (gate)
            return status;
    }

    public void Dispose()
    {
        cts.Cancel();
        if (!thread.Join(TimeSpan.FromSeconds(15)))
            throw new InvalidOperationException("stable play thread did not stop.");
        cts.Dispose();
    }

    void Run()
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                if (!OsuProcess.TryFind(out var found, out string? findError) || found == null)
                {
                    ResetPlay();
                    Publish(new StablePlayStatus(
                        Attached: false,
                        LayoutOk: false,
                        Pid: 0,
                        Enabled: settings.Current.AutoplayEnabled,
                        Playing: false,
                        Paused: false,
                        Foreground: false,
                        Ruleset: "",
                        Screen: "",
                        Time: 0,
                        Applied: 0,
                        Total: 0,
                        Columns: 0,
                        Keys: "",
                        Diagnostic: findError ?? "waiting for osu!stable"));
                    Wait(500);
                    continue;
                }

                using (found)
                {
                    try
                    {
                        using var game = new StableGame(found.Id);
                        while (!cts.IsCancellationRequested && !HasExited(found))
                        {
                            Tick(game, found);
                            Wait(1);
                        }
                    }
                    catch (Exception ex) when (!cts.IsCancellationRequested)
                    {
                        ResetPlay();
                        PublishError(found.Id, ex, attached: false, layoutOk: false);
                        Wait(500);
                    }
                }

                ResetPlay();
            }
        }
        finally
        {
            ResetPlay();
        }
    }

    void Tick(StableGame game, Process process)
    {
        var cfg = settings.Current;
        bool expired = BuildExpiry.Current.IsExpired();
        if (expired && cfg.AutoplayEnabled)
            settings.Update(s => s.AutoplayEnabled = false);

        try
        {
            var frame = game.ReadStatus();
            bool focused = OsuProcess.IsForeground(process);
            bool onMap = frame.OsuMode == StableLayout.OsuModePlay
                         && frame.PlayMode == StableLayout.PlayModeMania;

            if (!onMap)
            {
                if (chartLoaded)
                    ResetPlay();
            }
            else if (!chartLoaded)
            {
                LoadChart(game, frame);
            }

            if (chartLoaded)
                RebuildManiaIfNeeded(ManiaHumanParams.From(cfg.Mania), frame.AudioTime);

            bool run = cfg.AutoplayEnabled
                       && cfg.Mania.Enabled
                       && !expired
                       && onMap
                       && !frame.Paused
                       && focused
                       && driver.HitCount > 0
                       && sink is SendInputSink;
            driver.Tick(frame.AudioTime, sink, run);

            Publish(new StablePlayStatus(
                Attached: true,
                LayoutOk: true,
                Pid: process.Id,
                Enabled: cfg.AutoplayEnabled && !expired,
                Playing: run,
                Paused: frame.Paused,
                Foreground: focused,
                Ruleset: RulesetName(frame.PlayMode),
                Screen: ScreenName(frame.OsuMode),
                Time: frame.AudioTime,
                Applied: driver.Applied,
                Total: driver.HitCount,
                Columns: driver.Columns,
                Keys: keys,
                Diagnostic: Diagnostic(cfg, expired, onMap, focused, frame, settings.LastLoadError)));
        }
        catch (Exception ex)
        {
            sink.ReleaseHeld();
            PublishError(process.Id, ex, attached: true, layoutOk: true);
        }
    }

    void LoadChart(StableGame game, StableGame.Frame frame)
    {
        var notes = game.LoadNotes();
        if (notes.Length == 0)
            return;

        var source = StableGame.ToPlan(frame with { Notes = notes });
        maniaSeed = Random.Shared.Next();
        maniaHumanKey = null;
        var plan = pipeline.SetSource(source, ManiaTransforms(ManiaHumanParams.From(settings.Current.Mania), maniaSeed));
        var map = game.ReadManiaKeys(plan.Columns);
        if (map.VirtualKeys.Length != plan.Columns)
            throw new InvalidOperationException($"key map '{map.Spec}' has {map.VirtualKeys.Length} keys but the map is {plan.Columns}K.");

        sink.ReleaseHeld();
        sink = new SendInputSink(map.VirtualKeys, map.Spec);
        keys = map.Spec;
        driver.Load(plan);
        driver.SnapTo(frame.AudioTime, sink, play: false);
        maniaHumanKey = ManiaHumanParams.From(settings.Current.Mania);
        chartLoaded = true;
    }

    void RebuildManiaIfNeeded(ManiaHumanParams key, double time)
    {
        if (maniaHumanKey == key || !pipeline.HasSource)
            return;

        var plan = pipeline.Rebuild(ManiaTransforms(key, maniaSeed));
        driver.Load(plan);
        driver.SnapTo(time, sink, play: false);
        maniaHumanKey = key;
    }

    static IPlayPlanTransform<ManiaPlayPlan>[] ManiaTransforms(ManiaHumanParams key, int seed) =>
        [new ManiaHumanizer(key, seed)];

    void ResetPlay()
    {
        sink.ReleaseHeld();
        driver.Unload(sink);
        sink = NullInputSink.Instance;
        pipeline.Clear();
        chartLoaded = false;
        maniaHumanKey = null;
        keys = "";
    }

    void Publish(in StablePlayStatus next)
    {
        lock (gate)
            status = next;
    }

    void PublishError(int pid, Exception ex, bool attached, bool layoutOk)
    {
        var inner = ex.GetBaseException();
        string message = $"{inner.GetType().Name}: {inner.Message}";
        var current = Snapshot();
        Publish(current with
        {
            Attached = attached,
            LayoutOk = layoutOk && attached,
            Pid = pid,
            Playing = false,
            Diagnostic = message,
        });
    }

    static string Diagnostic(ManiacSettings cfg, bool expired, bool onMap, bool focused, StableGame.Frame frame, string settingsError)
    {
        if (expired)
            return BuildExpiry.Current.Describe(DateTimeOffset.UtcNow);
        if (!string.IsNullOrEmpty(settingsError))
            return "settings " + settingsError;
        if (!cfg.AutoplayEnabled)
            return "autoplay off";
        if (!cfg.Mania.Enabled)
            return "mania driver off";
        if (frame.PlayMode >= 0 && frame.PlayMode != StableLayout.PlayModeMania && frame.OsuMode == StableLayout.OsuModePlay)
            return "unsupported ruleset";
        if (!onMap)
            return "";
        if (frame.Paused)
            return "paused";
        if (!focused)
            return "osu! is not in the foreground; input withheld";
        return "";
    }

    static string RulesetName(int playMode) => playMode switch
    {
        StableLayout.PlayModeOsu => "osu",
        StableLayout.PlayModeTaiko => "taiko",
        StableLayout.PlayModeCatch => "catch",
        StableLayout.PlayModeMania => "mania",
        -1 => "",
        _ => playMode.ToString()
    };

    static string ScreenName(int osuMode) => osuMode == StableLayout.OsuModePlay ? "playing" : $"mode {osuMode}";

    static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    void Wait(int milliseconds)
    {
        cts.Token.WaitHandle.WaitOne(milliseconds);
    }
}
