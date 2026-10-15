namespace Maniac.Stable;

public readonly record struct StablePlayStatus(
    bool Attached,
    bool LayoutOk,
    int Pid,
    bool Enabled,
    bool Playing,
    bool Paused,
    bool Foreground,
    string Ruleset,
    string Screen,
    double Time,
    int Applied,
    int Total,
    int Columns,
    string Keys,
    string Diagnostic);
