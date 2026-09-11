using System.Reflection;

namespace Maniac.Core;

public static class ManiacVersion
{
    public static string Current { get; } = FromAssembly(typeof(ManiacVersion).Assembly);

    public static string FromAssembly(Assembly assembly)
    {
        string? raw = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException($"{assembly.GetName().Name} is missing AssemblyInformationalVersion.");
        return Display(raw);
    }

    public static string Display(string informationalVersion)
    {
        int plus = informationalVersion.IndexOf('+');
        string version = plus >= 0 ? informationalVersion[..plus] : informationalVersion;
        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException($"Empty informational version '{informationalVersion}'.");
        return version;
    }
}
