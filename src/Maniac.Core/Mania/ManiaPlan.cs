namespace Maniac.Core.Mania;

public readonly struct ManiaHit
{
    public readonly double Start;
    public readonly double End;
    public readonly int Column;
    public readonly bool IsHold;
    public readonly bool IsInstantHold;

    public ManiaHit(double start, double end, int column, bool isHold, bool isInstantHold)
    {
        Start = start;
        End = end;
        Column = column;
        IsHold = isHold;
        IsInstantHold = isInstantHold;
    }
}

public readonly struct ManiaEvent
{
    public readonly double Time;
    public readonly byte Column;
    public readonly bool Down;

    public ManiaEvent(double time, byte column, bool down)
    {
        Time = time;
        Column = column;
        Down = down;
    }
}

public readonly struct ManiaPressRelease
{
    public readonly double Press;
    public readonly double Release;
    public readonly int Column;

    public ManiaPressRelease(double press, double release, int column)
    {
        Press = press;
        Release = release;
        Column = column;
    }
}

public sealed class ManiaPlayPlan
{
    public readonly ManiaHit[] Hits;
    public readonly ManiaEvent[] Events;
    public readonly int Columns;
    public readonly int HitCount;

    public ManiaPlayPlan(ManiaEvent[] events, int columns, int hitCount, ManiaHit[]? hits = null)
    {
        Events = events;
        Columns = columns;
        HitCount = hitCount;
        Hits = hits ?? [];
    }
}
