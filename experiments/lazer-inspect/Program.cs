namespace LazerInspect;

static class Program
{
    static int Main(string[] args)
    {
        try
        {
            var parsed = Args.Parse(args);
            if (parsed.Help)
            {
                PrintHelp();
                return 0;
            }

            using var reader = LazerReader.Attach(parsed.Pid);
            RunCommand(reader, parsed.Command, parsed.Rest);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static void RunCommand(LazerReader reader, string command, string[] rest)
    {
        switch (command)
        {
            case "status":
                PrintStatus(reader);
                break;
            case "beatmap":
                PrintBeatmap(reader, ParseInt(rest, 40));
                break;
            case "runtime-offsets":
                PrintRuntimeOffsets(reader);
                break;
            case "fields":
                PrintFields(reader, rest.FirstOrDefault());
                break;
            case "get":
                if (rest.Length < 2)
                    throw new InvalidOperationException("usage: get <type> <field>    e.g. get osu.Game.OsuGameBase beatmapClock");
                PrintGet(reader, rest[0], rest[1]);
                break;
            case "watch":
                RunWatch(reader, ParseInt(rest, 1000));
                break;
            case "input":
                PrintInput(reader);
                break;
            default:
                throw new InvalidOperationException($"unknown command '{command}'. try --help");
        }
    }

    static void PrintStatus(LazerReader reader)
    {
        var screens = reader.Screens();
        var current = reader.CurrentScreen();
        var ruleset = reader.Ruleset();
        var beatmap = reader.ReadBeatmap();
        double time = reader.CurrentTime();
        ulong player = reader.Player();

        Console.WriteLine($"process     {reader.Memory.ProcessId}");
        Console.WriteLine($"game        OsuGameDesktop @ 0x{reader.GameBase:X}");
        Console.WriteLine($"offsets     {reader.Offsets.OsuVersion}");
        Console.WriteLine($"screens     {screens.Length}");
        for (int i = 0; i < screens.Length; i++)
        {
            string tag = screens[i] == current ? "current" : $"[{i}]";
            Console.WriteLine($"  {tag,-8} 0x{screens[i]:X}  {reader.ClassifyScreen(screens[i])}");
        }

        Console.WriteLine($"phase       {reader.ClassifyScreen(current)}");
        Console.WriteLine($"ruleset     {ruleset.ShortName} (id {ruleset.OnlineId})");
        Console.WriteLine($"mods        {reader.ModCount()} selected");
        Console.WriteLine($"beatmap     {beatmap.Artist} - {beatmap.Title} [{beatmap.Difficulty}]" +
                          (beatmap.OnlineId > 0 ? $"  #{beatmap.OnlineId}" : ""));
        Console.WriteLine($"time        {FormatTime(time)}  ({time:0} ms)");
        if (player != 0)
            Console.WriteLine($"player      @ 0x{player:X}");
    }

    static void PrintRuntimeOffsets(LazerReader reader)
    {
        Console.WriteLine($"game-dir    {reader.Offsets.GameDirectory}");
        Console.WriteLine($"version     {reader.Offsets.OsuVersion}");
        Console.WriteLine($"{"offset",8}  field");
        foreach (var (name, offset) in reader.Offsets.Dump())
            Console.WriteLine($"{offset,8}  {name}");
    }

    static void PrintBeatmap(LazerReader reader, int limit)
    {
        var beatmap = reader.ReadBeatmap();
        Console.WriteLine($"working     @ 0x{beatmap.Working:X}");
        Console.WriteLine($"info        {beatmap.Artist} - {beatmap.Title} [{beatmap.Difficulty}]");
        Console.WriteLine($"online-id   {beatmap.OnlineId}");
        Console.WriteLine($"md5         {beatmap.Md5}");
        Console.WriteLine($"hash        {beatmap.Hash}");
        Console.WriteLine($"difficulty  CS={beatmap.CircleSize}  OD={beatmap.OverallDifficulty}  AR={beatmap.ApproachRate}  HP={beatmap.DrainRate}");
        Console.WriteLine($"listed      {beatmap.TotalObjectCount} objects (BeatmapInfo.TotalObjectCount)");

        var workingHits = BeatmapMemory.TryLoadWorking(reader, beatmap);
        var loaded = BeatmapMemory.TryLoadGameplay(reader) ?? workingHits;
        if (workingHits is not null && loaded is not null && workingHits.Value.Address != loaded.Value.Address)
            Console.WriteLine($"decoded     {workingHits.Value.HitObjects.Count} objects @ 0x{workingHits.Value.Address:X} (WorkingBeatmap task)");

        if (loaded is null)
        {
            Console.WriteLine("hitobjects  (not in memory yet — select a map or wait for it to finish loading)");
            return;
        }

        Console.WriteLine($"loaded      {loaded.Value.Source} beatmap @ 0x{loaded.Value.Address:X}");
        if (loaded.Value.ColumnCount is int columns)
        {
            var used = loaded.Value.HitObjects.Select(h => h.Column).OfType<int>().Distinct().OrderBy(c => c).ToList();
            Console.WriteLine($"columns     {columns}" + (used.Count > 0 ? $"  used=[{string.Join(",", used)}]" : ""));
        }
        Console.WriteLine($"hitobjects  {loaded.Value.HitObjects.Count}");

        int lasting = 0;
        int shown = 0;
        foreach (var hit in loaded.Value.HitObjects)
        {
            if (hit.EndTime is double)
                lasting++;
            if (shown++ >= limit)
                continue;
            string dur = hit.EndTime is double e ? $"  {FormatTime(e)}  dur={e - hit.StartTime:0}ms" : "";
            string col = hit.Column is int c ? $"  col={c}" : "";
            Console.WriteLine($"  {FormatTime(hit.StartTime),10}{dur}{col}");
        }

        int instant = loaded.Value.HitObjects.Count - lasting;
        Console.WriteLine($"counts      {instant} instant  {lasting} lasting");
        if (loaded.Value.HitObjects.Count > limit)
            Console.WriteLine($"            (showing first {limit}; pass a larger limit)");
    }

    static void PrintFields(LazerReader reader, string? type)
    {
        if (string.IsNullOrEmpty(type))
        {
            foreach (var name in reader.Offsets.Types())
                Console.WriteLine(name);
            return;
        }

        var fields = reader.Offsets.Fields(type).ToList();
        if (fields.Count == 0)
        {
            var matches = reader.Offsets.Types()
                .Where(t => t.Contains(type, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1)
            {
                type = matches[0];
                fields = reader.Offsets.Fields(type).ToList();
            }
            else
            {
                Console.WriteLine($"no offset table for '{type}'.");
                foreach (var m in matches.Take(20))
                    Console.WriteLine($"  {m}");
                return;
            }
        }

        Console.WriteLine(type);
        Console.WriteLine($"{"offset",8}  field");
        foreach (var (name, offset) in fields.OrderBy(f => f.Offset))
            Console.WriteLine($"{offset,8}  {name}");
    }

    static void PrintGet(LazerReader reader, string type, string field)
    {
        ulong value = reader.GameField(type, field);
        Console.WriteLine($"game+{type}.{field} = 0x{value:X}  ({value})");
        if (value != 0)
        {
            try { Console.WriteLine($"  as string: {reader.Memory.ReadClrString(value)}"); }
            catch { /* not a string */ }
        }
    }

    static void PrintInput(LazerReader reader)
    {
        var graph = reader.TryReadInputGraph();
        if (graph is null)
        {
            Console.WriteLine("input       (not playing — no DrawableRuleset / ManiaInputManager)");
            return;
        }

        var g = graph.Value;
        Console.WriteLine($"drawable    @ 0x{g.DrawableRuleset:X}");
        Console.WriteLine($"manager     @ 0x{g.InputManager:X}  UseParentInput={g.UseParentInput}");
        Console.WriteLine($"bindings    @ 0x{g.KeyBindingContainer:X}");
        Console.WriteLine($"pressed     {g.PressedActions.Length}");
        foreach (int action in g.PressedActions)
        {
            string name = action is >= 0 and < 20 ? $"Key{action + 1}" : $"action:{action}";
            Console.WriteLine($"            {name} ({action})");
        }
    }

    static void RunWatch(LazerReader reader, int intervalMs)
    {
        Console.WriteLine($"watching every {intervalMs} ms (Ctrl+C to stop).");
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        while (!cts.IsCancellationRequested)
        {
            Console.WriteLine("────────");
            try { PrintStatus(reader); }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); }
            cts.Token.WaitHandle.WaitOne(intervalMs);
        }
    }

    static int ParseInt(string[] rest, int fallback)
    {
        foreach (var tok in rest)
        {
            if (int.TryParse(tok, out int n))
                return n;
        }
        return fallback;
    }

    static string FormatTime(double ms)
    {
        bool neg = ms < 0;
        ms = Math.Abs(ms);
        int total = (int)Math.Round(ms);
        return $"{(neg ? "-" : "")}{total / 60000}:{total % 60000 / 1000:00}.{total % 1000:000}";
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            lazer-inspect — experimental osu!lazer memory reader

            Finds the osu! window, resolves the running client's files on disk,
            and dumps CoreCLR FieldDesc offsets from those assemblies at startup.
            Game objects are then walked via ReadProcessMemory.

            usage:
              lazer-inspect [--pid N] [command]

            commands:
              status              screen stack, ruleset, beatmap, clock
              beatmap [limit]     metadata + hit objects from the in-memory IBeatmap
              runtime-offsets     FieldDesc offsets dumped from the live install
              fields [type]       dump the offset table (or one type)
              get <type> <field>  read a pointer-sized field off OsuGameDesktop
              watch [ms]          re-print status on an interval
              input               locate ManiaInputManager / KeyBindingContainer while playing

            examples:
              dotnet run --project experiments/lazer-inspect -- status
              dotnet run --project experiments/lazer-inspect -- beatmap 80
              dotnet run --project experiments/lazer-inspect -- fields SoloPlayer
            """);
    }
}

readonly struct Args
{
    public bool Help { get; init; }
    public int? Pid { get; init; }
    public string Command { get; init; }
    public string[] Rest { get; init; }

    public static Args Parse(string[] args)
    {
        bool help = false;
        int? pid = null;
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-h" or "--help" or "help")
            {
                help = true;
                continue;
            }

            if (a is "--pid" && i + 1 < args.Length)
            {
                pid = int.Parse(args[++i]);
                continue;
            }

            if (a.StartsWith("--pid=", StringComparison.Ordinal))
            {
                pid = int.Parse(a["--pid=".Length..]);
                continue;
            }

            positional.Add(a);
        }

        return new Args
        {
            Help = help,
            Pid = pid,
            Command = positional.Count > 0 ? positional[0] : "status",
            Rest = positional.Skip(1).ToArray(),
        };
    }
}
