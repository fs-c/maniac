namespace StableHost;

/// <summary>
/// Names recovered from a live osu!stable 32-bit CLR heap (Confuser names, this client build).
/// Missing type/field is a hard failure — do not guess a replacement.
/// </summary>
static class StableLayout
{
    public const string HitObjectType = "#=zu87$7AJCGiKbHMhV6JOStjSGnRsYcvmuog==";
    public const string ManiaNoteType = "#=z1zg8JBfE4I8aYDjrdBvaT3ot4Vlgfodn6sGnL_0FJVXqg7GrYKE8TOs=";
    public const string ManiaHoldType = "#=zj_xBP_TgnWyeWEkkrSYRAxWvtdtVSlH$r6OaWgMSqL9TiTPNRI5tCTzr0QWN";
    public const string AudioEngineType = "#=zySBjyD2zKcTAsOEEDA==";
    public const string AudioTimeField = "#=z1ownPGA=";
    public const string GameType = "#=zLQ1lSsp1Sl649eje$Q==";
    // Three osu.OsuModes statics exist. During play this machine had [10, 2, 5]; 2 is Play.
    public const string OsuModePlayField = "#=zFX09SEA=";
    public static readonly string[] OsuModeFields =
    [
        "#=zAArBrWv3SBHh",
        OsuModePlayField,
        "#=zzZ4j3LNHmelQ"
    ];

    public const string BindingManagerType = "#=zQk4E9Jbj43L21lMT9Q==";
    public const string BindingMapField = "#=zOgzVY2mh5Lq5";
    public const string BindingNamesField = "#=zbXf81NJjRflp";
    public const string DefaultManiaKeysField = "#=zRK_7iO0vrufLoZ3$wg==";
    public const string ManiaLayoutType = "#=zHOKWtdmNDHwUQhZEpw48M40=";
    public const string ManiaLayoutKeysField = "#=zFQkLGWI=";
    public const string ManiaLayoutColumnsField = "#=z8$ftRkA=";
    public const string ManiaLayoutKindField = "#=zzlzUDg8=";
    public const string ManiaColumnType = "#=z5N916H9H27tUdrBKpd$IfnxddH1oxeeHlFOSXMIGXMkrNWB6dg==";
    public const int ManiaColumnIndexOffset = 0x1C;
    public const int ManiaColumnXOffset = 0x20;
    public const int ManiaColumnKeyOffset = 0x2C;

    public const int StartTimeOffset = 0x0C;
    public const int EndTimeOffset = 0x10;
    public const int PositionOffset = 0x34;
    public const int ColumnOffset = 0x98;

    // osu.OsuModes.Play — recovered historically; watch prints the raw value so a mismatch is obvious.
    public const int OsuModePlay = 2;
    public const int PlayModeMania = 3;
}
