using Microsoft.Diagnostics.Runtime;
using Maniac.Core.Mania;

namespace StableHost;

sealed class StableGame : IDisposable
{
    const string HitObjectTypeName = StableLayout.HitObjectType;
    const string ManiaHoldTypeName = StableLayout.ManiaHoldType;
    const int StartTimeOffset = StableLayout.StartTimeOffset;
    const int PositionOffset = StableLayout.PositionOffset;
    const int EndTimeOffset = StableLayout.EndTimeOffset;

    readonly DataTarget target;
    readonly ClrRuntime runtime;
    ClrHeap Heap => runtime.Heap;
    readonly ClrAppDomain domain;

    ClrStaticField? audioTimeField;
    ClrStaticField[]? osuModeFields;
    ClrStaticField? osuModePlayField;
    ClrStaticField? playModeField;
    ulong noteListAddress;
    DateTime nextNoteScan;
    int lastAudioTime = int.MinValue;
    DateTime lastAudioChange = DateTime.UtcNow;

    public StableGame(int pid, bool snapshot)
    {
        target = snapshot
            ? DataTarget.CreateSnapshotAndAttach(pid)
            : DataTarget.AttachToProcess(pid, suspend: false);
        if (target.ClrVersions.Length == 0)
            throw new InvalidOperationException("no CLR in the target process.");
        runtime = target.ClrVersions[0].CreateRuntime();
        domain = runtime.AppDomains[0];
        if (!runtime.Heap.CanWalkHeap)
            throw new InvalidOperationException("CLR heap is not walkable. Retry.");
        BindStatics();
    }

    public void Dispose()
    {
        runtime.Dispose();
        target.Dispose();
    }

    public Frame Read()
    {
        var status = ReadStatus();
        var notes = status.OsuMode == StableLayout.OsuModePlay ? ReadNotes() : [];
        if (status.OsuMode != StableLayout.OsuModePlay)
            noteListAddress = 0;
        return status with { Notes = notes };
    }

    public Frame ReadStatus()
    {
        int audioTime = ReadAudioTime();
        int osuMode = ReadOsuMode();
        int playMode = ReadPlayMode();
        bool paused = osuMode != StableLayout.OsuModePlay || DetectPause(audioTime);
        return new Frame(audioTime, paused, osuMode, playMode, []);
    }

    public Note[] LoadNotes()
    {
        if (!Heap.CanWalkHeap)
            throw new InvalidOperationException("CLR heap is not walkable (GC). Retry.");
        return ReadNotes();
    }

    public ManiaKeyMap ReadManiaKeys(int columns) => StableBindings.ReadMap(Heap, columns);

    public static ManiaPlayPlan ToPlan(Frame frame)
    {
        if (frame.Notes.Length == 0)
            return ManiaPlanner.Build([]);

        var xs = frame.Notes.Select(n => n.X).Distinct().OrderBy(x => x).ToArray();
        if (xs.Length == 0)
            throw new InvalidOperationException("notes have no X positions.");

        var hits = new ManiaHit[frame.Notes.Length];
        for (int i = 0; i < frame.Notes.Length; i++)
        {
            var note = frame.Notes[i];
            int column = ColumnOf(note.X, xs);
            bool hold = note.IsHold || note.End > note.Start + 1;
            hits[i] = new ManiaHit(note.Start, hold ? note.End : note.Start, column, hold, false);
        }

        return ManiaPlanner.Build(hits);
    }

    static int ColumnOf(float x, float[] sortedXs)
    {
        int best = 0;
        float bestDist = Math.Abs(x - sortedXs[0]);
        for (int i = 1; i < sortedXs.Length; i++)
        {
            float dist = Math.Abs(x - sortedXs[i]);
            if (dist < bestDist)
            {
                best = i;
                bestDist = dist;
            }
        }

        return best;
    }

