using System.Diagnostics;
using System.Text;

namespace ManiaHuman;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var opt = Args.Parse(args);
            if (opt.Help)
            {
                PrintHelp();
                return 0;
            }

            if (opt.Command == "selftest")
                return SelfTest.Run();

            var catalog = new Catalog(opt.DataDir);
            Console.WriteLine($"data    {catalog.DataDir}");

            switch (opt.Command)
            {
                case "login":
                    await catalog.LoginAsync();
                    return 0;
                case "fetch":
                    int n = await catalog.FetchAsync(opt.Maps, opt.Scores, opt.Me, opt.BeatmapId, opt.Replace);
                    Console.WriteLine($"fetched {n} replays");
                    if (opt.AnalyzeAfter)
                        return await AnalyzeAll(catalog, opt);
                    return 0;
                case "scan":
                    int copied = catalog.ScanLocal();
                    Console.WriteLine($"imported {copied} local mania replays");
                    if (opt.AnalyzeAfter)
                        return await AnalyzeAll(catalog, opt);
                    return 0;
                default:
                    return await AnalyzeAll(catalog, opt);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static async Task<int> AnalyzeAll(Catalog catalog, Args opt)
    {
        var files = opt.Paths.Count > 0
            ? opt.Paths
            : catalog.ReplayFiles().ToList();
        if (files.Count == 0)
        {
            Console.WriteLine("No .osr files. Put them in data/replays, or run fetch / scan. See --help.");
            return 0;
        }

        var stats = new List<ReplayStats>();
        int skipped = 0;
        foreach (string file in files)
        {
            OsrReplay replay;
            try { replay = ReplayIO.ParseOsr(file); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"skip {Path.GetFileName(file)}: {ex.Message}");
                skipped++;
                continue;
            }

            if (replay.Mode != GameMode.Mania)
            {
                Console.WriteLine($"skip {Path.GetFileName(file)}: not mania");
                skipped++;
                continue;
            }

            if (replay.Unusable)
            {
                Console.WriteLine($"skip {replay.Player}: unusable mods {replay.ModsLabel}");
                skipped++;
                continue;
            }

            var beatmap = await catalog.ResolveBeatmapAsync(replay);
            if (beatmap is null)
            {
                Console.WriteLine($"skip {replay.Player}: no beatmap for md5 {replay.BeatmapMd5}");
                skipped++;
                continue;
            }

            if (!beatmap.IsMania)
            {
                Console.WriteLine($"skip {beatmap.DisplayName}: converts are not parsed");
                skipped++;
                continue;
            }

            var match = NoteMatcher.Match(replay, beatmap);
            var one = Analyze.Compute(match);
            if (catalog.MetaFor(replay, file) is { } meta)
            {
                one.LeaderboardRank = meta.LeaderboardRank;
                one.ApiAccuracy = meta.Accuracy;
                one.Stars = meta.Stars;
                one.Source = meta.Source;
                one.UserGlobalRank = meta.UserGlobalRank;
            }
            stats.Add(one);
            Console.WriteLine($"{replay.Player,-16} {Trunc(beatmap.Version, 24),-24} UR {one.UnstableRate,5:0.0}  miss {one.MissRate,5:0.0%}  n={one.MatchedHeads}");
        }

        var agg = Analyze.Aggregate(stats);
        agg.Skipped = skipped;
        Report.Print(agg);
        string html = Report.Write(catalog.DataDir, agg);
        Console.WriteLine($"report  {html}");
        if (opt.Open)
            Process.Start(new ProcessStartInfo(html) { UseShellExecute = true });
        return 0;
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    static void PrintHelp()
    {
        Console.WriteLine("""
            mania-human — timing statistics from public mania replays

            Matches .osr keypresses to .osu notes and reports hit error, chord
            desync, tap duration, misses, and how those change with rank.

            usage:
              dotnet run --project experiments/mania-human -- [command] [options] [files.osr ...]

            commands:
              analyze     parse local replays (default)
              fetch       download mania replays via the osu! API
              login       OAuth login (only if replay download is rejected)
              scan        copy mania replays/beatmaps from local stable or lazer
              selftest    parser + matcher checks, no network

            fetch (default) takes the top-50 spread on hard ranked 4K/7K maps
            (stars ≥ 6) plus recent plays from mid-tier 4K pp ranking pages.
            The API leaderboard is only 50 scores, so "100th place" is not
            available; #50 on a hard map plus non-BP recents is the substitute.

            options:
              --data DIR      data folder (default: experiments/mania-human/data)
              --maps N        hard maps to sample (fetch, default 4)
              --scores N      scores spread across each map's top 50 (default 8)
              --beatmap ID    fetch one beatmap's leaderboard spread
              --me            fetch the logged-in user's mania top plays
              --replace       delete previously fetched .osr files first
              --keep          keep existing replays (default)
              --open          open the HTML report
              --also-analyze  run analyze after fetch/scan

            credentials (any of):
              OSU_CLIENT_ID + OSU_CLIENT_SECRET   API v2 (list maps/scores)
              then `login` for replay files       user OAuth, saved under %APPDATA%\maniac-lazer
              OSU_API_KEY                         legacy v1, stable replays only

            without credentials, drop .osr files into data/replays (export from the
            game) and .osu files into data/beatmaps, or run scan if you have local scores.
            """);
    }
}

