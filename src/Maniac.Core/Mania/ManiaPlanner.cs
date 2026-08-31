namespace Maniac.Core.Mania;

public static class ManiaPlanner
{
    public const double ReleaseDelay = 20;
    public const int MaxColumn = 19;

    public static ManiaPlayPlan Build(ReadOnlySpan<ManiaHit> hits)
    {
        int n = hits.Length;
        if (n == 0)
            return new ManiaPlayPlan([], 0, 0);

        int[] next = new int[n];
        int[] lastByColumn = new int[MaxColumn + 1];
        Array.Fill(lastByColumn, -1);
        int columns = 0;
        int used = 0;

        for (int i = 0; i < n; i++)
        {
            int column = hits[i].Column;
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
            int column = hits[i].Column;
            if ((uint)column > MaxColumn)
                continue;

            double release = ReleaseTime(in hits[i], next[i] >= 0 ? hits[next[i]] : null);
            events[w++] = new ManiaEvent(hits[i].Start, (byte)column, true);
            events[w++] = new ManiaEvent(release, (byte)column, false);
        }

        Array.Sort(events, 0, w, EventComparer.Instance);
        if (w != events.Length)
            Array.Resize(ref events, w);

        return new ManiaPlayPlan(events, columns, used);
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
