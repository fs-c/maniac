using Microsoft.Diagnostics.Runtime;
using Maniac.Core.Mania;

namespace Maniac.Stable;

sealed class StableGame : IDisposable
{
    const string HitObjectTypeName = StableLayout.HitObjectType;
    const string ManiaHoldTypeName = StableLayout.ManiaHoldType;
    const int StartTimeOffset = StableLayout.StartTimeOffset;
    const int PositionOffset = StableLayout.PositionOffset;
    const int EndTimeOffset = StableLayout.EndTimeOffset;

    readonly DataTarget target;
    readonly ClrRuntime runtime;
    readonly ClrAppDomain domain;
    readonly List<ClrType> types;
    ClrHeap Heap => runtime.Heap;

    ClrStaticField? audioTimeField;
    ClrStaticField? osuModePlayField;
    ClrStaticField? playModeField;
    ulong noteListAddress;
    DateTime nextNoteScan;
    int lastAudioTime = int.MinValue;
    DateTime lastAudioChange = DateTime.UtcNow;

    public StableGame(int pid)
    {
        target = DataTarget.AttachToProcess(pid, suspend: false);
        if (target.ClrVersions.Length == 0)
            throw new InvalidOperationException("no CLR in the target process.");
        runtime = target.ClrVersions[0].CreateRuntime();
        domain = runtime.AppDomains[0];
        if (!runtime.Heap.CanWalkHeap)
            throw new InvalidOperationException("CLR heap is not walkable. Retry.");
        types = ClrTypes.Load(runtime);
        BindStatics();
    }

    public void Dispose()
    {
        runtime.Dispose();
        target.Dispose();
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
        ClrType audio = ClrTypes.Require(types, StableLayout.AudioEngineType);
        audioTimeField = ClrTypes.RequireStatic(audio, StableLayout.AudioTimeField);
        ClrType game = ClrTypes.Require(types, StableLayout.GameType);
        foreach (string name in StableLayout.OsuModeFields)
            ClrTypes.RequireStatic(game, name);
        osuModePlayField = ClrTypes.RequireStatic(game, StableLayout.OsuModePlayField);
        playModeField = FindStaticByTypeName("osu_common.PlayModes");
        if (playModeField == null)
            throw new InvalidOperationException("no static osu_common.PlayModes field.");
    }

    ClrStaticField? FindStaticByTypeName(string fieldTypeName)
    {
        foreach (var type in types)
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
