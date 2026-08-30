namespace LazerInspect;

/// <summary>
/// Reads the in-memory <c>IBeatmap</c> using FieldDesc offsets from the installed assemblies.
/// Prefers the post-convert gameplay beatmap while playing.
/// </summary>
static class BeatmapMemory
{
    public readonly record struct HitObject(
        ulong Address,
        ulong MethodTable,
        double StartTime,
        double? EndTime,
        int? Column);

    public readonly record struct LoadedBeatmap(
        ulong Address,
        ulong MethodTable,
        string Source,
        int? ColumnCount,
        IReadOnlyList<HitObject> HitObjects);

    public static LoadedBeatmap? TryLoad(LazerReader reader, BeatmapSnapshot meta)
    {
        return TryLoadGameplay(reader) ?? TryLoadWorking(reader, meta);
    }

    public static LoadedBeatmap? TryLoadGameplay(LazerReader reader)
    {
        var f = reader.Offsets;
        var mem = reader.Memory;
        ulong player = reader.Player();
        if (player == 0)
            return null;

        ulong fromGameplay = ReadPtr(mem, player, f.Player_GameplayState);
        ulong beatmap = ReadPtr(mem, fromGameplay, f.GameplayState_Beatmap);
        if (beatmap != 0)
            return ReadBeatmap(reader, beatmap, "gameplay");

        ulong drawable = ReadPtr(mem, player, f.Player_DrawableRuleset);
        beatmap = ReadPtr(mem, drawable, f.DrawableRuleset_Beatmap);
        return beatmap == 0 ? null : ReadBeatmap(reader, beatmap, "gameplay");
    }

    public static LoadedBeatmap? TryLoadWorking(LazerReader reader, BeatmapSnapshot meta)
    {
        var f = reader.Offsets;
        var mem = reader.Memory;
        ulong task = ReadPtr(mem, meta.Working, f.Working_beatmapLoadTask);
        ulong loaded = ReadPtr(mem, task, f.Task_m_result);
        return loaded == 0 ? null : ReadBeatmap(reader, loaded, "working");
    }

    static LoadedBeatmap ReadBeatmap(LazerReader reader, ulong beatmap, string source)
    {
        var f = reader.Offsets;
        var mem = reader.Memory;
        ulong list = ReadPtr(mem, beatmap, f.Beatmap_HitObjects);
        ulong[] raw = mem.TryReadRefList(list, out var items) ? items : [];

        var hits = new List<HitObject>(raw.Length);
        foreach (ulong obj in raw)
        {
            if (TryReadHitObject(reader, obj, out var hit))
                hits.Add(hit);
        }

        return new LoadedBeatmap(
            beatmap,
            mem.ReadPtrOrZero(beatmap),
            source,
            ColumnCount(reader, beatmap, hits),
            hits);
    }

    static bool TryReadHitObject(LazerReader reader, ulong obj, out HitObject hit)
    {
        hit = default;
        if (!ProcessMemory.SeemsPtr(obj))
            return false;

        var f = reader.Offsets;
        var mem = reader.Memory;
        ulong bindable = ReadPtr(mem, obj, f.HitObject_StartTimeBindable);
        if (bindable == 0 || !mem.TryReadDouble(bindable + (ulong)f.BindableDouble_value, out double start) ||
            !double.IsFinite(start))
            return false;

        double? end = null;
        if (mem.TryReadDouble(obj + (ulong)f.HoldNote_duration, out double duration) &&
            double.IsFinite(duration) && duration >= 10 && duration <= 180_000 &&
            LooksLikeHoldNote(reader, obj, start, start + duration))
        {
            end = start + duration;
        }

        int? column = TryReadColumn(reader, obj);

        hit = new HitObject(obj, mem.ReadPtrOrZero(obj), start, end, column);
        return true;
    }

    static int? TryReadColumn(LazerReader reader, ulong obj)
    {
        var f = reader.Offsets;
        var mem = reader.Memory;
        ulong prop = obj + (ulong)f.ManiaHitObject_column;
        ulong bindable = mem.ReadPtrOrZero(prop + (ulong)f.HitObjectProperty_backingBindable);
        if (ProcessMemory.SeemsPtr(bindable))
        {
            if (!mem.TryReadInt(bindable + (ulong)f.BindableInt_value, out int fromBindable) ||
                fromBindable is < 0 or > 18)
                return null;
            return fromBindable;
        }

        if (!mem.TryReadInt(prop + (ulong)f.HitObjectProperty_backingValue, out int fromBacking) ||
            fromBacking is < 0 or > 18)
            return null;
        return fromBacking;
    }

    static int? ColumnCount(LazerReader reader, ulong beatmap, IReadOnlyList<HitObject> hits)
    {
        var f = reader.Offsets;
        var mem = reader.Memory;
        if (ObjectBaseSize(mem, beatmap) >= f.ManiaBeatmap_Stages + 8)
        {
            ulong stages = ReadPtr(mem, beatmap, f.ManiaBeatmap_Stages);
            if (mem.TryReadRefList(stages, out ulong[] items) && items.Length is >= 1 and <= 4)
            {
                int total = 0;
                bool ok = true;
                foreach (ulong stage in items)
                {
                    if (!mem.TryReadInt(stage + (ulong)f.StageDefinition_Columns, out int n) || n is < 1 or > 18)
                    {
                        ok = false;
                        break;
                    }
                    total += n;
                }
                if (ok && total is >= 1 and <= 18)
                    return total;
            }
        }

        var cols = hits.Select(h => h.Column).OfType<int>().ToList();
        if (cols.Count == 0)
            return null;

        var sorted = cols.Distinct().Where(c => c is >= 0 and <= 18).OrderBy(c => c).ToList();
        if (sorted.Count == 0)
            return null;
        int last = sorted[0];
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] > last + 1)
                break;
            last = sorted[i];
        }
        return last + 1;
    }

    static ulong ReadPtr(ProcessMemory mem, ulong obj, int offset) =>
        obj == 0 ? 0 : mem.ReadPtrOrZero(obj + (ulong)offset);

    static bool LooksLikeHoldNote(LazerReader reader, ulong obj, double start, double end)
    {
        var f = reader.Offsets;
        var mem = reader.Memory;
        ulong head = ReadPtr(mem, obj, f.HoldNote_Head);
        ulong tail = ReadPtr(mem, obj, f.HoldNote_Tail);
        if (!ProcessMemory.SeemsPtr(head) || !ProcessMemory.SeemsPtr(tail) || head == obj || tail == obj)
            return false;
        return TryStart(reader, head, out double headStart)
            && TryStart(reader, tail, out double tailStart)
            && Math.Abs(headStart - start) < 1
            && Math.Abs(tailStart - end) < 1;
    }

    static bool TryStart(LazerReader reader, ulong obj, out double start)
    {
        var f = reader.Offsets;
        var mem = reader.Memory;
        ulong bindable = ReadPtr(mem, obj, f.HitObject_StartTimeBindable);
        start = 0;
        return bindable != 0
            && mem.TryReadDouble(bindable + (ulong)f.BindableDouble_value, out start)
            && double.IsFinite(start);
    }

    static int ObjectBaseSize(ProcessMemory mem, ulong obj)
    {
        ulong mt = mem.ReadPtrOrZero(obj);
        if (mt == 0 || !mem.TryReadInt(mt + 4, out int size) || size is < 16 or > 0x10000)
            return 0;
        return size;
    }
}
