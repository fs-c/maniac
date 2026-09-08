using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using osu.Framework.Platform;

namespace Maniac;

/// <summary>
/// Default-ALC trampoline. hostfxr cannot load into a collectible context, and
/// Harmony cannot emit a postfix that lives in one either (non-collectible
/// dynamic assemblies may not reference collectible types).
///
/// This type owns plugin load/unload and a delegate into the plugin. After
/// unload the delegate is null; the frame hook is a no-op.
/// </summary>
public static class Session
{
    public const int Reload = 1;
    public const int Unload = 2;

    static readonly object Gate = new();
    static AssemblyLoadContext? alc;
    static WeakReference? alcWeak;
    static MethodInfo? start;
    static Action<GameHost>? tick;
    static string sourceDir = "";
    static string? loadDir;
    static int pending;
    static int continueCmd;
    static int attached;

    public static void SetSourceDir(string dir)
    {
        if (!string.IsNullOrWhiteSpace(dir))
            sourceDir = dir;
    }

    public static void SetTick(Action<GameHost>? value) => tick = value;

    public static void RequestReload() => Volatile.Write(ref pending, Reload);
    public static void RequestUnload() => Volatile.Write(ref pending, Unload);
    public static int ConsumePending() => Interlocked.Exchange(ref pending, 0);
    public static void PrepareContinue(int cmd) => Volatile.Write(ref continueCmd, cmd);

    public static void ContinueAfterStop()
    {
        lock (Gate)
        {
            int cmd = Interlocked.Exchange(ref continueCmd, 0);
            DropPlugin();
            if (cmd == Reload)
                StartPlugin();
        }
    }

    internal static void Run(string command, InjectMethod method)
    {
        lock (Gate)
        {
            AttachOnce(method);
            bool unload = command.Equals("unload", StringComparison.OrdinalIgnoreCase);
            if (unload)
            {
                FrameHook.Stop();
                RequestUnload();
                return;
            }

            if (alc == null)
                StartPlugin();
            else
                RequestReload();
        }
    }

    static void AttachOnce(InjectMethod method)
    {
        if (Interlocked.Exchange(ref attached, 1) != 0)
            return;
        FrameHook.Attach(OnUpdateFrame, method);
    }

    static void OnUpdateFrame(GameHost host)
    {
        try { tick?.Invoke(host); }
        catch (Exception ex) { Log(ex); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void StartPlugin()
    {
        LoadContext();
        start?.Invoke(null, null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void DropPlugin()
    {
        tick = null;
        start = null;

        var dying = alc;
        alc = null;
        dying?.Unload();

        for (int i = 0; i < 3 && alcWeak is { IsAlive: true }; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        alcWeak = null;
        if (loadDir == null)
            return;
        try { Directory.Delete(loadDir, recursive: true); }
        catch { /* still mapped */ }
        loadDir = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void LoadContext()
    {
        if (string.IsNullOrEmpty(sourceDir))
            sourceDir = Path.GetDirectoryName(typeof(Session).Assembly.Location) ?? "";

        string dll = Path.Combine(sourceDir, "Maniac.Plugin.dll");
        if (!File.Exists(dll))
            throw new FileNotFoundException("Maniac.Plugin.dll not found. Rebuild the plugin.", dll);

        loadDir = Path.Combine(Path.GetTempPath(), "maniac-lazer", DateTime.UtcNow.Ticks.ToString());
        Directory.CreateDirectory(loadDir);

        foreach (string pattern in new[] { "Maniac.Plugin.*", "Maniac.Core.*" })
        {
            foreach (string file in Directory.GetFiles(sourceDir, pattern))
                File.Copy(file, Path.Combine(loadDir, Path.GetFileName(file)), overwrite: true);
        }

        alc = new PluginLoadContext(loadDir);
        alcWeak = new WeakReference(alc, trackResurrection: true);
        var asm = alc.LoadFromAssemblyPath(Path.Combine(loadDir, "Maniac.Plugin.dll"));
        var type = asm.GetType("Maniac.Plugin.Entry")
            ?? throw new InvalidOperationException("Maniac.Plugin.Entry not found");
        start = type.GetMethod("Start", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException("Maniac.Plugin.Entry.Start");
    }

    public static void Log(string message)
    {
        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "bootstrap.log"), $"{DateTime.Now:O} {message}\n");
        }
        catch { /* nowhere to write */ }
    }

    public static void Log(Exception ex)
    {
        Log(ex.ToString());
    }

    sealed class PluginLoadContext : AssemblyLoadContext
    {
        readonly string dir;

        public PluginLoadContext(string dir) : base("maniac-plugin", isCollectible: true)
        {
            this.dir = dir;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is null or "Maniac" or "osu.Framework" or "osu.Game" or "osuTK" or "Realm" or "0Harmony")
                return null;

            string path = Path.Combine(dir, assemblyName.Name + ".dll");
            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }
    }
}
