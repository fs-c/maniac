using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace LazerInspect;

/// <summary>
/// Instance field offsets dumped from CoreCLR FieldDesc of the running client's assemblies.
/// Offsets include the MethodTable pointer (first field of a class is typically at 8).
/// </summary>
sealed class OffsetTable
{
    const BindingFlags InstanceDeclared =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    static readonly string[] RequiredTypes =
    [
        "osu.Game.OsuGame",
        "osu.Game.OsuGameBase",
        "osu.Framework.Screens.ScreenStack",
        "osu.Game.Rulesets.RulesetInfo",
        "osu.Game.Beatmaps.FramedBeatmapClock",
        "osu.Framework.Timing.FramedClock",
        "osu.Game.Beatmaps.WorkingBeatmap",
        "osu.Game.Beatmaps.WorkingBeatmapCache+BeatmapManagerWorkingBeatmap",
        "osu.Game.Beatmaps.BeatmapInfo",
        "osu.Game.Beatmaps.BeatmapMetadata",
        "osu.Game.Beatmaps.BeatmapDifficulty",
        "osu.Game.Screens.Play.SubmittingPlayer",
        "osu.Game.Screens.Play.Player",
        "osu.Game.Screens.Play.PlayerLoader",
        "osu.Game.Screens.OsuScreen",
        "osu.Game.Online.Chat.ExternalLinkOpener",
        "osu.Game.Online.API.APIAccess",
        "osu.Game.Screens.Play.GameplayState",
        "osu.Game.Rulesets.Objects.HitObject",
        "osu.Game.Rulesets.Mania.Objects.ManiaHitObject",
        "osu.Game.Rulesets.Mania.Objects.HoldNote",
        "osu.Game.Rulesets.Mania.Beatmaps.ManiaBeatmap",
        "osu.Game.Rulesets.Mania.Beatmaps.StageDefinition",
    ];

    static readonly string[] OptionalTypes =
    [
        "osu.Desktop.OsuGameDesktop",
        "osu.Game.Screens.Select.SongSelect",
        "osu.Game.Screens.Select.SoloSongSelect",
        "osu.Game.Screens.SelectV2.SoloSongSelect",
        "osu.Game.Screens.SelectV2.SongSelect",
        "osu.Game.Screens.Ranking.SoloResultsScreen",
        "osu.Game.Screens.Edit.Editor",
    ];

    readonly Dictionary<string, Dictionary<string, int>> _types;

    public string GameDirectory { get; }
    public string OsuVersion { get; }

    public int Working_BeatmapInfo { get; }
    public int Working_beatmapLoadTask { get; }
    public int Task_m_result { get; }
    public int Beatmap_HitObjects { get; }
    public int Beatmap_beatmapInfo { get; }
    public int HitObject_StartTimeBindable { get; }
    public int ManiaHitObject_column { get; }
    public int HoldNote_duration { get; }
    public int HoldNote_Head { get; }
    public int HoldNote_Tail { get; }
    public int Player_GameplayState { get; }
    public int Player_DrawableRuleset { get; }
    public int GameplayState_Beatmap { get; }
    public int DrawableRuleset_Beatmap { get; }
    public int BindableDouble_value { get; }
    public int BindableInt_value { get; }
    public int ManiaBeatmap_Stages { get; }
    public int StageDefinition_Columns { get; }
    public int HitObjectProperty_backingBindable { get; }
    public int HitObjectProperty_backingValue { get; }
    public int DrawableRuleset_KeyBindingInputManager { get; }
    public int RulesetInputManager_KeyBindingContainer { get; }
    public int KeyBindingContainer_pressedActions { get; }
    public int PassThrough_useParentInput { get; }

