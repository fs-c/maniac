using Maniac.Launcher;
using Xunit;

namespace Maniac.Launcher.Tests;

public class PluginPayloadTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "maniac-lazer-tests", Guid.NewGuid().ToString("N"));

    public PluginPayloadTests()
    {
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); }
        catch { /* leftover */ }
    }

    [Fact]
    public void Sidecar_wins_over_embedded()
    {
        string baseDir = Path.Combine(root, "app");
        string sidecar = Path.Combine(baseDir, "plugin");
        Directory.CreateDirectory(sidecar);
        File.WriteAllText(Path.Combine(sidecar, "Maniac.dll"), "sidecar");

        string dest = PluginPayload.Resolve(baseDir, [new PayloadFile("Maniac.dll", "embedded"u8.ToArray())], Path.Combine(root, "payload"));
        Assert.Equal(Path.GetFullPath(sidecar), dest);
        Assert.Equal("sidecar", File.ReadAllText(Path.Combine(dest, "Maniac.dll")));
    }

    [Fact]
    public void Missing_sidecar_and_embedded_is_incomplete()
    {
        string baseDir = Path.Combine(root, "empty-app");
        Directory.CreateDirectory(baseDir);
        var ex = Assert.Throws<PayloadMissingException>(() =>
            PluginPayload.Resolve(baseDir, [], Path.Combine(root, "payload")));
        Assert.Equal(CliMessages.Incomplete, ex.Message);
    }

    [Fact]
    public void Extracts_to_stable_hash_and_skips_rewrite()
    {
        string baseDir = Path.Combine(root, "shipped");
        Directory.CreateDirectory(baseDir);
        var files = new PayloadFile[]
        {
            new("Maniac.dll", [1, 2, 3]),
            new("Maniac.Plugin.dll", [4, 5]),
        };
        string payloadRoot = Path.Combine(root, "payload");

        string first = PluginPayload.Resolve(baseDir, files, payloadRoot);
        Assert.Equal(Path.Combine(payloadRoot, PluginPayload.Hash(files)), first);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(first, "Maniac.dll")));

        string dllPath = Path.Combine(first, "Maniac.dll");
        File.SetLastWriteTimeUtc(dllPath, DateTime.UtcNow.AddMinutes(-5));
        var stamp = File.GetLastWriteTimeUtc(dllPath);
        string second = PluginPayload.Resolve(baseDir, files, payloadRoot);
        Assert.Equal(first, second);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(dllPath));
    }

    [Fact]
    public void Hash_is_order_independent()
    {
        var a = new PayloadFile[] { new("b.dll", [2]), new("a.dll", [1]) };
        var b = new PayloadFile[] { new("a.dll", [1]), new("b.dll", [2]) };
        Assert.Equal(PluginPayload.Hash(a), PluginPayload.Hash(b));
        Assert.NotEqual(PluginPayload.Hash(a), PluginPayload.Hash([new("a.dll", [1])]));
    }

    [Fact]
    public void Rewrites_incomplete_extract()
    {
        string baseDir = Path.Combine(root, "partial-app");
        Directory.CreateDirectory(baseDir);
        var files = new PayloadFile[] { new("Maniac.dll", [9, 9, 9]) };
        string payloadRoot = Path.Combine(root, "payload");
        string dest = Path.Combine(payloadRoot, PluginPayload.Hash(files));
        Directory.CreateDirectory(dest);
        File.WriteAllBytes(Path.Combine(dest, "Maniac.dll"), [1]);

        PluginPayload.Resolve(baseDir, files, payloadRoot);
        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(Path.Combine(dest, "Maniac.dll")));
    }

    [Fact]
    public void Launcher_embeds_plugin_payload()
    {
        var files = PluginPayload.ReadEmbedded(typeof(Program).Assembly);
        Assert.Contains(files, f => f.Name == "Maniac.dll" && f.Bytes.Length > 0);
        Assert.Contains(files, f => f.Name == "Maniac.Plugin.dll" && f.Bytes.Length > 0);
        Assert.Contains(files, f => f.Name == "Maniac.Core.dll" && f.Bytes.Length > 0);
        Assert.Contains(files, f => f.Name == "Maniac.runtimeconfig.json" && f.Bytes.Length > 0);
    }
}
