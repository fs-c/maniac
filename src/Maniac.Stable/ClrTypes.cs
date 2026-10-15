using Microsoft.Diagnostics.Runtime;

namespace Maniac.Stable;

static class ClrTypes
{
    public static List<ClrType> Load(ClrRuntime runtime)
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

    public static ClrType Require(IReadOnlyList<ClrType> types, string name)
    {
        foreach (var type in types)
        {
            if (type.Name == name)
                return type;
        }

        throw new InvalidOperationException($"missing type {name}. This osu! build does not match the recovered layout.");
    }

    public static ClrStaticField RequireStatic(ClrType type, string name)
    {
        foreach (var field in type.StaticFields)
        {
            if (field.Name == name)
                return field;
        }

        throw new InvalidOperationException($"missing static {type.Name}.{name}");
    }

    static bool IsFrameworkModule(string moduleName) =>
        moduleName.Contains("mscorlib", StringComparison.OrdinalIgnoreCase)
        || moduleName.Contains("System.", StringComparison.OrdinalIgnoreCase)
        || moduleName.Contains("Microsoft.", StringComparison.OrdinalIgnoreCase)
        || moduleName.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase);
}
