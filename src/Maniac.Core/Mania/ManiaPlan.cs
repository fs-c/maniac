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

public sealed class ManiaPlayPlan
{
    public readonly ManiaEvent[] Events;
    public readonly int Columns;
    public readonly int HitCount;

    public ManiaPlayPlan(ManiaEvent[] events, int columns, int hitCount)
    {
        Events = events;
        Columns = columns;
        HitCount = hitCount;
    }
}
