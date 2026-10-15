using Microsoft.Diagnostics.Runtime;

namespace StableHost;

static class HeapInspect
{
    const string HitObjectHint = "#=zu87$7AJCGiKbHMhV6JOStjSGnRsYcvmuog==";

    public static void Run(int pid)
    {
        using var target = Open(pid);
        using var runtime = target.ClrVersions[0].CreateRuntime();
        var heap = runtime.Heap;
        if (!heap.CanWalkHeap)
            throw new InvalidOperationException("CLR heap is not walkable (GC in progress?). Retry.");
        var types = LoadTypes(runtime);
        var counts = CountAll(heap);
        DumpNamedFields(types);
        DumpAudioTypes(types);
        DumpHitObjectFamily(types, counts);
        DumpManagers(types, counts);
    }

    public static DataTarget Open(int pid)
    {
        Console.WriteLine($"snapshot pid {pid}");
        var target = DataTarget.CreateSnapshotAndAttach(pid);
        if (target.ClrVersions.Length == 0)
            throw new InvalidOperationException("no CLR in the snapshot.");
        foreach (var version in target.ClrVersions)
            Console.WriteLine($"clr {version.Flavor} {version.Version} pointerSize={target.DataReader.PointerSize}");
        return target;
    }

    public static List<ClrType> LoadTypes(ClrRuntime runtime)
    {
        var types = new List<ClrType>();
        foreach (var module in runtime.EnumerateModules())
        {
            string moduleName = module.Name ?? "";
            if (IsFrameworkModule(moduleName))
                continue;
            foreach (var (methodTable, _) in module.EnumerateTypeDefToMethodTableMap())
            {
                var type = runtime.GetTypeByMethodTable(methodTable);
                if (type != null)
                    types.Add(type);
            }
        }

        return types;
    }

    static void DumpNamedFields(List<ClrType> types)
    {
        Console.WriteLine("--- fields named StartTime/EndTime/Column/Time/Paused ---");
        foreach (var type in types)
        {
            foreach (var field in type.Fields)
            {
                if (!IsClockOrNoteField(field.Name))
                    continue;
                Console.WriteLine($"{type.Name} +{field.Offset:X3} {field.ElementType} {field.Name} : {field.Type?.Name}");
            }

            foreach (var field in type.StaticFields)
            {
                if (!IsClockOrNoteField(field.Name))
                    continue;
                Console.WriteLine($"{type.Name} static {field.ElementType} {field.Name} : {field.Type?.Name}");
            }
        }
    }

    static void DumpAudioTypes(List<ClrType> types)
    {
        Console.WriteLine("--- audio-ish types ---");
        foreach (var type in types)
        {
            string name = type.Name ?? "";
            if (!name.Contains("Audio", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Bass", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("PlayTime", StringComparison.OrdinalIgnoreCase))
                continue;
            Console.WriteLine(name);
            DumpFields(type, "  ");
        }
    }

    static void DumpHitObjectFamily(List<ClrType> types, Dictionary<string, int> counts)
    {
        var hitObject = types.FirstOrDefault(t => t.Name == HitObjectHint)
            ?? throw new InvalidOperationException($"missing hit object type {HitObjectHint}");
        Console.WriteLine("--- hit object type ---");
        Console.WriteLine(hitObject.Name);
        DumpFields(hitObject, "  ");
        Console.WriteLine($"instances: {GetCount(counts, hitObject.Name!)}");

        Console.WriteLine("--- types assignable to hit object ---");
        foreach (var type in types)
        {
            if (type.Name == hitObject.Name)
                continue;
            if (!Inherits(type, hitObject))
                continue;
            int n = GetCount(counts, type.Name!);
            Console.WriteLine($"{n,8}  {type.Name}  base={type.BaseType?.Name}");
            DumpFields(type, "    ");
        }
    }

    static void DumpManagers(List<ClrType> types, Dictionary<string, int> counts)
    {
        Console.WriteLine("--- types with List<hit object> ---");
        foreach (var type in types)
        {
            bool match = false;
            foreach (var field in type.Fields)
            {
                string fieldType = field.Type?.Name ?? "";
                if (fieldType.Contains(HitObjectHint, StringComparison.Ordinal))
                {
                    match = true;
                    break;
                }
            }

            if (!match)
                continue;
            int n = GetCount(counts, type.Name ?? "");
            Console.WriteLine($"{n,8}  {type.Name}");
            DumpFields(type, "  ");
        }
    }

    static void DumpFields(ClrType type, string indent)
    {
        foreach (var field in type.Fields.OrderBy(f => f.Offset))
            Console.WriteLine($"{indent}+{field.Offset:X3} {field.ElementType,-10} {field.Name} : {field.Type?.Name ?? "?"}");
        foreach (var field in type.StaticFields)
            Console.WriteLine($"{indent}static {field.ElementType,-10} {field.Name} : {field.Type?.Name ?? "?"}");
    }

    static Dictionary<string, int> CountAll(ClrHeap heap)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var obj in heap.EnumerateObjects())
        {
            if (obj.Type?.Name is not { } name)
                continue;
            counts[name] = counts.TryGetValue(name, out int n) ? n + 1 : 1;
        }

        return counts;
    }

    static int GetCount(Dictionary<string, int> counts, string name) =>
        counts.TryGetValue(name, out int n) ? n : 0;

    static bool Inherits(ClrType type, ClrType ancestor)
    {
        for (var current = type.BaseType; current != null; current = current.BaseType)
        {
            if (current.Name == ancestor.Name)
                return true;
        }

        return false;
    }

    static bool IsClockOrNoteField(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith("#=", StringComparison.Ordinal))
            return false;
        return name.Contains("StartTime", StringComparison.OrdinalIgnoreCase)
            || name.Contains("EndTime", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Column", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Time", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Paused", StringComparison.OrdinalIgnoreCase)
            || name.Equals("IsPaused", StringComparison.OrdinalIgnoreCase)
            || name.Contains("PlayTime", StringComparison.OrdinalIgnoreCase)
            || name.Contains("AudioTime", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsFrameworkModule(string moduleName) =>
        moduleName.Contains("mscorlib", StringComparison.OrdinalIgnoreCase)
        || moduleName.Contains("System.", StringComparison.OrdinalIgnoreCase)
        || moduleName.Contains("Microsoft.", StringComparison.OrdinalIgnoreCase)
        || moduleName.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase);
}