    OffsetTable(string gameDirectory, string version, Dictionary<string, Dictionary<string, int>> types)
    {
        GameDirectory = gameDirectory;
        OsuVersion = version;
        _types = types;

        Working_BeatmapInfo = this["osu.Game.Beatmaps.WorkingBeatmap", "BeatmapInfo"];
        Working_beatmapLoadTask = this["osu.Game.Beatmaps.WorkingBeatmap", "beatmapLoadTask"];
        HitObject_StartTimeBindable = this["osu.Game.Rulesets.Objects.HitObject", "StartTimeBindable"];
        Player_GameplayState = this["osu.Game.Screens.Play.Player", "GameplayState"];
        Player_DrawableRuleset = this["osu.Game.Screens.Play.Player", "DrawableRuleset"];
        GameplayState_Beatmap = this["osu.Game.Screens.Play.GameplayState", "Beatmap"];
        Beatmap_HitObjects = this["osu.Game.Beatmaps.Beatmap`1", "HitObjects"];
        Beatmap_beatmapInfo = this["osu.Game.Beatmaps.Beatmap`1", "beatmapInfo"];
        DrawableRuleset_Beatmap = this["osu.Game.Rulesets.UI.DrawableRuleset`1", "Beatmap"];
        BindableDouble_value = this["osu.Framework.Bindables.Bindable`1[Double]", "value"];
        BindableInt_value = this["osu.Framework.Bindables.Bindable`1[Int32]", "value"];
        Task_m_result = this["System.Threading.Tasks.Task`1", "m_result"];
        HitObjectProperty_backingBindable = this["osu.Game.Rulesets.Objects.HitObjectProperty`1", "backingBindable"];
        HitObjectProperty_backingValue = this["osu.Game.Rulesets.Objects.HitObjectProperty`1", "backingValue"];
        ManiaHitObject_column = this["osu.Game.Rulesets.Mania.Objects.ManiaHitObject", "column"];
        HoldNote_duration = this["osu.Game.Rulesets.Mania.Objects.HoldNote", "duration"];
        HoldNote_Head = this["osu.Game.Rulesets.Mania.Objects.HoldNote", "Head"];
        HoldNote_Tail = this["osu.Game.Rulesets.Mania.Objects.HoldNote", "Tail"];
        ManiaBeatmap_Stages = this["osu.Game.Rulesets.Mania.Beatmaps.ManiaBeatmap", "Stages"];
        StageDefinition_Columns = this["osu.Game.Rulesets.Mania.Beatmaps.StageDefinition", "Columns"];
        DrawableRuleset_KeyBindingInputManager = this["osu.Game.Rulesets.UI.DrawableRuleset`1", "KeyBindingInputManager"];
        RulesetInputManager_KeyBindingContainer = this["osu.Game.Rulesets.UI.RulesetInputManager`1", "KeyBindingContainer"];
        KeyBindingContainer_pressedActions = this["osu.Framework.Input.Bindings.KeyBindingContainer`1", "pressedActions"];
        PassThrough_useParentInput = this["osu.Framework.Input.PassThroughInputManager", "useParentInput"];
    }

