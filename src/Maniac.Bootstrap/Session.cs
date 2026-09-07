using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using osu.Framework.Platform;

namespace Maniac;

/// <summary>
/// Default-ALC trampoline. hostfxr cannot load into a collectible context, and
/// Harmony cannot emit a postfix that lives in one either (non-collectible
/// dynamic assemblies may not reference collectible types).
///
/// This type owns a single UpdateFrame postfix and a delegate into the plugin.
/// After unload the delegate is null; the postfix is a no-op.
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
    static Harmony? harmony;
    static int patched;
    static string sourceDir = "";
    static string? loadDir;
    static int pending;
    static int continueCmd;

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

    public static void Run(string command)
    {
        lock (Gate)
        {
            AttachOnce();
            bool unload = command.Equals("unload", StringComparison.OrdinalIgnoreCase);
            if (alc == null)
            {
                if (!unload)
                    StartPlugin();
                return;
            }

            if (unload)
            {
                Volatile.Write(ref pumping, 0);
                RequestUnload();
            }
            else
                RequestReload();
        }
    }

    static int pumping;

    static void AttachOnce()
    {
        if (Interlocked.Exchange(ref patched, 1) != 0)
            return;

        Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", "dynamicmethod");
        try
        {
            harmony = new Harmony("maniac-stub");
            var updateFrame = AccessTools.Method(typeof(GameHost), "UpdateFrame")
                ?? throw new MissingMethodException("GameHost.UpdateFrame");
            harmony.Patch(updateFrame, postfix: new HarmonyMethod(typeof(Session), nameof(OnUpdateFrame)));
            return;
        }
        catch (Exception ex)
        {
            Log(ex);
        }

        StartSchedulerPump();
    }

    static void StartSchedulerPump()
    {
        var host = FindHost();
        if (host?.UpdateThread?.Scheduler != null)
        {
            QueuePump(host);
            return;
        }

        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 200; i++)
            {
                await Task.Delay(50).ConfigureAwait(false);
                var found = FindHost();
                if (found?.UpdateThread?.Scheduler == null)
                    continue;
                QueuePump(found);
                return;
            }
        });
    }

    static void QueuePump(GameHost host)
    {
        if (Interlocked.Exchange(ref pumping, 1) != 0)
            return;
        host.UpdateThread.Scheduler.Add(() => Pump(host));
    }

    static void Pump(GameHost host)
    {
        try { tick?.Invoke(host); }
        catch (Exception ex) { Log(ex); }

        if (Volatile.Read(ref pumping) == 0)
            return;
        host.UpdateThread.Scheduler.Add(() => Pump(host));
    }

    static GameHost? FindHost()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string? name = assembly.GetName().Name;
            if (name is null || name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft", StringComparison.Ordinal))
                continue;

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types!; }

            foreach (var type in types)
            {
                if (type == null)
                    continue;
                foreach (var member in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object? value;
                    try { value = member.GetValue(null); }
                    catch { continue; }
                    if (value is GameHost host)
                        return host;
                }
            }
        }

        return null;
    }

    static void OnUpdateFrame(GameHost __instance)
    {
        try { tick?.Invoke(__instance); }
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

    static string? lastLog;
    static long lastLogTicks;

    public static void Log(Exception ex)
    {
        string text = ex.ToString();
        long now = DateTime.UtcNow.Ticks;
        if (text == lastLog && now - lastLogTicks < TimeSpan.FromSeconds(30).Ticks)
            return;
        lastLog = text;
        lastLogTicks = now;

        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "maniac-lazer");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "bootstrap.log"), $"{DateTime.Now:O} {ex}\n");
        }
        catch { /* nowhere to write */ }
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
