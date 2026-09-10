using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Maniac.Core;

namespace Maniac.Launcher;

readonly record struct PayloadFile(string Name, byte[] Bytes);

static class PluginPayload
{
    public const string ResourcePrefix = "plugin.";

    public static string Resolve(string baseDirectory, Assembly assembly) =>
        Resolve(baseDirectory, ReadEmbedded(assembly), ManiacPaths.PayloadDirectory);

    public static string Resolve(
        string baseDirectory,
        IReadOnlyList<PayloadFile> embedded,
        string payloadRoot)
    {
        string sidecar = Path.Combine(baseDirectory, "plugin");
        if (File.Exists(Path.Combine(sidecar, "Maniac.dll")))
            return Path.GetFullPath(sidecar);

        if (embedded.Count == 0)
            throw new PayloadMissingException();

        string dest = Path.Combine(payloadRoot, Hash(embedded));
        Extract(embedded, dest);
        return dest;
    }

    public static IReadOnlyList<PayloadFile> ReadEmbedded(Assembly assembly)
    {
        var files = new List<PayloadFile>();
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                continue;

            string fileName = name[ResourcePrefix.Length..];
            if (fileName.Length == 0 || fileName.IndexOfAny(['/', '\\']) >= 0)
                continue;

            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new PayloadMissingException();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            files.Add(new PayloadFile(fileName, memory.ToArray()));
        }

        return files;
    }

    internal static string Hash(IReadOnlyList<PayloadFile> files)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            hasher.AppendData(Encoding.UTF8.GetBytes(file.Name));
            hasher.AppendData(BitConverter.GetBytes(file.Bytes.Length));
            hasher.AppendData(file.Bytes);
        }

        return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    }

    static void Extract(IReadOnlyList<PayloadFile> files, string dest)
    {
        Directory.CreateDirectory(dest);
        if (IsComplete(files, dest))
            return;

        foreach (var file in files)
        {
            string path = Path.Combine(dest, file.Name);
            File.WriteAllBytes(path, file.Bytes);
        }
    }

    static bool IsComplete(IReadOnlyList<PayloadFile> files, string dest)
    {
        foreach (var file in files)
        {
            string path = Path.Combine(dest, file.Name);
            if (!File.Exists(path))
                return false;
            long length = new FileInfo(path).Length;
            if (length != file.Bytes.Length)
                return false;
        }

        return files.Count > 0;
    }
}