    public static OffsetTable Load(string gameDirectory)
    {
        if (!File.Exists(Path.Combine(gameDirectory, "osu.Game.dll")))
            throw new InvalidOperationException($"osu.Game.dll not found in {gameDirectory}");

        SelfCheck();

        var alc = new AssemblyLoadContext("lazer-fields", isCollectible: true);
        alc.Resolving += (_, name) =>
        {
            if (name.Name is null)
                return null;
            string path = Path.Combine(gameDirectory, name.Name + ".dll");
            return File.Exists(path) ? alc.LoadFromAssemblyPath(path) : null;
        };

        try
        {
            var assemblies = new List<Assembly>
            {
                alc.LoadFromAssemblyPath(Path.Combine(gameDirectory, "osu.Game.dll")),
                alc.LoadFromAssemblyPath(Path.Combine(gameDirectory, "osu.Framework.dll")),
                alc.LoadFromAssemblyPath(Path.Combine(gameDirectory, "osu.Game.Rulesets.Mania.dll")),
            };

            string desktop = Path.Combine(gameDirectory, "osu!.dll");
            if (File.Exists(desktop))
            {
                try { assemblies.Add(alc.LoadFromAssemblyPath(desktop)); }
                catch { /* entry assembly isn't required for field dumps */ }
            }

            Type Require(string name) =>
                FindType(assemblies, name) ?? throw new InvalidOperationException($"Type not found: {name}");

            var types = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            foreach (string name in RequiredTypes)
                types[name] = DumpType(Require(name));
            foreach (string name in OptionalTypes)
            {
                var t = FindType(assemblies, name);
                if (t != null)
                    types[name] = DumpType(t);
            }

            var hitObject = Require("osu.Game.Rulesets.Objects.HitObject");
            types["osu.Game.Beatmaps.Beatmap`1"] =
                DumpType(Require("osu.Game.Beatmaps.Beatmap`1").MakeGenericType(hitObject));
            types["osu.Game.Rulesets.UI.DrawableRuleset`1"] =
                DumpType(Require("osu.Game.Rulesets.UI.DrawableRuleset`1").MakeGenericType(hitObject));

            var propOpen = Require("osu.Game.Rulesets.Objects.HitObjectProperty`1");
            types["osu.Game.Rulesets.Objects.HitObjectProperty`1"] =
                DumpType(propOpen.MakeGenericType(typeof(int)), valueType: true);

            var bindableOpen = Require("osu.Framework.Bindables.Bindable`1");
            types["osu.Framework.Bindables.Bindable`1[Double]"] =
                DumpType(bindableOpen.MakeGenericType(typeof(double)));
            types["osu.Framework.Bindables.Bindable`1[Int32]"] =
                DumpType(bindableOpen.MakeGenericType(typeof(int)));

            types["System.Threading.Tasks.Task`1"] = DumpType(typeof(Task<object>));

            types["osu.Framework.Input.PassThroughInputManager"] =
                DumpType(Require("osu.Framework.Input.PassThroughInputManager"));

            var maniaAction = Require("osu.Game.Rulesets.Mania.ManiaAction");
            types["osu.Game.Rulesets.UI.RulesetInputManager`1"] =
                DumpType(Require("osu.Game.Rulesets.UI.RulesetInputManager`1").MakeGenericType(maniaAction));
            types["osu.Framework.Input.Bindings.KeyBindingContainer`1"] =
                DumpType(Require("osu.Framework.Input.Bindings.KeyBindingContainer`1").MakeGenericType(maniaAction));

            return new OffsetTable(gameDirectory, ReadOsuVersion(gameDirectory), types);
        }
        finally
        {
            alc.Unload();
        }
    }

    public int this[string type, string field]
    {
        get
        {
            if (TryGet(type, field, out int offset))
                return offset;
            string listed = _types.TryGetValue(type, out var fields)
                ? string.Join(", ", fields.Keys)
                : "(type not dumped)";
            throw new KeyNotFoundException($"No offset '{type}.{field}'. Have: {listed}");
        }
    }

    public bool TryGet(string type, string field, out int offset)
    {
        offset = 0;
        if (!_types.TryGetValue(type, out var fields))
            return false;
        if (fields.TryGetValue(field, out offset))
            return true;

        const string backing = ">k__BackingField";
        if (field.StartsWith("<", StringComparison.Ordinal) &&
            field.EndsWith(backing, StringComparison.Ordinal) &&
            field.Length > backing.Length + 1 &&
            fields.TryGetValue(field[1..^backing.Length], out offset))
            return true;

        return fields.TryGetValue($"<{field}>k__BackingField", out offset);
    }

    public IEnumerable<string> Types() => _types.Keys.OrderBy(n => n, StringComparer.Ordinal);

    public IEnumerable<(string Name, int Offset)> Fields(string type) =>
        _types.TryGetValue(type, out var fields)
            ? fields.Select(kv => (kv.Key, kv.Value))
            : [];

