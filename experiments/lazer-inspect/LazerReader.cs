namespace LazerInspect;

sealed class LazerReader : IDisposable
{
    static readonly byte[] DrawSizePattern =
        [0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x80, 0x44, 0x00, 0x00, 0x40, 0x44];

    static readonly int[] AnchorDeltas = [0x24, 0x28, 0x2c, 0x20, 0x30, 0x1c, 0x34];

    public ProcessMemory Memory { get; }
    public OffsetTable Offsets { get; }
    public ulong GameBase { get; }

    LazerReader(ProcessMemory memory, OffsetTable offsets, ulong gameBase)
    {
        Memory = memory;
        Offsets = offsets;
        GameBase = gameBase;
    }

    public static LazerReader Attach(int? pid = null)
    {
        var memory = ProcessMemory.Attach(pid);
        string gameDir = Path.GetDirectoryName(memory.ProcessPath)
            ?? throw new InvalidOperationException($"No directory for {memory.ProcessPath}.");
        if (!File.Exists(Path.Combine(gameDir, "osu.Game.dll")))
        {
            throw new InvalidOperationException(
                $"osu.Game.dll is not next to the running client ({memory.ProcessPath}).");
        }

        Console.Error.WriteLine($"pid {memory.ProcessId}  {memory.ProcessPath}");
        var offsets = OffsetTable.Load(gameDir);
        Console.Error.WriteLine($"offsets    FieldDesc dump of {offsets.OsuVersion}");

        ulong gameBase = FindGameBase(memory, offsets);
        Console.Error.WriteLine($"OsuGameDesktop @ 0x{gameBase:X}");
        return new LazerReader(memory, offsets, gameBase);
    }

    public void Dispose() => Memory.Dispose();

    public ulong Field(ulong obj, string type, string field) =>
        obj == 0 ? 0 : Memory.ReadPtrOrZero(obj + (ulong)Offsets[type, field]);

    public ulong GameField(string type, string field) => Field(GameBase, type, field);

    public ulong ScreenStack() => GameField("osu.Game.OsuGame", "<ScreenStack>k__BackingField");

    public ulong[] Screens()
    {
        ulong stackObj = Field(ScreenStack(), "osu.Framework.Screens.ScreenStack", "stack");
        var items = Memory.ReadRefList(stackObj);
        // Stack<T> stores bottom-first; current screen is the last item.
        return items;
    }

    public ulong CurrentScreen()
    {
        var screens = Screens();
        return screens.Length == 0 ? 0 : screens[^1];
    }

    public string ClassifyScreen(ulong screen)
    {
        if (screen == 0)
            return "unknown";
        if (IsPlayer(screen))
            return "playing";
        if (IsPlayerLoader(screen))
            return "player-loader";
        if (IsSongSelect(screen))
            return "song-select";
        if (IsResultScreen(screen))
            return "results";
        if (IsEditor(screen))
            return "editor";
        return $"screen@0x{screen:X}";
    }

    public (int OnlineId, string ShortName, string Name) Ruleset()
    {
        ulong bindable = GameField("osu.Game.OsuGameBase", "Ruleset");
        ulong info = Memory.BindableObject(bindable);
        int id = Memory.ReadInt(info + (ulong)Offsets["osu.Game.Rulesets.RulesetInfo", "<OnlineID>k__BackingField"]);
        return (id, RulesetName(id), RulesetName(id));
    }

    public int ModCount()
    {
        ulong bindable = GameField("osu.Game.OsuGameBase", "SelectedMods");
        ulong list = Memory.BindableObject(bindable);
        return Memory.ReadRefList(list).Length;
    }

    public double CurrentTime()
    {
        ulong clock = GameField("osu.Game.OsuGameBase", "beatmapClock");
        ulong source = Field(clock, "osu.Game.Beatmaps.FramedBeatmapClock", "finalClockSource");
        return Memory.ReadDouble(source + (ulong)Offsets["osu.Framework.Timing.FramedClock", "<CurrentTime>k__BackingField"]);
    }

    public ulong Player()
    {
        ulong screen = CurrentScreen();
        return IsPlayer(screen) ? screen : 0;
    }

