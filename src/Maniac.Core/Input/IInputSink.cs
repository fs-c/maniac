namespace Maniac.Core.Input;

/// <summary>
/// Game-bound action sink. Implementations must be allocation-free in the hot path.
/// Action indices are ruleset-specific (mania columns, osu mouse buttons).
/// </summary>
public interface IInputSink
{
    void Press(int action);
    void Release(int action);
    void MoveMouse(float x, float y);
    void ReleaseHeld();
}
