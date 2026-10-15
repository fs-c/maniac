using Microsoft.Diagnostics.Runtime;

namespace StableHost;

static class ClockHunt
{
    public static void Run(int pid)
    {
        using var target = DataTarget.AttachToProcess(pid, suspend: false);
        if (target.ClrVersions.Length == 0)
            throw new InvalidOperationException("no CLR in the target process.");
        using var runtime = target.ClrVersions[0].CreateRuntime();
        var domain = runtime.AppDomains[0];
        var fields = new List<(string Name, ClrStaticField Field, int First)>();

        foreach (var type in HeapInspect.LoadTypes(runtime))
        {
            foreach (var field in type.StaticFields)
            {
                if (field.ElementType is not (ClrElementType.Int32 or ClrElementType.Double))
                    continue;
                try
                {
                    if (!field.IsInitialized(domain))
                        continue;
                    int value = ReadAsInt(field, domain);
                    fields.Add(($"{type.Name}.{field.Name}", field, value));
                }
                catch
                {
                    // some statics are not readable from a live attach
                }
            }
        }

        Console.WriteLine($"static numeric fields: {fields.Count}");
        Thread.Sleep(250);
        var moving = new List<(string Name, int First, int Second, int Delta)>();
        foreach (var (name, field, first) in fields)
        {
            try
            {
                int second = ReadAsInt(field, domain);
                int delta = second - first;
                if (delta == 0)
                    continue;
                moving.Add((name, first, second, delta));
            }
            catch
            {
            }
        }

        Console.WriteLine("--- statics that changed in 250ms ---");
        foreach (var row in moving.OrderByDescending(r => Math.Abs(r.Delta)))
            Console.WriteLine($"{row.Delta,8}  {row.First} -> {row.Second}  {row.Name}");

        DumpType(runtime, "#=zLQ1lSsp1Sl649eje$Q==");
        DumpType(runtime, "#=zySBjyD2zKcTAsOEEDA==");
        DumpType(runtime, "#=zWaDS1PxBdCrk9m9nUg==");
        DumpEnum(runtime, "osu.OsuModes");
        DumpEnum(runtime, "osu_common.PlayModes");
    }

    static void DumpEnum(ClrRuntime runtime, string name)
    {
        Console.WriteLine($"--- enum {name} ---");
        var type = runtime.Heap.GetTypeByName(name) ?? HeapInspect.LoadTypes(runtime).FirstOrDefault(t => t.Name == name);
        if (type == null)
        {
            Console.WriteLine("  missing");
            return;
        }

        foreach (var field in type.Fields)
            Console.WriteLine($"  {field.Name} = {field.Read<int>(0, interior: true)}");
        foreach (var field in type.StaticFields)
        {
            try
            {
                Console.WriteLine($"  static {field.Name} = {field.Read<int>(runtime.AppDomains[0])}");
            }
            catch
            {
                Console.WriteLine($"  static {field.Name}");
            }
        }
    }

    static void DumpType(ClrRuntime runtime, string name)
    {
        Console.WriteLine($"--- type {name} ---");
        foreach (var type in HeapInspect.LoadTypes(runtime))
        {
            if (type.Name != name)
                continue;
            foreach (var field in type.Fields.OrderBy(f => f.Offset))
                Console.WriteLine($"  +{field.Offset:X3} {field.ElementType,-10} {field.Name} : {field.Type?.Name}");
            foreach (var field in type.StaticFields)
                Console.WriteLine($"  static {field.ElementType,-10} {field.Name} : {field.Type?.Name}");
            return;
        }

        Console.WriteLine("  missing");
    }

    static int ReadAsInt(ClrStaticField field, ClrAppDomain domain)
    {
        if (field.ElementType is ClrElementType.Double)
            return (int)field.Read<double>(domain);
        return field.Read<int>(domain);
    }
}
