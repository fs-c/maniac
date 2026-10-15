using Maniac.Core.Mania;

namespace Maniac.Stable;

static class SelfTest
{
    public static void Run()
    {
        var frame = new StableGame.Frame(
            AudioTime: 1000,
            Paused: false,
            OsuMode: StableLayout.OsuModePlay,
            PlayMode: StableLayout.PlayModeMania,
            Notes:
            [
                new(0, 0, 64, false, StableLayout.HitObjectType),
                new(0, 0, 192, false, StableLayout.HitObjectType),
                new(0, 0, 320, false, StableLayout.HitObjectType),
                new(500, 1500, 448, true, StableLayout.ManiaHoldType),
            ]);
        var plan = StableGame.ToPlan(frame);
        if (plan.Columns != 4)
            throw new InvalidOperationException($"expected 4 columns, got {plan.Columns}");
        if (plan.HitCount != 4)
            throw new InvalidOperationException($"expected 4 hits, got {plan.HitCount}");
        if (plan.Hits[3].Column != 3 || !plan.Hits[3].IsHold)
            throw new InvalidOperationException("hold on column 3 was not preserved.");
        if (plan.Hits[3].End != 1500)
            throw new InvalidOperationException("hold end time was not preserved.");

        var driver = new ManiaDriver();
        var sink = new RecordingKeys();
        driver.Load(plan);
        driver.Tick(0, sink, play: true);
        if (sink.Down.Count != 3)
            throw new InvalidOperationException($"expected 3 presses at t=0, got {sink.Down.Count}");
        driver.Tick(500, sink, play: true);
        if (!sink.Down.Contains(3))
            throw new InvalidOperationException("hold press at t=500 was not applied.");
        driver.Tick(1500, sink, play: true);
        if (sink.Down.Contains(3))
            throw new InvalidOperationException("hold was still down after end.");

        if (ManiaKeys.Default(7) != "sdf-jkl")
            throw new InvalidOperationException("7K default key map is not sdf-jkl.");
        var seven = ManiaKeys.Parse("sdf-jkl");
        if (seven.Length != 7 || seven[3] != 0x20)
            throw new InvalidOperationException("7K key map must use space for '-'.");
        if (ManiaKeys.Parse("asdfjkl;").Length != 8)
            throw new InvalidOperationException("8K key map must accept ';'.");
        if (StableBindings.SpecFromXna([83, 68, 70, 32, 74, 75, 76]) != "sdf-jkl")
            throw new InvalidOperationException("XNA 7K keys did not format as sdf-jkl.");
        if (StableBindings.SpecFromXna(StableBindings.ParseXnaNames("S D F Space J K L")) != "sdf-jkl")
            throw new InvalidOperationException("parsed 7K layout string did not match sdf-jkl.");
        try
        {
            ManiaKeys.Parse("df@k");
            throw new InvalidOperationException("invalid key map was accepted.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("df@k"))
        {
        }

        var sevenFrame = new StableGame.Frame(
            AudioTime: 0,
            Paused: false,
            OsuMode: StableLayout.OsuModePlay,
            PlayMode: StableLayout.PlayModeMania,
            Notes:
            [
                new(100, 100, 15, false, StableLayout.HitObjectType),
                new(100, 100, 45, false, StableLayout.HitObjectType),
                new(100, 100, 75, false, StableLayout.HitObjectType),
                new(100, 100, 105, false, StableLayout.HitObjectType),
                new(100, 100, 135, false, StableLayout.HitObjectType),
                new(100, 100, 165, false, StableLayout.HitObjectType),
                new(200, 800, 195, true, StableLayout.ManiaHoldType),
            ]);
        var sevenPlan = StableGame.ToPlan(sevenFrame);
        if (sevenPlan.Columns != 7)
            throw new InvalidOperationException($"expected 7 columns, got {sevenPlan.Columns}");
        if (sevenPlan.Hits[6].Column != 6 || !sevenPlan.Hits[6].IsHold)
            throw new InvalidOperationException("7K hold on last column was not preserved.");

        _ = new SendInputSink("sdf-jkl");
    }

    sealed class RecordingKeys : Maniac.Core.Input.IInputSink
    {
        public readonly HashSet<int> Down = [];

        public void Press(int action) => Down.Add(action);
        public void Release(int action) => Down.Remove(action);
        public void MoveMouse(float x, float y) { }
        public void ReleaseHeld() => Down.Clear();
    }
}
