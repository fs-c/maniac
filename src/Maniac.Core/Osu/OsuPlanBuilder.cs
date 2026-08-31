using Maniac.Core.Geometry;

namespace Maniac.Core.Osu;

public sealed class OsuPlanBuilder
{
    public const double KeyUpDelay = 50;
    public const double AlternateGap = 266;
    public const double Reaction = 100;

    readonly List<Draft> drafts = [];
    readonly List<Vec2> path = [];

    public void AddCircle(double start, Vec2 position, double preempt) =>
        Add(OsuNoteKind.Circle, start, start, position, preempt, ReadOnlySpan<Vec2>.Empty);

    public void AddSlider(double start, double end, Vec2 startPos, double preempt, ReadOnlySpan<Vec2> pathPoints) =>
        Add(OsuNoteKind.Slider, start, end, startPos, preempt, pathPoints);

    public void AddSpinner(double start, double end, Vec2 centre, double preempt) =>
        Add(OsuNoteKind.Spinner, start, end, centre, preempt, ReadOnlySpan<Vec2>.Empty);

    void Add(OsuNoteKind kind, double start, double end, Vec2 startPos, double preempt, ReadOnlySpan<Vec2> pathPoints)
    {
        int pathStart = path.Count;
        for (int i = 0; i < pathPoints.Length; i++)
            path.Add(pathPoints[i]);
        drafts.Add(new Draft(start, end, preempt, startPos, kind, pathStart, pathPoints.Length));
    }

    public OsuPlayPlan Build()
    {
        drafts.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        var notes = new OsuNote[drafts.Count];
        var buttons = new OsuButtonEvent[drafts.Count * 2];
        int button = 0;
        double prevStart = double.NegativeInfinity;
        var pathPoints = path.ToArray();

        for (int i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            if (d.Start - prevStart >= 0 && d.Start - prevStart < AlternateGap)
                button = 1 - button;
            else
                button = 0;

            Vec2 endPos = d.Kind == OsuNoteKind.Slider && d.PathLength > 0
                ? pathPoints[d.PathStart + d.PathLength - 1]
                : d.StartPos;
            double moveStart = d.Start - Math.Max(0, d.Preempt - Reaction);
            notes[i] = new OsuNote(
                d.Start,
                d.End,
                d.Preempt,
                moveStart,
                d.StartPos,
                endPos,
                d.Kind,
                (byte)button,
                d.PathStart,
                d.PathLength);
            buttons[i * 2] = new OsuButtonEvent(d.Start, (byte)button, true);
            buttons[i * 2 + 1] = new OsuButtonEvent(d.End + KeyUpDelay, (byte)button, false);
            prevStart = d.Start;
        }

        Array.Sort(buttons, ButtonComparer.Instance);
        return new OsuPlayPlan(notes, pathPoints, buttons);
    }

    readonly record struct Draft(
        double Start,
        double End,
        double Preempt,
        Vec2 StartPos,
        OsuNoteKind Kind,
        int PathStart,
        int PathLength);

    sealed class ButtonComparer : IComparer<OsuButtonEvent>
    {
        public static readonly ButtonComparer Instance = new();

        public int Compare(OsuButtonEvent a, OsuButtonEvent b)
        {
            int time = a.Time.CompareTo(b.Time);
            if (time != 0)
                return time;
            return a.Down.CompareTo(b.Down) * -1;
        }
    }
}