    void BindStatics()
    {
        ClrType audio = RequireType(StableLayout.AudioEngineType);
        audioTimeField = RequireStatic(audio, StableLayout.AudioTimeField);
        ClrType game = RequireType(StableLayout.GameType);
        osuModeFields = new ClrStaticField[StableLayout.OsuModeFields.Length];
        for (int i = 0; i < StableLayout.OsuModeFields.Length; i++)
            osuModeFields[i] = RequireStatic(game, StableLayout.OsuModeFields[i]);
        osuModePlayField = RequireStatic(game, StableLayout.OsuModePlayField);
        playModeField = FindStaticByTypeName("osu_common.PlayModes");
        if (playModeField == null)
            throw new InvalidOperationException("no static osu_common.PlayModes field.");
        Console.WriteLine($"bound audio {StableLayout.AudioEngineType}.{StableLayout.AudioTimeField}");
        Console.WriteLine($"bound mode {playModeField.ContainingType.Name}.{playModeField.Name}");
    }

    ClrType RequireType(string name)
    {
        foreach (var type in HeapInspect.LoadTypes(runtime))
        {
            if (type.Name == name)
                return type;
        }

        throw new InvalidOperationException($"missing type {name}. This osu! build does not match the recovered layout.");
    }

    static ClrStaticField RequireStatic(ClrType type, string name)
    {
        foreach (var field in type.StaticFields)
        {
            if (field.Name == name)
                return field;
        }

        throw new InvalidOperationException($"missing static {type.Name}.{name}");
    }

    ClrStaticField? FindStaticByTypeName(string fieldTypeName)
    {
        foreach (var type in HeapInspect.LoadTypes(runtime))
        {
            foreach (var field in type.StaticFields)
            {
                if (field.Type?.Name == fieldTypeName)
                    return field;
            }
        }

        return null;
    }

    int ReadAudioTime()
    {
        if (audioTimeField == null || !audioTimeField.IsInitialized(domain))
            throw new InvalidOperationException("audio time static is not initialized.");
        return audioTimeField.Read<int>(domain);
    }

    int ReadOsuMode()
    {
        if (osuModePlayField == null)
            throw new InvalidOperationException("osu mode static was not bound.");
        return osuModePlayField.Read<int>(domain);
    }

    int ReadPlayMode()
    {
        if (playModeField == null || !playModeField.IsInitialized(domain))
            return -1;
        return playModeField.Read<int>(domain);
    }

    Note[] ReadNotes()
    {
        if (noteListAddress != 0)
        {
            var cached = Heap.GetObject(noteListAddress);
            if (cached.IsValid && IsHitObjectList(cached.Type))
            {
                var notes = ListNotes(cached);
                if (notes.Length > 0)
                    return notes;
            }

            noteListAddress = 0;
        }

        if (noteListAddress == 0 && DateTime.UtcNow < nextNoteScan)
            return [];

        Note[] best = [];
        ulong bestAddr = 0;
        foreach (var obj in Heap.EnumerateObjects())
        {
            if (!IsHitObjectList(obj.Type))
                continue;
            var notes = ListNotes(obj);
            if (notes.Length > best.Length)
            {
                best = notes;
                bestAddr = obj.Address;
            }
        }

        noteListAddress = bestAddr;
        if (best.Length == 0)
            nextNoteScan = DateTime.UtcNow.AddMilliseconds(250);
        return best;
    }

