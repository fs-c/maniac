using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SharpCompress.Compressors.LZMA;

namespace ManiaHuman;

enum GameMode : byte
{
    Osu = 0,
    Taiko = 1,
    Catch = 2,
    Mania = 3,
}

[Flags]
enum LegacyMods
{
    None = 0,
    NoFail = 1 << 0,
    Easy = 1 << 1,
    Hidden = 1 << 3,
    HardRock = 1 << 4,
    SuddenDeath = 1 << 5,
    DoubleTime = 1 << 6,
    Relax = 1 << 7,
    HalfTime = 1 << 8,
    Nightcore = 1 << 9,
    Flashlight = 1 << 10,
    Autoplay = 1 << 11,
    SpunOut = 1 << 12,
    Autopilot = 1 << 13,
    Perfect = 1 << 14,
    Key4 = 1 << 15,
    Key5 = 1 << 16,
    Key6 = 1 << 17,
    Key7 = 1 << 18,
    Key8 = 1 << 19,
    FadeIn = 1 << 20,
    Random = 1 << 21,
    Cinema = 1 << 22,
    Key9 = 1 << 24,
    Coop = 1 << 25,
    Key1 = 1 << 26,
    Key3 = 1 << 27,
    Key2 = 1 << 28,
    ScoreV2 = 1 << 29,
    Mirror = 1 << 30,
}

readonly struct ReplayFrame
{
    public readonly double Time;
    public readonly int Keys;

    public ReplayFrame(double time, int keys)
    {
        Time = time;
        Keys = keys;
    }
}

readonly struct KeyEvent
{
    public readonly double Time;
    public readonly byte Column;
    public readonly bool Down;

    public KeyEvent(double time, byte column, bool down)
    {
        Time = time;
        Column = column;
        Down = down;
    }
}

sealed class ModInfo
{
    public string Acronym { get; init; } = "";
    public double? SpeedChange { get; init; }
}

sealed class OsrReplay
{
    public GameMode Mode;
    public int Version;
    public string BeatmapMd5 = "";
    public string Player = "";
    public string ReplayMd5 = "";
    public int Count300, Count100, Count50, CountGeki, CountKatu, CountMiss;
    public int TotalScore;
    public int MaxCombo;
    public bool Perfect;
    public LegacyMods LegacyMods;
    public DateTime Timestamp;
    public long OnlineId;
    public ReplayFrame[] Frames = [];
    public KeyEvent[] Events = [];
    public ModInfo[] Mods = [];
    public string? SourcePath;
    public string? ClientVersion;
    public bool IsLazer => Version >= 30_000_000;

    public double Rate
    {
        get
        {
            foreach (var mod in Mods)
            {
                if (mod.SpeedChange is double s && s > 0)
                    return s;
            }

            if (LegacyMods.HasFlag(LegacyMods.DoubleTime) || LegacyMods.HasFlag(LegacyMods.Nightcore))
                return 1.5;
            if (LegacyMods.HasFlag(LegacyMods.HalfTime))
                return 0.75;
            return 1;
        }
    }

    public bool HasClassicWindows => !IsLazer || HasAcronym("CL");
    public bool Mirror => LegacyMods.HasFlag(LegacyMods.Mirror) || HasAcronym("MR");
    public bool Unusable => HasAcronym("RD") || HasAcronym("AU") || HasAcronym("CN") || HasAcronym("RX") || HasAcronym("AP")
                            || LegacyMods.HasFlag(LegacyMods.Random) || LegacyMods.HasFlag(LegacyMods.Autoplay)
                            || LegacyMods.HasFlag(LegacyMods.Cinema) || LegacyMods.HasFlag(LegacyMods.Relax)
                            || LegacyMods.HasFlag(LegacyMods.Autopilot);

    public int? KeyModCount
    {
        get
        {
            if (LegacyMods.HasFlag(LegacyMods.Key9) || HasAcronym("9K")) return 9;
            if (LegacyMods.HasFlag(LegacyMods.Key8) || HasAcronym("8K")) return 8;
            if (LegacyMods.HasFlag(LegacyMods.Key7) || HasAcronym("7K")) return 7;
            if (LegacyMods.HasFlag(LegacyMods.Key6) || HasAcronym("6K")) return 6;
            if (LegacyMods.HasFlag(LegacyMods.Key5) || HasAcronym("5K")) return 5;
            if (LegacyMods.HasFlag(LegacyMods.Key4) || HasAcronym("4K")) return 4;
            if (LegacyMods.HasFlag(LegacyMods.Key3) || HasAcronym("3K")) return 3;
            if (LegacyMods.HasFlag(LegacyMods.Key2) || HasAcronym("2K")) return 2;
            if (LegacyMods.HasFlag(LegacyMods.Key1) || HasAcronym("1K")) return 1;
            return null;
        }
    }

