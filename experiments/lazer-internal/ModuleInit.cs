using System.Reflection;
using System.Runtime.CompilerServices;

namespace Maniac;

static class ModuleInit
{
    [ModuleInitializer]
    internal static void Init()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            string? name = new AssemblyName(args.Name).Name;
            if (name is not "0Harmony")
                return null;

            string dir = Path.GetDirectoryName(typeof(ModuleInit).Assembly.Location) ?? "";
            string path = Path.Combine(dir, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
    }
}
