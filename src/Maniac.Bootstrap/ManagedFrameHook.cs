using System.Reflection;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;

namespace Maniac;

/// <summary>
/// Startup-hook / Harmony-fallback tick: find the live GameHost without a static,
/// then wrap UpdateThread.OnNewFrame (or re-queue on its scheduler).
/// </summary>
static class ManagedFrameHook
{
    static Action<GameHost>? onFrame;
    static int pumping;

    public static void Attach(Action<GameHost> callback)
    {
        onFrame = callback;
        var host = FindHost();
        if (host != null)
        {
            AttachPump(host);
            return;
        }

        Session.Log("host not ready, polling");
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 200; i++)
            {
                await Task.Delay(50).ConfigureAwait(false);
                var found = FindHost();
                if (found == null)
                    continue;
                AttachPump(found);
                return;
            }

            Session.Log("gave up waiting for GameHost");
        });
    }

    public static void Stop() => Volatile.Write(ref pumping, 0);

    static void AttachPump(GameHost host)
    {
        if (TryWrapNewFrame(host))
        {
            Session.Log("wrapped UpdateThread.OnNewFrame");
            return;
        }

        if (host.UpdateThread?.Scheduler == null)
        {
            Session.Log("no scheduler to pump");
            return;
        }

        Session.Log("queueing scheduler pump");
        QueuePump(host);
    }

    static bool TryWrapNewFrame(GameHost host)
    {
        var thread = host.UpdateThread;
        if (thread == null)
            return false;

        var field = typeof(GameThread).GetField("OnNewFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || !typeof(Delegate).IsAssignableFrom(field.FieldType))
            return false;

        try
        {
            var original = field.GetValue(thread) as Action;
            Action wrapped = () =>
            {
                original?.Invoke();
                Invoke(host);
            };
            field.SetValue(thread, wrapped);
            return true;
        }
        catch (Exception ex)
        {
            Session.Log(ex);
            return false;
        }
    }

    static void QueuePump(GameHost host)
    {
        if (Interlocked.Exchange(ref pumping, 1) != 0)
            return;
        host.UpdateThread.Scheduler.Add(() => Pump(host));
    }

    static void Pump(GameHost host)
    {
        Invoke(host);
        if (Volatile.Read(ref pumping) == 0)
            return;
        host.UpdateThread.Scheduler.Add(() => Pump(host));
    }

    static void Invoke(GameHost host) => onFrame?.Invoke(host);

    static GameHost? FindHost()
    {
        try
        {
            var host = FindHostIn(Logger.Storage, 0);
            if (host != null)
                Session.Log($"found host {host.GetType().Name}");
            return host;
        }
        catch (Exception ex)
        {
            Session.Log(ex);
            return null;
        }
    }

    static GameHost? FindHostIn(object? obj, int depth)
    {
        if (obj == null || depth > 8)
            return null;
        if (obj is GameHost host)
            return host;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = obj.GetType();

        foreach (var field in type.GetFields(flags))
        {
            if (!typeof(GameHost).IsAssignableFrom(field.FieldType))
                continue;
            try
            {
                if (field.GetValue(obj) is GameHost found)
                    return found;
            }
            catch { /* skip */ }
        }

        foreach (var field in type.GetFields(flags))
        {
            if (!typeof(Storage).IsAssignableFrom(field.FieldType))
                continue;
            object? next;
            try { next = field.GetValue(obj); }
            catch { continue; }
            var nested = FindHostIn(next, depth + 1);
            if (nested != null)
                return nested;
        }

        foreach (var prop in type.GetProperties(flags))
        {
            if (prop.GetIndexParameters().Length != 0)
                continue;
            if (!typeof(Storage).IsAssignableFrom(prop.PropertyType)
                && !typeof(GameHost).IsAssignableFrom(prop.PropertyType))
                continue;
            object? next;
            try { next = prop.GetValue(obj); }
            catch { continue; }
            var nested = FindHostIn(next, depth + 1);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
