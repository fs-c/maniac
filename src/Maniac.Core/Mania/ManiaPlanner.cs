namespace Maniac.Core.Mania;

public static class ManiaPlanner
{
    public const double ReleaseDelay = 20;
    public const int MaxColumn = 19;

    public static ManiaPlayPlan Build(ReadOnlySpan<ManiaHit> hits)
    {
        var stored = hits.ToArray();
        int n = stored.Length;
        if (n == 0)
            return new ManiaPlayPlan([], 0, 0, stored);

        int[] next = new int[n];
        int[] lastByColumn = new int[MaxColumn + 1];
        Array.Fill(lastByColumn, -1);
        int columns = 0;
        int used = 0;

        for (int i = 0; i < n; i++)
        {
            int column = stored[i].Column;
            if ((uint)column > MaxColumn)
            {
                next[i] = -1;
                continue;
            }

            used++;
            if (column + 1 > columns)
                columns = column + 1;
            int prev = lastByColumn[column];
            if (prev >= 0)
                next[prev] = i;
            lastByColumn[column] = i;
            next[i] = -1;
        }

        var events = new ManiaEvent[used * 2];
        int w = 0;
        for (int i = 0; i < n; i++)
        {
            int column = stored[i].Column;
            if ((uint)column > MaxColumn)
                continue;

            double release = ReleaseTime(in stored[i], next[i] >= 0 ? stored[next[i]] : null);
            events[w++] = new ManiaEvent(stored[i].Start, (byte)column, true);
            events[w++] = new ManiaEvent(release, (byte)column, false);
        }

        events = SortAndTrim(events, w);
        return new ManiaPlayPlan(events, columns, used, stored);
    }

    public static ManiaPlayPlan FromPressReleases(ManiaHit[] sourceHits, ReadOnlySpan<ManiaPressRelease> notes, int columns = 0)
    {
        int n = notes.Length;
        if (n == 0)
            return new ManiaPlayPlan([], columns, 0, sourceHits);

        var events = new ManiaEvent[n * 2];
        int w = 0;
        int used = 0;
        for (int i = 0; i < n; i++)
        {
            int column = notes[i].Column;
            if ((uint)column > MaxColumn)
                continue;

            used++;
            if (column + 1 > columns)
                columns = column + 1;
            double press = notes[i].Press;
            double release = notes[i].Release;
            if (release <= press)
                release = press + 1;
            events[w++] = new ManiaEvent(press, (byte)column, true);
            events[w++] = new ManiaEvent(release, (byte)column, false);
        }

        events = SortAndTrim(events, w);
        return new ManiaPlayPlan(events, columns, used, sourceHits);
    }

    static ManiaEvent[] SortAndTrim(ManiaEvent[] events, int w)
    {
        Array.Sort(events, 0, w, EventComparer.Instance);
        if (w != events.Length)
            Array.Resize(ref events, w);
        return events;
    }

    public static double ReleaseTime(in ManiaHit current, ManiaHit? next)
    {
        if (current.IsHold)
            return current.End;

        double delay = current.IsInstantHold ? 1 : ReleaseDelay;
        if (next is not { } following || following.Start > current.End + delay)
            return current.End + delay;
        return current.End + (following.Start - current.End) * 0.9;
    }

    sealed class EventComparer : IComparer<ManiaEvent>
    {
        public static readonly EventComparer Instance = new();

        public int Compare(ManiaEvent a, ManiaEvent b)
        {
            int time = a.Time.CompareTo(b.Time);
            if (time != 0)
                return time;
            return a.Down.CompareTo(b.Down) * -1;
        }
    }
}