readonly struct Args
{
    public string Command { get; init; }
    public string DataDir { get; init; }
    public List<string> Paths { get; init; }
    public int Maps { get; init; }
    public int Scores { get; init; }
    public int? BeatmapId { get; init; }
    public bool Me { get; init; }
    public bool Open { get; init; }
    public bool Help { get; init; }
    public bool AnalyzeAfter { get; init; }
    public bool Replace { get; init; }

    public static Args Parse(string[] args)
    {
        string cmd = "analyze";
        string? data = null;
        int maps = 4, scores = 8;
        int? beatmap = null;
        bool me = false, open = false, help = false, also = false, replace = false;
        var paths = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-h" or "--help" or "help") { help = true; continue; }
            if (a is "--open") { open = true; continue; }
            if (a is "--me") { me = true; continue; }
            if (a is "--replace") { replace = true; continue; }
            if (a is "--keep") { replace = false; continue; }
            if (a is "--also-analyze" or "--analyze") { also = true; continue; }
            if (a is "--data" && i + 1 < args.Length) { data = args[++i]; continue; }
            if (a is "--maps" && i + 1 < args.Length) { maps = int.Parse(args[++i]); continue; }
            if (a is "--scores" && i + 1 < args.Length) { scores = int.Parse(args[++i]); continue; }
            if (a is "--beatmap" && i + 1 < args.Length) { beatmap = int.Parse(args[++i]); continue; }
            if (a.StartsWith('-'))
                throw new InvalidOperationException($"unknown flag {a}");
            if (cmd == "analyze" && a is "analyze" or "fetch" or "login" or "scan" or "selftest")
            {
                cmd = a;
                continue;
            }

            paths.Add(a);
        }

        return new Args
        {
            Command = cmd,
            DataDir = data ?? DefaultDataDir(),
            Paths = paths,
            Maps = maps,
            Scores = scores,
            BeatmapId = beatmap,
            Me = me,
            Open = open,
            Help = help,
            AnalyzeAfter = also,
            Replace = replace,
        };
    }

    static string DefaultDataDir()
    {
        string cwd = Directory.GetCurrentDirectory();
        string nested = Path.Combine(cwd, "experiments", "mania-human");
        if (Directory.Exists(nested))
            return Path.Combine(nested, "data");
        string here = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            string candidate = Path.Combine(here, "data");
            string csproj = Path.Combine(here, "ManiaHuman.csproj");
            if (File.Exists(csproj))
                return candidate;
            string? parent = Directory.GetParent(here)?.FullName;
            if (parent is null) break;
            here = parent;
        }

        return Path.Combine(cwd, "data");
    }
}

static class SelfTest
{
    public static int Run()
    {
        int failed = 0;
        void check(string name, bool ok)
        {
            Console.WriteLine(ok ? $"  ok   {name}" : $"  FAIL {name}");
            if (!ok) failed++;
        }

        const string osu = """
            osu file format v14
            [General]
            Mode: 3
            [Metadata]
            Title:test
            Artist:a
            Version:x
            [Difficulty]
            CircleSize:4
            OverallDifficulty:8
            [HitObjects]
            64,192,1000,1,0,0:0:0:0:
            192,192,1000,1,0,0:0:0:0:
            320,192,1200,128,0,1500:0:0:0:0:
            """;
        var map = ReplayIO.ParseOsu(Encoding.UTF8.GetBytes(osu.Replace("\r\n", "\n")));
        check("parse keys", map.Columns == 4 && map.IsMania);
        check("parse objects", map.Notes.Length == 3 && map.Notes[0].Column == 0 && map.Notes[1].Column == 1 && map.Notes[2].IsHold);
        check("column from x", ReplayIO.ColumnFromX(320, 4) == 2);

        var classic = new HitWindows(8, 1, classic: true, easy: false, hardRock: false, convert: false);
        check("classic 320 window ~16.5", classic.Perfect is >= 16 and <= 17);
        check("classic miss grows with easier OD", new HitWindows(0, 1, true, false, false, false).Miss > classic.Miss);

        var exact = new OsrReplay
        {
            Mode = GameMode.Mania,
            Player = "exact",
            Events =
            [
                new(1000, 0, true), new(1000, 1, true),
                new(1020, 0, false), new(1020, 1, false),
                new(1200, 2, true), new(1500, 2, false),
            ],
        };
        var exactStats = Analyze.Compute(NoteMatcher.Match(exact, map));
        check("exact-time UR is ~0", exactStats.UnstableRate < 0.01);
        check("exact chord spread is 0", exactStats.ChordSpreadMean < 0.01);
        check("no misses when every note is pressed", exactStats.MissedHeads == 0);

        var humanEvents = new KeyEvent[]
        {
            new(1004, 0, true), new(1009, 1, true),
            new(1055, 0, false), new(1060, 1, false),
            new(1192, 2, true), new(1488, 2, false),
        };
        var human = new OsrReplay { Mode = GameMode.Mania, Player = "human", Events = humanEvents };
        var hs = Analyze.Compute(NoteMatcher.Match(human, map));
        check("offset presses have UR > 0", hs.UnstableRate > 1);
        check("late presses have positive mean error", hs.MeanError > 0);
        check("staggered chord is desynced", hs.ChordSpreadMean >= 5);
        check("early hold tail is negative release error", hs.HoldReleaseMean < 0);

        Console.WriteLine(failed == 0 ? "selftest passed" : $"selftest failed ({failed})");
        return failed == 0 ? 0 : 1;
    }
}
