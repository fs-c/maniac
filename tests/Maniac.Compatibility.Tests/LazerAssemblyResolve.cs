using System.Reflection;
using System.Runtime.CompilerServices;

namespace Maniac.Compatibility.Tests;

static class LazerAssemblyResolve
{
    [ModuleInitializer]
    internal static void Init()
    {
        string? dir = FindLazerDir();
        if (dir == null)
            return;

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            string? name = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(name))
                return null;
            string path = Path.Combine(dir, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
    }

    static string? FindLazerDir()
    {
        string? env = Environment.GetEnvironmentVariable("LAZER_DIR");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(Path.Combine(env, "osu.Game.dll")))
            return env;

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (string name in new[] { "osulazer", "osu!lazer" })
        {
            string dir = Path.Combine(local, name, "current");
            if (File.Exists(Path.Combine(dir, "osu.Game.dll")))
                return dir;
        }

        return null;
    }
}
