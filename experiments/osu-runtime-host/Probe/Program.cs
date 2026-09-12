using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args is ["--make-fx-deps", var src, var dest])
{
    WriteFrameworkDeps(src, dest);
    return;
}

var corelib = typeof(object).Assembly.Location;
var info = FileVersionInfo.GetVersionInfo(corelib);
Console.WriteLine($"framework {RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"corelib   {corelib}");
Console.WriteLine($"filever   {info.FileVersion}");
Console.WriteLine($"basedir   {AppContext.BaseDirectory}");
Console.WriteLine($"dotnet_root {Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? ""}");

static void WriteFrameworkDeps(string osuDepsPath, string destPath)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(osuDepsPath));
    var root = doc.RootElement;
    var rid = ".NETCoreApp,Version=v8.0/win-x64";
    const string prefix = "runtimepack.Microsoft.NETCore.App.Runtime.win-x64/";
    JsonElement pack = default;
    string version = "";
    foreach (var prop in root.GetProperty("targets").GetProperty(rid).EnumerateObject())
    {
        if (!prop.Name.StartsWith(prefix, StringComparison.Ordinal))
            continue;
        pack = prop.Value;
        version = prop.Name[prefix.Length..];
        break;
    }

    if (version.Length == 0)
        throw new InvalidOperationException($"No {prefix}* entry in {osuDepsPath}");

    var newName = $"Microsoft.NETCore.App.Runtime.win-x64/{version}";
    var native = new JsonObject();
    if (pack.TryGetProperty("native", out var nativeEl))
    {
        foreach (var n in nativeEl.EnumerateObject())
        {
            if (n.Name.Equals("hostfxr.dll", StringComparison.OrdinalIgnoreCase))
                continue;
            native[n.Name] = JsonNode.Parse(n.Value.GetRawText());
        }
    }

    var runtime = new JsonObject();
    if (pack.TryGetProperty("runtime", out var runtimeEl))
    {
        foreach (var n in runtimeEl.EnumerateObject())
            runtime[n.Name] = JsonNode.Parse(n.Value.GetRawText());
    }

    var output = new JsonObject
    {
        ["runtimeTarget"] = JsonNode.Parse(root.GetProperty("runtimeTarget").GetRawText()),
        ["compilationOptions"] = new JsonObject(),
        ["targets"] = new JsonObject
        {
            [".NETCoreApp,Version=v8.0"] = new JsonObject(),
            [rid] = new JsonObject
            {
                [newName] = new JsonObject
                {
                    ["runtime"] = runtime,
                    ["native"] = native,
                },
            },
        },
        ["libraries"] = new JsonObject
        {
            [newName] = new JsonObject
            {
                ["type"] = "runtimepack",
                ["serviceable"] = false,
                ["sha512"] = "",
            },
        },
    };

    File.WriteAllText(destPath, output.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"wrote {destPath} ({version})");
}