    public bool HasAcronym(string acronym)
    {
        foreach (var mod in Mods)
        {
            if (string.Equals(mod.Acronym, acronym, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public string ModsLabel
    {
        get
        {
            if (Mods.Length > 0)
                return string.Join("", Mods.Select(m => m.Acronym));
            if (LegacyMods == LegacyMods.None)
                return "NM";
            return LegacyMods.ToString().Replace(" ", "");
        }
    }
}

sealed class ManiaNote
{
    public double Start;
    public double End;
    public int Column;
    public bool IsHold;
}

sealed class ManiaBeatmap
{
    public string Title = "";
    public string Artist = "";
    public string Version = "";
    public string Md5 = "";
    public int Mode;
    public float CircleSize = 4;
    public float OverallDifficulty = 8;
    public int BeatmapId;
    public int BeatmapSetId;
    public int FileFormat;
    public ManiaNote[] Notes = [];
    public string? SourcePath;

    public int Columns => Math.Clamp((int)Math.Round(CircleSize), 1, 18);
    public string DisplayName => $"{Artist} - {Title} [{Version}]";
    public bool IsMania => Mode == 3;
}

static class ReplayIO
{
    public static OsrReplay ParseOsr(string path) => ParseOsr(File.OpenRead(path), path);

    public static OsrReplay ParseOsr(Stream stream, string? path = null)
    {
        using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        var replay = new OsrReplay { SourcePath = path };
        replay.Mode = (GameMode)br.ReadByte();
        replay.Version = br.ReadInt32();
        replay.BeatmapMd5 = ReadOsuString(br);
        replay.Player = ReadOsuString(br);
        replay.ReplayMd5 = ReadOsuString(br);
        replay.Count300 = br.ReadUInt16();
        replay.Count100 = br.ReadUInt16();
        replay.Count50 = br.ReadUInt16();
        replay.CountGeki = br.ReadUInt16();
        replay.CountKatu = br.ReadUInt16();
        replay.CountMiss = br.ReadUInt16();
        replay.TotalScore = br.ReadInt32();
        replay.MaxCombo = br.ReadUInt16();
        replay.Perfect = br.ReadBoolean();
        replay.LegacyMods = (LegacyMods)br.ReadInt32();
        ReadOsuString(br); // life graph
        long ticks = br.ReadInt64();
        replay.Timestamp = ticks > 0 && ticks < DateTime.MaxValue.Ticks
            ? new DateTime(ticks, DateTimeKind.Utc)
            : DateTime.UnixEpoch;

        int replayLen = br.ReadInt32();
        byte[] compressed = replayLen > 0 ? br.ReadBytes(replayLen) : [];
        if (replay.Version >= 20140721 && br.BaseStream.Position + 8 <= br.BaseStream.Length)
            replay.OnlineId = br.ReadInt64();
        else if (replay.Version >= 20121008 && br.BaseStream.Position + 4 <= br.BaseStream.Length)
            replay.OnlineId = br.ReadInt32();

        if (replay.Version >= 30_000_001 && br.BaseStream.Position + 4 <= br.BaseStream.Length)
        {
            int extraLen = br.ReadInt32();
            if (extraLen > 0 && br.BaseStream.Position + extraLen <= br.BaseStream.Length)
                ApplyScoreInfo(replay, br.ReadBytes(extraLen));
        }

        replay.Mods = replay.Mods.Length > 0 ? replay.Mods : FromLegacy(replay.LegacyMods);
        replay.Frames = compressed.Length > 0 ? ParseFrames(DecompressLzma(compressed), replay.Mode, offset: 0) : [];
        replay.Events = ToKeyEvents(replay.Frames);
        return replay;
    }

    public static OsrReplay ParseV1ReplayData(string player, string beatmapMd5, LegacyMods mods, string lzmaBase64)
    {
        byte[] compressed = Convert.FromBase64String(lzmaBase64);
        var replay = new OsrReplay
        {
            Mode = GameMode.Mania,
            Player = player,
            BeatmapMd5 = beatmapMd5,
            LegacyMods = mods,
            Mods = FromLegacy(mods),
            Frames = ParseFrames(DecompressLzma(compressed), GameMode.Mania, 0),
        };
        replay.Events = ToKeyEvents(replay.Frames);
        return replay;
    }

    public static bool TryParseOsr(string path, out OsrReplay replay)
    {
        replay = null!;
        try
        {
            replay = ParseOsr(path);
            return replay.Mode == GameMode.Mania && replay.Frames.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static ManiaBeatmap ParseOsu(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var map = ParseOsu(bytes);
        map.SourcePath = path;
        return map;
    }

    public static ManiaBeatmap ParseOsu(byte[] bytes)
    {
        var map = new ManiaBeatmap { Md5 = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant() };
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string section = "";
        var notes = new List<ManiaNote>(256);
        int columns = 4;
        while (reader.ReadLine() is string raw)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;
            if (line.StartsWith("osu file format v", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.AsSpan("osu file format v".Length), out int ver))
            {
                map.FileFormat = ver;
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
                continue;
            }

            if (section is "General" or "Metadata" or "Difficulty")
            {
                int colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;
                string key = line[..colon].Trim();
                string value = line[(colon + 1)..].Trim();
                switch (key)
                {
                    case "Mode": map.Mode = ParseInt(value); break;
                    case "Title": map.Title = value; break;
                    case "Artist": map.Artist = value; break;
                    case "Version": map.Version = value; break;
                    case "BeatmapID": map.BeatmapId = ParseInt(value); break;
                    case "BeatmapSetID": map.BeatmapSetId = ParseInt(value); break;
                    case "CircleSize":
                        map.CircleSize = ParseFloat(value);
                        columns = map.Columns;
                        break;
                    case "OverallDifficulty": map.OverallDifficulty = ParseFloat(value); break;
                }

                continue;
            }

            if (section != "HitObjects")
                continue;

            var note = ParseHitObject(line, columns, map.FileFormat < 5 ? 24 : 0);
            if (note is { } n)
                notes.Add(n);
        }

        notes.Sort((a, b) =>
        {
            int t = a.Start.CompareTo(b.Start);
            return t != 0 ? t : a.Column.CompareTo(b.Column);
        });
        map.Notes = notes.ToArray();
        return map;
    }

    public static int ColumnFromX(float x, int columns)
    {
        if (columns <= 0)
            columns = 1;
        return Math.Clamp((int)MathF.Floor(x * columns / 512f), 0, columns - 1);
    }

    public static KeyEvent[] ToKeyEvents(ReadOnlySpan<ReplayFrame> frames)
    {
        var events = new List<KeyEvent>();
        int prev = 0;
        foreach (var frame in frames)
        {
            int changed = frame.Keys ^ prev;
            if (changed != 0)
            {
                for (int c = 0; c < 20; c++)
                {
                    if ((changed & (1 << c)) == 0)
                        continue;
                    events.Add(new KeyEvent(frame.Time, (byte)c, (frame.Keys & (1 << c)) != 0));
                }
            }

            prev = frame.Keys;
        }

        return events.ToArray();
    }

    static ManiaNote? ParseHitObject(string line, int columns, double offset)
    {
        // x,y,time,type,hitSound[,endTime:...]
        var parts = line.Split(',');
        if (parts.Length < 5)
            return null;
        float x = ParseFloat(parts[0]);
        double time = ParseFloat(parts[2]) + offset;
        int type = ParseInt(parts[3]);
        bool hold = (type & 128) != 0;
        bool circle = (type & 1) != 0;
        if (!hold && !circle)
            return null;

        double end = time;
        if (hold && parts.Length > 5)
        {
            string rest = parts[5];
            int cut = rest.IndexOf(':');
            end = ParseFloat(cut >= 0 ? rest[..cut] : rest) + offset;
            if (end < time)
                end = time;
        }

        return new ManiaNote
        {
            Start = time,
            End = end,
            Column = ColumnFromX(x, columns),
            IsHold = hold && end > time,
        };
    }

    static ReplayFrame[] ParseFrames(string data, GameMode mode, double offset)
    {
        var frames = new List<ReplayFrame>();
        double time = offset;
        var chunks = data.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (string chunk in chunks)
        {
            var p = chunk.Split('|');
            if (p.Length < 4)
                continue;
            if (p[0] == "-12345")
                continue;
            if (!TryParseDelta(p[0], out double delta))
                continue;
            float x = ParseFloat(p[1]);
            float y = ParseFloat(p[2]);
            time += delta;
            frames.Add(new ReplayFrame(time, mode == GameMode.Mania ? (int)x : ParseInt(p[3])));
            // skip the two stable intro frames at (256, -500)
            if (frames.Count <= 2 && x == 256 && y == -500)
                frames.RemoveAt(frames.Count - 1);
        }

        // stable sometimes emits a bogus first-frame time; never go backwards
        var cleaned = new List<ReplayFrame>(frames.Count);
        double last = double.NegativeInfinity;
        foreach (var f in frames)
        {
            if (f.Time < last)
                continue;
            cleaned.Add(f);
            last = f.Time;
        }

        return cleaned.ToArray();
    }

    static bool TryParseDelta(string s, out double delta)
    {
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
        {
            delta = i;
            return true;
        }

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            delta = Math.Round(d);
            return true;
        }

        delta = 0;
        return false;
    }

    static string DecompressLzma(byte[] compressed)
    {
        if (compressed.Length < 13)
            return "";
        var properties = compressed.AsSpan(0, 5).ToArray();
        long outSize = 0;
        for (int i = 0; i < 8; i++)
            outSize |= (long)compressed[5 + i] << (8 * i);
        long compressedSize = compressed.Length - 13;
        using var input = new MemoryStream(compressed, 13, (int)compressedSize);
        using var lzma = LzmaStream.Create(properties, input, compressedSize, outSize);
        using var reader = new StreamReader(lzma, Encoding.ASCII);
        return reader.ReadToEnd();
    }

    static void ApplyScoreInfo(OsrReplay replay, byte[] compressed)
    {
        string json;
        try { json = DecompressLzma(compressed); }
        catch { return; }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("online_id", out var oid) && oid.TryGetInt64(out long id) && id > 0)
                replay.OnlineId = id;
            if (root.TryGetProperty("client_version", out var cv))
                replay.ClientVersion = cv.GetString();
            if (root.TryGetProperty("mods", out var mods) && mods.ValueKind == JsonValueKind.Array)
            {
                var list = new List<ModInfo>();
                foreach (var m in mods.EnumerateArray())
                {
                    string acronym = m.TryGetProperty("acronym", out var a) ? a.GetString() ?? "" : "";
                    double? speed = null;
                    if (m.TryGetProperty("settings", out var settings)
                        && settings.TryGetProperty("speed_change", out var sc)
                        && sc.TryGetDouble(out double s))
                        speed = s;
                    if (acronym.Length > 0)
                        list.Add(new ModInfo { Acronym = acronym, SpeedChange = speed });
                }

                if (list.Count > 0)
                    replay.Mods = list.ToArray();
            }
        }
        catch
        {
            // extra blob is best-effort
        }
    }

    static ModInfo[] FromLegacy(LegacyMods mods)
    {
        var list = new List<ModInfo>();
        void add(LegacyMods flag, string acronym)
        {
            if (mods.HasFlag(flag))
                list.Add(new ModInfo { Acronym = acronym });
        }

        add(LegacyMods.Easy, "EZ");
        add(LegacyMods.NoFail, "NF");
        add(LegacyMods.Hidden, "HD");
        add(LegacyMods.HardRock, "HR");
        add(LegacyMods.SuddenDeath, "SD");
        add(LegacyMods.DoubleTime, "DT");
        add(LegacyMods.Nightcore, "NC");
        add(LegacyMods.HalfTime, "HT");
        add(LegacyMods.Flashlight, "FL");
        add(LegacyMods.FadeIn, "FI");
        add(LegacyMods.Perfect, "PF");
        add(LegacyMods.Mirror, "MR");
        add(LegacyMods.Random, "RD");
        add(LegacyMods.Autoplay, "AU");
        add(LegacyMods.ScoreV2, "SV2");
        add(LegacyMods.Key4, "4K");
        add(LegacyMods.Key5, "5K");
        add(LegacyMods.Key6, "6K");
        add(LegacyMods.Key7, "7K");
        add(LegacyMods.Key8, "8K");
        add(LegacyMods.Key9, "9K");
        return list.ToArray();
    }

    static string ReadOsuString(BinaryReader br)
    {
        byte kind = br.ReadByte();
        if (kind == 0)
            return "";
        return br.ReadString();
    }

    static int ParseInt(string s) => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    static float ParseFloat(string s) => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float n) ? n : 0;
}
