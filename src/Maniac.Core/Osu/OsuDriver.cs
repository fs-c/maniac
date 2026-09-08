using Maniac.Core.Geometry;
using Maniac.Core.Input;

namespace Maniac.Core.Osu;

public sealed class OsuDriver
{
    public const float SpinRadius = 50;
    public const float SpinRadPerMs = 0.05f;

    OsuPlayPlan? plan;
    int noteCursor;
    int buttonCursor;
    int applied;
    int lastingIndex = -1;
    double lastTime = double.NegativeInfinity;
    bool playing;
    readonly int[] holdCount = new int[2];
    readonly bool[] held = new bool[2];
    float lastX = Vec2.PlayfieldCentre.X;
    float lastY = Vec2.PlayfieldCentre.Y;

    public int Applied => applied;
    public int Total => plan?.Notes.Length ?? 0;

    public void Load(OsuPlayPlan playPlan)
    {
        plan = playPlan;
        noteCursor = 0;
        buttonCursor = 0;
        applied = 0;
        lastingIndex = -1;
        lastTime = double.NegativeInfinity;
        playing = false;
        Array.Clear(holdCount);
        Array.Clear(held);
        lastX = Vec2.PlayfieldCentre.X;
        lastY = Vec2.PlayfieldCentre.Y;
    }

    public void Unload(IInputSink sink)
    {
        ReleaseButtons(sink);
        plan = null;
        noteCursor = 0;
        buttonCursor = 0;
        applied = 0;
        lastingIndex = -1;
        lastTime = double.NegativeInfinity;
        playing = false;
    }

    public void Tick(double time, IInputSink sink, bool play)
    {
        var current = plan;
        if (current == null)
            return;

        bool seeked = time + 1 < lastTime;
        lastTime = time;
        if (!play)
        {
            playing = false;
            ReleaseButtons(sink);
            return;
        }

        if (!playing || seeked)
            Seek(current, time, sink);

        playing = true;
        AdvanceNotes(current, time);
        applied = noteCursor;
        Vec2 pos = PositionAt(current, time);
        lastX = pos.X;
        lastY = pos.Y;
        sink.MoveMouse(pos.X, pos.Y);
        AdvanceButtons(current, time, sink);
    }

    public void SnapTo(double time, IInputSink sink, bool play)
    {
        var current = plan;
        if (current == null)
            return;

        lastTime = time;
        playing = play;
        Seek(current, time, sink);
        AdvanceNotes(current, time);
        applied = noteCursor;
        if (!play)
            return;

        Vec2 pos = PositionAt(current, time);
        lastX = pos.X;
        lastY = pos.Y;
        sink.MoveMouse(pos.X, pos.Y);
        SyncButton(sink, 0);
        SyncButton(sink, 1);
    }

    void Seek(OsuPlayPlan current, double time, IInputSink sink)
    {
        ReleaseButtons(sink);
        noteCursor = 0;
        buttonCursor = 0;
        lastingIndex = -1;
        AdvanceNotes(current, time);
        while (buttonCursor < current.ButtonEvents.Length && current.ButtonEvents[buttonCursor].Time <= time)
        {
            ref readonly var e = ref current.ButtonEvents[buttonCursor];
            if (e.Down)
                holdCount[e.Button]++;
            else if (holdCount[e.Button] > 0)
                holdCount[e.Button]--;
            buttonCursor++;
        }
    }

    void AdvanceNotes(OsuPlayPlan current, double time)
    {
        var notes = current.Notes;
        while (noteCursor < notes.Length && notes[noteCursor].Start <= time)
        {
            if (notes[noteCursor].IsLasting)
                lastingIndex = noteCursor;
            noteCursor++;
        }

        if (lastingIndex >= 0 && time > notes[lastingIndex].End)
            lastingIndex = -1;
    }

    void AdvanceButtons(OsuPlayPlan current, double time, IInputSink sink)
    {
        var events = current.ButtonEvents;
        while (buttonCursor < events.Length && events[buttonCursor].Time <= time)
        {
            ref readonly var e = ref events[buttonCursor];
            if (e.Down)
                holdCount[e.Button]++;
            else if (holdCount[e.Button] > 0)
                holdCount[e.Button]--;
            buttonCursor++;
        }

        SyncButton(sink, 0);
        SyncButton(sink, 1);
    }

    void SyncButton(IInputSink sink, int button)
    {
        bool want = holdCount[button] > 0;
        if (held[button] == want)
            return;
        held[button] = want;
        if (want)
            sink.Press(button);
        else
            sink.Release(button);
    }

    void ReleaseButtons(IInputSink sink)
    {
        holdCount[0] = 0;
        holdCount[1] = 0;
        if (held[0])
        {
            held[0] = false;
            sink.Release(0);
        }

        if (held[1])
        {
            held[1] = false;
            sink.Release(1);
        }
    }

    Vec2 PositionAt(OsuPlayPlan current, double time)
    {
        var notes = current.Notes;
        if (lastingIndex >= 0)
        {
            ref readonly var lasting = ref notes[lastingIndex];
            if (time <= lasting.End)
                return Follow(current, in lasting, time);
        }

        int prev = noteCursor - 1;
        if (prev >= 0)
        {
            ref readonly var tapping = ref notes[prev];
            if (!tapping.IsLasting && time <= tapping.Start + OsuPlanBuilder.KeyUpDelay)
                return tapping.StartPos;
        }

        if (noteCursor >= notes.Length)
            return prev >= 0 ? notes[prev].EndPos : Vec2.PlayfieldCentre;

        ref readonly var next = ref notes[noteCursor];
        Vec2 from = prev >= 0 ? notes[prev].EndPos : Vec2.PlayfieldCentre;
        if (time <= next.MoveStart || next.Start <= next.MoveStart)
            return from;

        float t = (float)((time - next.MoveStart) / (next.Start - next.MoveStart));
        t = Math.Clamp(t, 0f, 1f);
        t = 1f - (1f - t) * (1f - t);
        return Vec2.Lerp(from, next.StartPos, t);
    }

    public static Vec2 Follow(OsuPlayPlan plan, in OsuNote note, double time)
    {
        if (note.Kind == OsuNoteKind.Spinner)
        {
            float angle = (float)((Math.Clamp(time, note.Start, note.End) - note.Start) * SpinRadPerMs);
            return note.StartPos + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * SpinRadius;
        }

        if (note.Kind == OsuNoteKind.Slider && note.PathLength > 1 && note.End > note.Start)
        {
            double progress = Math.Clamp((time - note.Start) / (note.End - note.Start), 0, 1);
            float f = (float)progress * (note.PathLength - 1);
            int i = (int)f;
            if (i >= note.PathLength - 1)
                return plan.PathPoints[note.PathStart + note.PathLength - 1];
            float t = f - i;
            return Vec2.Lerp(
                plan.PathPoints[note.PathStart + i],
                plan.PathPoints[note.PathStart + i + 1],
                t);
        }

        return note.StartPos;
    }
}