    public IEnumerable<(string Name, int Offset)> Dump()
    {
        yield return ("WorkingBeatmap.BeatmapInfo", Working_BeatmapInfo);
        yield return ("WorkingBeatmap.beatmapLoadTask", Working_beatmapLoadTask);
        yield return ("Task<T>.m_result", Task_m_result);
        yield return ("Beatmap<T>.HitObjects", Beatmap_HitObjects);
        yield return ("Beatmap<T>.beatmapInfo", Beatmap_beatmapInfo);
        yield return ("HitObject.StartTimeBindable", HitObject_StartTimeBindable);
        yield return ("ManiaHitObject.column", ManiaHitObject_column);
        yield return ("HoldNote.duration", HoldNote_duration);
        yield return ("HoldNote.Head", HoldNote_Head);
        yield return ("HoldNote.Tail", HoldNote_Tail);
        yield return ("Player.GameplayState", Player_GameplayState);
        yield return ("Player.DrawableRuleset", Player_DrawableRuleset);
        yield return ("GameplayState.Beatmap", GameplayState_Beatmap);
        yield return ("DrawableRuleset<T>.Beatmap", DrawableRuleset_Beatmap);
        yield return ("Bindable<double>.value", BindableDouble_value);
        yield return ("Bindable<int>.value", BindableInt_value);
        yield return ("ManiaBeatmap.Stages", ManiaBeatmap_Stages);
        yield return ("StageDefinition.Columns", StageDefinition_Columns);
        yield return ("HitObjectProperty<int>.backingBindable", HitObjectProperty_backingBindable);
        yield return ("HitObjectProperty<int>.backingValue", HitObjectProperty_backingValue);
        yield return ("DrawableRuleset.KeyBindingInputManager", DrawableRuleset_KeyBindingInputManager);
        yield return ("RulesetInputManager.KeyBindingContainer", RulesetInputManager_KeyBindingContainer);
        yield return ("KeyBindingContainer.pressedActions", KeyBindingContainer_pressedActions);
        yield return ("PassThroughInputManager.useParentInput", PassThrough_useParentInput);
    }

    static string ReadOsuVersion(string gameDirectory)
    {
        string deps = Path.Combine(gameDirectory, "osu!.deps.json");
        if (File.Exists(deps))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(deps));
            if (doc.RootElement.TryGetProperty("libraries", out var libraries))
            {
                foreach (var lib in libraries.EnumerateObject())
                {
                    if (lib.Name.StartsWith("osu!/", StringComparison.Ordinal))
                        return lib.Name["osu!/".Length..];
                }
            }
        }

        string exe = Path.Combine(gameDirectory, "osu!.exe");
        if (File.Exists(exe))
        {
            string? product = FileVersionInfo.GetVersionInfo(exe).ProductVersion;
            if (!string.IsNullOrEmpty(product))
                return product;
        }

        return "?";
    }

    static Type? FindType(IEnumerable<Assembly> assemblies, string name)
    {
        foreach (var asm in assemblies)
        {
            var type = asm.GetType(name);
            if (type != null)
                return type;
        }

        return null;
    }

    static Dictionary<string, int> DumpType(Type type, bool valueType = false)
    {
        var fields = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var field in EnumerateInstanceFields(type))
        {
            if (field.IsStatic)
                continue;
            fields[field.Name] = valueType || field.DeclaringType?.IsValueType == true
                ? StructOffset(field)
                : InstanceOffset(field);
        }

        return fields;
    }

    static void SelfCheck()
    {
        int a = InstanceOffset(typeof(OffsetProbe).GetField("A", InstanceDeclared)!);
        int b = InstanceOffset(typeof(OffsetProbe).GetField("B", InstanceDeclared)!);
        if (a != IntPtr.Size || b != IntPtr.Size * 2)
            throw new InvalidOperationException($"FieldDesc self-check failed (A={a} B={b}, expected {IntPtr.Size} and {IntPtr.Size * 2}).");
    }

    static IEnumerable<FieldInfo> EnumerateInstanceFields(Type type)
    {
        for (Type? t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(InstanceDeclared))
                yield return f;
        }
    }

    static int StructOffset(FieldInfo field)
    {
        IntPtr desc = field.FieldHandle.Value;
        int packed = Marshal.ReadInt32(desc, IntPtr.Size + 4);
        int relative = packed & 0x07FF_FFFF;
        if (relative is < 0 or > 0x10000)
            throw new InvalidOperationException($"Implausible FieldDesc offset {relative} for {field.DeclaringType}.{field.Name}.");
        return relative;
    }

    /// <summary>
    /// FieldDesc.m_dwOffset is relative to the start of instance data (after the MethodTable pointer on classes).
    /// </summary>
    static int InstanceOffset(FieldInfo field)
    {
        if (field.IsStatic)
            throw new InvalidOperationException($"{field.DeclaringType}.{field.Name} is static.");
        return StructOffset(field) + IntPtr.Size;
    }

    sealed class OffsetProbe
    {
#pragma warning disable CS0649
        public object? A;
        public object? B;
#pragma warning restore CS0649
    }
}
