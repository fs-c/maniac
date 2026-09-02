using System.Globalization;
using System.Reflection;

namespace Maniac.Core;

public sealed class BuildExpiry
{
    public const string MetadataKey = "ManiacBuildExpiry";

    public static BuildExpiry Current { get; } = FromAssembly(typeof(BuildExpiry).Assembly);

    public DateTimeOffset? EndTimeUtc { get; }
    public bool StampInvalid { get; }

    BuildExpiry(DateTimeOffset? endTimeUtc, bool stampInvalid)
    {
        EndTimeUtc = endTimeUtc;
        StampInvalid = stampInvalid;
    }

    public bool HasEndTime => EndTimeUtc.HasValue;

    public bool IsExpired(DateTimeOffset utcNow) =>
        StampInvalid || EndTimeUtc is { } end && utcNow >= end;

    public bool IsExpired() => IsExpired(DateTimeOffset.UtcNow);

    public string Describe(DateTimeOffset utcNow)
    {
        if (StampInvalid)
            return "This build has an invalid expiry stamp and will not run.";
        if (EndTimeUtc is not { } end)
            return "";
        string when = end.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        return utcNow >= end
            ? $"This build expired on {when}."
            : $"This build ends {when}.";
    }

    public static BuildExpiry FromAssembly(Assembly assembly)
    {
        string? raw = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == MetadataKey)?.Value;
        return Parse(raw);
    }

    public static BuildExpiry Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new(null, false);

        raw = raw.Trim();
        return TryParseStamp(raw, out var end)
            ? new(end, false)
            : new(null, true);
    }

    static bool TryParseStamp(string raw, out DateTimeOffset end)
    {
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        string[] formats =
        [
            "yyyy-MM-dd",
            "yyyy-MM-dd'T'HH:mmK",
            "yyyy-MM-dd'T'HH:mm:ssK",
        ];
        return DateTimeOffset.TryParseExact(raw, formats, CultureInfo.InvariantCulture, styles, out end);
    }
}