    public InputGraph? TryReadInputGraph()
    {
        ulong player = Player();
        if (player == 0)
            return null;

        ulong drawable = Memory.ReadPtrOrZero(player + (ulong)Offsets.Player_DrawableRuleset);
        ulong manager = Memory.ReadPtrOrZero(drawable + (ulong)Offsets.DrawableRuleset_KeyBindingInputManager);
        ulong bindings = Memory.ReadPtrOrZero(manager + (ulong)Offsets.RulesetInputManager_KeyBindingContainer);
        if (manager == 0 || bindings == 0)
            return null;

        bool useParent = Memory.ReadByte(manager + (ulong)Offsets.PassThrough_useParentInput) != 0;
        ulong list = Memory.ReadPtrOrZero(bindings + (ulong)Offsets.KeyBindingContainer_pressedActions);
        int[] pressed = Memory.TryReadIntList(list, out var items) ? items : [];
        return new InputGraph(drawable, manager, bindings, useParent, pressed);
    }

    public BeatmapSnapshot ReadBeatmap()
    {
        ulong bindable = GameField("osu.Game.OsuGameBase", "<Beatmap>k__BackingField");
        ulong working = Memory.BindableObject(bindable);
        ulong info = Field(working, "osu.Game.Beatmaps.WorkingBeatmapCache+BeatmapManagerWorkingBeatmap", "BeatmapInfo");
        ulong setInfo = Field(working, "osu.Game.Beatmaps.WorkingBeatmapCache+BeatmapManagerWorkingBeatmap", "BeatmapSetInfo");
        ulong meta = Field(info, "osu.Game.Beatmaps.BeatmapInfo", "<Metadata>k__BackingField");
        ulong diff = Field(info, "osu.Game.Beatmaps.BeatmapInfo", "<Difficulty>k__BackingField");

        string hash = Memory.ReadClrStringPtr(info + (ulong)Offsets["osu.Game.Beatmaps.BeatmapInfo", "<Hash>k__BackingField"]);
        string md5 = Memory.ReadClrStringPtr(info + (ulong)Offsets["osu.Game.Beatmaps.BeatmapInfo", "<MD5Hash>k__BackingField"]);
        string diffName = Memory.ReadClrStringPtr(info + (ulong)Offsets["osu.Game.Beatmaps.BeatmapInfo", "<DifficultyName>k__BackingField"]);
        int onlineId = Memory.ReadInt(info + (ulong)Offsets["osu.Game.Beatmaps.BeatmapInfo", "<OnlineID>k__BackingField"]);

        string title = Memory.ReadClrStringPtr(meta + (ulong)Offsets["osu.Game.Beatmaps.BeatmapMetadata", "<Title>k__BackingField"]);
        string artist = Memory.ReadClrStringPtr(meta + (ulong)Offsets["osu.Game.Beatmaps.BeatmapMetadata", "<Artist>k__BackingField"]);

        float cs = Memory.ReadFloat(diff + (ulong)Offsets["osu.Game.Beatmaps.BeatmapDifficulty", "<CircleSize>k__BackingField"]);
        float od = Memory.ReadFloat(diff + (ulong)Offsets["osu.Game.Beatmaps.BeatmapDifficulty", "<OverallDifficulty>k__BackingField"]);
        float ar = Memory.ReadFloat(diff + (ulong)Offsets["osu.Game.Beatmaps.BeatmapDifficulty", "<ApproachRate>k__BackingField"]);
        float hp = Memory.ReadFloat(diff + (ulong)Offsets["osu.Game.Beatmaps.BeatmapDifficulty", "<DrainRate>k__BackingField"]);
        int totalObjects = Memory.ReadInt(info + (ulong)Offsets["osu.Game.Beatmaps.BeatmapInfo", "<TotalObjectCount>k__BackingField"]);

        return new BeatmapSnapshot(working, info, setInfo, artist, title, diffName, onlineId, hash, md5, cs, od, ar, hp, totalObjects);
    }

    bool IsPlayer(ulong screen) =>
        screen != 0
        && Field(screen, "osu.Game.Screens.Play.SubmittingPlayer", "<api>k__BackingField")
            == GameField("osu.Game.OsuGameBase", "<API>k__BackingField")
        && Field(screen, "osu.Game.Screens.Play.SubmittingPlayer", "<spectatorClient>k__BackingField")
            == GameField("osu.Game.OsuGameBase", "<SpectatorClient>k__BackingField");

    bool IsPlayerLoader(ulong screen) =>
        screen != 0
        && Field(screen, "osu.Game.Screens.OsuScreen", "<logo>k__BackingField")
            == GameField("osu.Game.OsuGame", "osuLogo")
        && Field(screen, "osu.Game.Screens.Play.PlayerLoader", "<leaderboardManager>k__BackingField")
            == GameField("osu.Game.OsuGameBase", "<LeaderboardManager>k__BackingField");

