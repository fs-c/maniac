namespace Maniac.Core.Input;

public sealed class NullInputSink : IInputSink
{
    public static readonly NullInputSink Instance = new();

    public void Press(int action) { }
    public void Release(int action) { }
    public void MoveMouse(float x, float y) { }
    public void ReleaseHeld() { }
}