    public void ProbeLists()
    {
        int hitObjects = 0;
        Console.WriteLine("--- hit object lists ---");
        foreach (var obj in Heap.EnumerateObjects())
        {
            if (obj.Type?.Name == HitObjectTypeName || (obj.Type != null && InheritsName(obj.Type, HitObjectTypeName)))
                hitObjects++;
            if (!IsHitObjectList(obj.Type))
                continue;
            int size = ReadInt(obj, "_size");
            Console.WriteLine($"list size={size} @ 0x{obj.Address:X} type={obj.Type?.Name}");
        }

        Console.WriteLine($"hit object instances: {hitObjects}");
        Console.WriteLine($"t={ReadAudioTime()} playMode={ReadPlayMode()}");
        if (osuModeFields != null)
        {
            for (int i = 0; i < osuModeFields.Length; i++)
                Console.WriteLine($"osuMode[{i}] {osuModeFields[i].Name}={osuModeFields[i].Read<int>(domain)}");
        }

        var notes = ReadNotes();
        Console.WriteLine($"chosen list notes={notes.Length}");
        if (notes.Length == 0)
            return;

        var xs = notes.Select(n => n.X).Distinct().OrderBy(x => x).ToArray();
        int holds = notes.Count(n => n.IsHold || n.End > n.Start + 1);
        var types = notes.GroupBy(n => n.TypeName).Select(g => $"{g.Count()} {g.Key}");
        Console.WriteLine($"holds={holds} uniqueX={xs.Length} [{string.Join(", ", xs.Select(x => x.ToString("0.###")))}]");
        Console.WriteLine("types: " + string.Join("; ", types));
        DumpNote("first", notes[0]);
        DumpNote("last", notes[^1]);
        int mid = notes.Length / 2;
        DumpNote("mid", notes[mid]);
        var plan = ToPlan(new Frame(ReadAudioTime(), false, 2, ReadPlayMode(), notes));
        Console.WriteLine($"plan hits={plan.HitCount} columns={plan.Columns} events={plan.Events.Length} firstEvent={plan.Events[0].Time:0} lastEvent={plan.Events[^1].Time:0}");
        DumpHitFields(LargestList(), 0, 1, notes.Length / 2, notes.Length - 1);
        DumpFirstOfType(LargestList(), "#=zj_xBP_TgnWyeWEkkrSYRAxWvtdtVSlH$r6OaWgMSqL9TiTPNRI5tCTzr0QWN");
    }

    void DumpFirstOfType(ClrObject list, string typeName)
    {
        int size = ReadInt(list, "_size");
        var items = list.ReadObjectField("_items").AsArray();
        for (int i = 0; i < size; i++)
        {
            var hit = items.GetObjectValue(i);
            if (hit.Type?.Name != typeName)
                continue;
            Console.WriteLine($"--- first hold-like {i} ---");
            DumpHitFieldsAt(hit);
            return;
        }

        Console.WriteLine($"no object of type {typeName}");
    }

    void DumpHitFieldsAt(ClrObject hit)
    {
        Console.WriteLine($"--- object {hit.Type?.Name} ---");
        var type = hit.Type ?? throw new InvalidOperationException("hit has no type");
        foreach (var field in type.Fields.OrderBy(f => f.Offset))
        {
            string? value = FieldValue(hit, field);
            if (value == null)
                continue;
            Console.WriteLine($"  +{field.Offset:X3} {field.ElementType} {value}");
        }
    }

    ClrObject LargestList()
    {
        ClrObject best = default;
        int bestSize = -1;
        foreach (var obj in Heap.EnumerateObjects())
        {
            if (!IsHitObjectList(obj.Type))
                continue;
            int size = ReadInt(obj, "_size");
            if (size > bestSize)
            {
                best = obj;
                bestSize = size;
            }
        }

        if (bestSize < 0)
            throw new InvalidOperationException("no hit object list.");
        return best;
    }

    void DumpHitFields(ClrObject list, params int[] indexes)
    {
        var items = list.ReadObjectField("_items").AsArray();
        foreach (int i in indexes)
        {
            var hit = items.GetObjectValue(i);
            Console.WriteLine($"--- object {i} {hit.Type?.Name} ---");
            var type = hit.Type ?? throw new InvalidOperationException("hit has no type");
            foreach (var field in type.Fields.OrderBy(f => f.Offset))
            {
        string? value = FieldValue(hit, field);
                if (value == null)
                    continue;
                Console.WriteLine($"  +{field.Offset:X3} {field.ElementType} {value}");
            }
        }
    }

    static string? FieldValue(ClrObject obj, ClrInstanceField field)
    {
        try
        {
            switch (field.ElementType)
            {
                case ClrElementType.Int32:
                    return field.Read<int>(obj.Address, false).ToString();
                case ClrElementType.Int64:
                    return field.Read<long>(obj.Address, false).ToString();
                case ClrElementType.Float:
                    return field.Read<float>(obj.Address, false).ToString("0.###");
                case ClrElementType.Double:
                    return field.Read<double>(obj.Address, false).ToString("0.###");
                case ClrElementType.Boolean:
                    return field.Read<bool>(obj.Address, false).ToString();
                case ClrElementType.Struct:
                    if (field.Type?.Name == "Microsoft.Xna.Framework.Vector2")
                    {
                        var vector = field.ReadStruct(obj.Address, false);
                        return $"({vector.ReadField<float>("X"):0.###},{vector.ReadField<float>("Y"):0.###})";
                    }
                    return null;
                default:
                    return null;
            }
        }
        catch
        {
            return "?";
        }
    }

    static void DumpNote(string label, Note note)
    {
        Console.WriteLine($"{label} start={note.Start:0.###} end={note.End:0.###} x={note.X:0.###} hold={note.IsHold} type={note.TypeName}");
    }

    static bool InheritsName(ClrType type, string ancestor)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.Name == ancestor)
                return true;
        }

        return false;
    }

    static bool IsHitObjectList(ClrType? type)
    {
        string name = type?.Name ?? "";
        return name.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal)
            && name.Contains(HitObjectTypeName, StringComparison.Ordinal);
    }

    Note[] ListNotes(ClrObject list)
    {
        int size = ReadInt(list, "_size");
        if (size <= 0)
            return [];
        if (size > 20000)
            throw new InvalidOperationException($"hit object list size {size} is not plausible.");

        var itemsObj = list.ReadObjectField("_items");
        if (itemsObj.IsNull || !itemsObj.IsValid)
            throw new InvalidOperationException("hit object list has no _items.");
        var items = itemsObj.AsArray();
        var notes = new Note[size];
        for (int i = 0; i < size; i++)
        {
            var hit = items.GetObjectValue(i);
            if (hit.IsNull || !hit.IsValid)
                throw new InvalidOperationException($"hit object {i}/{size} is null.");
            notes[i] = ReadNote(hit);
        }

        return notes;
    }

    static Note ReadNote(ClrObject hit)
    {
        double start = ReadIntAt(hit, StartTimeOffset);
        double end = ReadIntAt(hit, EndTimeOffset);
        float x = ReadVectorX(hit, PositionOffset);
        string typeName = hit.Type?.Name ?? "";
        bool hold = typeName == ManiaHoldTypeName || end > start;
        return new Note(start, end, x, hold, typeName);
    }

    bool DetectPause(int audioTime)
    {
        var now = DateTime.UtcNow;
        if (audioTime != lastAudioTime)
        {
            lastAudioTime = audioTime;
            lastAudioChange = now;
            return false;
        }

        return lastAudioTime != int.MinValue && now - lastAudioChange > TimeSpan.FromMilliseconds(150);
    }

    static int ReadInt(ClrObject obj, string name)
    {
        var field = obj.Type?.GetFieldByName(name)
            ?? throw new InvalidOperationException($"{obj.Type?.Name} has no field {name}");
        return field.Read<int>(obj.Address, interior: false);
    }

    static int ReadIntAt(ClrObject obj, int offset)
    {
        var field = FieldAt(obj, offset, ClrElementType.Int32);
        return field.Read<int>(obj.Address, interior: false);
    }

    static float ReadVectorX(ClrObject obj, int offset)
    {
        var type = obj.Type ?? throw new InvalidOperationException("hit object has no type");
        ClrInstanceField? field = null;
        foreach (var candidate in type.Fields)
        {
            if (candidate.Offset == offset)
            {
                field = candidate;
                break;
            }
        }

        if (field == null)
            throw new InvalidOperationException($"{type.Name} has no field at +{offset:X}");
        var vector = field.ReadStruct(obj.Address, interior: false);
        return vector.ReadField<float>("X");
    }

    static ClrInstanceField FieldAt(ClrObject obj, int offset, ClrElementType element)
    {
        var type = obj.Type ?? throw new InvalidOperationException("hit object has no type");
        foreach (var field in type.Fields)
        {
            if (field.Offset == offset && field.ElementType == element)
                return field;
        }

        throw new InvalidOperationException($"{type.Name} has no {element} field at +{offset:X}");
    }

    public readonly record struct Note(double Start, double End, float X, bool IsHold, string TypeName);

    public readonly record struct Frame(int AudioTime, bool Paused, int OsuMode, int PlayMode, Note[] Notes);
}
