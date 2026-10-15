using System.Runtime.InteropServices;
using Maniac.Core.Input;
using Maniac.Core.Mania;

namespace StableHost;

static class Program
{
    static int Main(string[] args)
    {
        try
        {
            string command = args.Length == 0 ? "inspect" : args[0];
            if (command == "selftest")
            {
                SelfTest.Run();
                return 0;
            }

            var process = OsuProcess.RequireStable();
            Console.WriteLine($"stable pid={process.Id} {process.MainModule?.FileName}");
            switch (command)
            {
                case "inspect":
                    HeapInspect.Run(process.Id);
                    return 0;
                case "clocks":
                    ClockHunt.Run(process.Id);
                    return 0;
                case "probe":
                    using (var game = new StableGame(process.Id, snapshot: false))
                        game.ProbeLists();
                    return 0;
                case "watch":
                    Watch(process.Id, Seconds(args, 20));
                    return 0;
                case "play":
                    Play(process.Id, KeysArg(args), dry: false, Seconds(args, 0));
                    return 0;
                case "bindings":
                    BindingHunt.Run(process.Id);
                    return 0;
                case "chart":
                    Play(process.Id, KeysArg(args), dry: true, Seconds(args, 0));
                    return 0;
                default:
                    throw new InvalidOperationException($"unknown command '{command}'. use selftest, inspect, clocks, probe, watch, chart, play, or bindings.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    static int Seconds(string[] args, int fallback)
    {
        for (int i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--seconds")
                return int.Parse(args[i + 1]);
        }

        return fallback;
    }

    static string? KeysArg(string[] args)
    {
        for (int i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--keys")
                return args[i + 1];
        }

        return null;
    }

    static ManiaKeyMap BindKeys(StableGame game, int columns, string? keysArg)
    {
        ManiaKeyMap map = keysArg == null
            ? game.ReadManiaKeys(columns)
            : new ManiaKeyMap(keysArg, ManiaKeys.Parse(keysArg));
        if (map.VirtualKeys.Length != columns)
            throw new InvalidOperationException($"key map '{map.Spec}' has {map.VirtualKeys.Length} keys but the map is {columns}K.");
        return map;
    }

    static void Watch(int pid, int seconds)
    {
        using var game = new StableGame(pid, snapshot: false);
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            var frame = game.Read();
            PrintFrame(frame);
            Thread.Sleep(200);
        }
    }

    static void Play(int pid, string? keysArg, bool dry, int seconds)
    {
        using var game = new StableGame(pid, snapshot: false);
        var driver = new ManiaDriver();
        IInputSink sink = NullInputSink.Instance;
        string? keys = null;
        ManiaKeyMap? keyMap = null;
        var hwnd = OsuProcess.RequireStable().MainWindowHandle;
        if (!dry && hwnd == IntPtr.Zero)
            throw new InvalidOperationException("osu! has no window.");

        ulong lastSig = 0;
        var lastError = "";
        var lastPrint = DateTime.UtcNow;
        var until = seconds > 0 ? DateTime.UtcNow.AddSeconds(seconds) : DateTime.MaxValue;
        while (DateTime.UtcNow < until)
        {
            try
            {
                var status = game.ReadStatus();
                bool onMap = status.OsuMode == StableLayout.OsuModePlay
                    && status.PlayMode == StableLayout.PlayModeMania;
                if (!onMap)
                {
                    if (lastSig != 0)
                    {
                        driver.Unload(sink);
                        sink.ReleaseHeld();
                        lastSig = 0;
                        keys = null;
                        keyMap = null;
                        if (!dry)
                            sink = NullInputSink.Instance;
                        Console.WriteLine("left play; unloaded plan");
                    }
                }
                else if (lastSig == 0)
                {
                    var notes = game.LoadNotes();
                    if (notes.Length > 0)
                    {
                        var plan = StableGame.ToPlan(status with { Notes = notes });
                        keyMap = BindKeys(game, plan.Columns, keysArg);
                        keys = keyMap.Value.Spec;
                        if (!dry)
                        {
                            sink.ReleaseHeld();
                            sink = new SendInputSink(keyMap.Value.VirtualKeys, keyMap.Value.Spec);
                        }

                        driver.Load(plan);
                        lastSig = NoteSignature(notes);
                        Console.WriteLine($"loaded {plan.HitCount} hits columns={plan.Columns} events={plan.Events.Length} keys={keys}");
                        var first = notes[0];
                        var last = notes[^1];
                        Console.WriteLine($"first t={first.Start:0} x={first.X:0} hold={first.IsHold} last t={last.Start:0}->{last.End:0} x={last.X:0}");
                    }
                }

                bool focused = dry || GetForegroundWindow() == hwnd;
                bool play = !dry && onMap && !status.Paused && focused && driver.HitCount > 0;
                driver.Tick(status.AudioTime, sink, play);
                if (DateTime.UtcNow - lastPrint > TimeSpan.FromMilliseconds(200))
                {
                    PrintFrame(status, driver, keys, focused);
                    lastPrint = DateTime.UtcNow;
                }
                lastError = "";
            }
            catch (Exception ex)
            {
                sink.ReleaseHeld();
                string message = ex.GetBaseException().Message;
                if (message != lastError)
                {
                    Console.WriteLine($"error: {message}");
                    lastError = message;
                }
            }

            Thread.Sleep(1);
        }

        sink.ReleaseHeld();
    }

    static ulong NoteSignature(StableGame.Note[] notes)
    {
        if (notes.Length == 0)
            return 0;
        var first = notes[0];
        var last = notes[^1];
        return (ulong)notes.Length << 48
            ^ (ulong)(uint)first.Start
            ^ (ulong)(uint)last.Start << 16
            ^ (ulong)(uint)last.End << 32;
    }

    static void PrintFrame(StableGame.Frame frame, ManiaDriver? driver = null, string? keys = null, bool focused = true)
    {
        string mode = frame.PlayMode switch
        {
            0 => "osu",
            1 => "taiko",
            2 => "catch",
            3 => "mania",
            _ => frame.PlayMode.ToString()
        };
        string extra = "";
        if (driver != null)
            extra += $" plan={driver.HitCount}h/{driver.Columns}K applied={driver.Applied}/{driver.Total}";
        if (keys != null)
            extra += $" keys={keys}";
        if (!focused)
            extra += " waiting-focus";
        if (frame.Notes.Length == 0)
        {
            Console.WriteLine($"t={frame.AudioTime} paused={frame.Paused} osuMode={frame.OsuMode} mode={mode}{extra}");
            return;
        }

        var first = frame.Notes[0];
        var last = frame.Notes[^1];
        Console.WriteLine(
            $"t={frame.AudioTime} paused={frame.Paused} osuMode={frame.OsuMode} mode={mode} notes={frame.Notes.Length} first={first.Start:0}/{first.X:0} last={last.Start:0}->{last.End:0}/{last.X:0}{extra}");
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
}
