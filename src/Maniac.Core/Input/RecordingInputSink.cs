namespace Maniac.Core.Input;

public sealed class RecordingInputSink : IInputSink
{
    public readonly List<RecordedAction> Actions = [];
    public (float X, float Y) LastMouse;
    public int MouseMoves;
    readonly bool[] held = new bool[32];

    public void Press(int action)
    {
        if ((uint)action < (uint)held.Length)
            held[action] = true;
        Actions.Add(new RecordedAction(action, Down: true));
    }

    public void Release(int action)
    {
        if ((uint)action < (uint)held.Length)
            held[action] = false;
        Actions.Add(new RecordedAction(action, Down: false));
    }

    public void MoveMouse(float x, float y)
    {
        LastMouse = (x, y);
        MouseMoves++;
    }

    public void ReleaseHeld()
    {
        for (int i = 0; i < held.Length; i++)
        {
            if (!held[i])
                continue;
            held[i] = false;
            Actions.Add(new RecordedAction(i, Down: false));
        }
    }

    public readonly record struct RecordedAction(int Action, bool Down);
}
