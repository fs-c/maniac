using Maniac.Core.Input;

namespace Maniac.Core.Mania;

public sealed class ManiaDriver
{
    ManiaPlayPlan? plan;
    int cursor;
    int applied;
    double lastTime = double.NegativeInfinity;
    readonly bool[] held = new bool[ManiaPlanner.MaxColumn + 1];
    readonly bool[] wantHeld = new bool[ManiaPlanner.MaxColumn + 1];

    public int Applied => applied;
    public int Total => plan?.Events.Length ?? 0;
    public int Columns => plan?.Columns ?? 0;
    public int HitCount => plan?.HitCount ?? 0;

    public void Load(ManiaPlayPlan playPlan)
    {
        plan = playPlan;
        cursor = 0;
        applied = 0;
        lastTime = double.NegativeInfinity;
        Array.Clear(held);
        Array.Clear(wantHeld);
    }

    public void Unload(IInputSink sink)
    {
        ReleaseHeld(sink);
        plan = null;
        cursor = 0;
        applied = 0;
        lastTime = double.NegativeInfinity;
    }

    public void Tick(double time, IInputSink sink, bool play)
    {
        var events = plan?.Events;
        if (events == null)
            return;

        bool seeked = time + 1 < lastTime;
        if (seeked)
            SeekReplay(events, time, sink);

        lastTime = time;
        if (!play)
        {
            ReleaseHeld(sink);
            return;
        }

        if (seeked)
            RestoreHeld(sink);

        while (cursor < events.Length && events[cursor].Time <= time)
        {
            Apply(in events[cursor], sink);
            cursor++;
            applied++;
        }
    }

    void SeekReplay(ManiaEvent[] events, double time, IInputSink sink)
    {
        ReleaseHeld(sink);
        cursor = SeekCursor(events, time);
        applied = cursor;
        Array.Clear(wantHeld);
        for (int i = 0; i < cursor; i++)
            wantHeld[events[i].Column] = events[i].Down;
    }

    void RestoreHeld(IInputSink sink)
    {
        for (int c = 0; c < wantHeld.Length; c++)
        {
            if (!wantHeld[c] || held[c])
                continue;
            held[c] = true;
            sink.Press(c);
        }
    }

    public static int SeekCursor(ManiaEvent[] events, double time)
    {
        int lo = 0;
        int hi = events.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (events[mid].Time <= time)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    void Apply(in ManiaEvent e, IInputSink sink)
    {
        int column = e.Column;
        if (held[column] == e.Down)
            return;

        held[column] = e.Down;
        if (e.Down)
            sink.Press(column);
        else
            sink.Release(column);
    }

    void ReleaseHeld(IInputSink sink)
    {
        for (int i = 0; i < held.Length; i++)
        {
            if (!held[i])
                continue;
            held[i] = false;
            sink.Release(i);
        }
    }
}