    bool IsSongSelect(ulong screen)
    {
        if (screen == 0)
            return false;
        if (Offsets.TryGet("osu.Game.Screens.Select.SongSelect", "<game>k__BackingField", out int off) &&
            Memory.ReadPtrOrZero(screen + (ulong)off) == GameBase)
            return true;
        if (Offsets.TryGet("osu.Game.Screens.SelectV2.SoloSongSelect", "<game>k__BackingField", out off) &&
            Memory.ReadPtrOrZero(screen + (ulong)off) == GameBase)
            return true;
        return false;
    }

    bool IsResultScreen(ulong screen) =>
        screen != 0
        && Offsets.TryGet("osu.Game.Screens.Ranking.SoloResultsScreen", "<api>k__BackingField", out int off)
        && Memory.ReadPtrOrZero(screen + (ulong)off) == GameField("osu.Game.OsuGameBase", "<API>k__BackingField");

    bool IsEditor(ulong screen) =>
        screen != 0
        && Offsets.TryGet("osu.Game.Screens.Edit.Editor", "<api>k__BackingField", out int off)
        && Memory.ReadPtrOrZero(screen + (ulong)off) == GameField("osu.Game.OsuGameBase", "<API>k__BackingField");

    static string RulesetName(int id) => id switch
    {
        0 => "osu",
        1 => "taiko",
        2 => "catch",
        3 => "mania",
        _ => $"ruleset:{id}",
    };

    static ulong FindGameBase(ProcessMemory memory, OffsetTable offsets)
    {
        Console.Error.WriteLine("scanning for ScalingContainerTargetDrawSize…");
        int candidates = 0;
        foreach (ulong anchor in memory.Scan(DrawSizePattern))
        {
            candidates++;
            foreach (int delta in AnchorDeltas)
            {
                try
                {
                    ulong opener = memory.ReadPtr(anchor - (ulong)delta);
                    if (opener == 0)
                        continue;
                    ulong api = memory.ReadPtr(opener + (ulong)offsets["osu.Game.Online.Chat.ExternalLinkOpener", "api"]);
                    ulong game = memory.ReadPtr(api + (ulong)offsets["osu.Game.Online.API.APIAccess", "game"]);
                    if (IsGameBase(memory, offsets, game))
                    {
                        Console.Error.WriteLine($"{candidates} pattern candidate(s) tried");
                        return game;
                    }
                }
                catch
                {
                    // wrong candidate
                }
            }
        }

        throw new InvalidOperationException($"Could not resolve OsuGameDesktop ({candidates} pattern candidate(s)).");
    }

    static bool IsGameBase(ProcessMemory memory, OffsetTable offsets, ulong address)
    {
        if (!ProcessMemory.SeemsPtr(address) || memory.ReadPtrOrZero(address) == 0)
            return false;

        ulong screens = memory.ReadPtrOrZero(address + (ulong)offsets["osu.Game.OsuGame", "ScreenStack"]);
        if (!ProcessMemory.SeemsPtr(screens))
            return false;

        ulong stack = memory.ReadPtrOrZero(screens + (ulong)offsets["osu.Framework.Screens.ScreenStack", "stack"]);
        if (!memory.TryReadInt(stack + 0x10, out int depth) || depth is < 1 or > 32)
            return false;

        ulong bindable = memory.ReadPtrOrZero(address + (ulong)offsets["osu.Game.OsuGameBase", "Beatmap"]);
        ulong working = memory.BindableObject(bindable);
        if (!ProcessMemory.SeemsPtr(working))
            return false;

        ulong info = memory.ReadPtrOrZero(working + (ulong)offsets.Working_BeatmapInfo);
        return ProcessMemory.SeemsPtr(info);
    }
}

readonly record struct BeatmapSnapshot(
    ulong Working,
    ulong Info,
    ulong SetInfo,
    string Artist,
    string Title,
    string Difficulty,
    int OnlineId,
    string Hash,
    string Md5,
    float CircleSize,
    float OverallDifficulty,
    float ApproachRate,
    float DrainRate,
    int TotalObjectCount);

readonly record struct InputGraph(
    ulong DrawableRuleset,
    ulong InputManager,
    ulong KeyBindingContainer,
    bool UseParentInput,
    int[] PressedActions);
