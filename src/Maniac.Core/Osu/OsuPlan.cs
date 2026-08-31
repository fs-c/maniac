using Maniac.Core.Geometry;

namespace Maniac.Core.Osu;

public enum OsuNoteKind : byte
{
    Circle,
    Slider,
    Spinner,
}

public readonly struct OsuNote
{
    public readonly double Start;
    public readonly double End;
    public readonly double Preempt;
    public readonly double MoveStart;
    public readonly Vec2 StartPos;
    public readonly Vec2 EndPos;
    public readonly OsuNoteKind Kind;
    public readonly byte Button;
    public readonly int PathStart;
    public readonly int PathLength;

    public OsuNote(
        double start,
        double end,
        double preempt,
        double moveStart,
        Vec2 startPos,
        Vec2 endPos,
        OsuNoteKind kind,
        byte button,
        int pathStart,
        int pathLength)
    {
        Start = start;
        End = end;
        Preempt = preempt;
        MoveStart = moveStart;
        StartPos = startPos;
        EndPos = endPos;
        Kind = kind;
        Button = button;
        PathStart = pathStart;
        PathLength = pathLength;
    }

    public bool IsLasting => Kind != OsuNoteKind.Circle;
}

public readonly struct OsuButtonEvent
{
    public readonly double Time;
    public readonly byte Button;
    public readonly bool Down;

    public OsuButtonEvent(double time, byte button, bool down)
    {
        Time = time;
        Button = button;
        Down = down;
    }
}

public sealed class OsuPlayPlan
{
    public readonly OsuNote[] Notes;
    public readonly Vec2[] PathPoints;
    public readonly OsuButtonEvent[] ButtonEvents;

    public OsuPlayPlan(OsuNote[] notes, Vec2[] pathPoints, OsuButtonEvent[] buttonEvents)
    {
        Notes = notes;
        PathPoints = pathPoints;
        ButtonEvents = buttonEvents;
    }
}
