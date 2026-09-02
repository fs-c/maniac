using System.Reflection;
using Maniac.Core;
using Xunit;

[assembly: AssemblyMetadata(BuildExpiry.MetadataKey, "2026-10-15T00:00:00Z")]

namespace Maniac.Core.Tests;

public class BuildExpiryTests
{
    static readonly DateTimeOffset Noon = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_stamp_never_expires(string? raw)
    {
        var expiry = BuildExpiry.Parse(raw);
        Assert.False(expiry.HasEndTime);
        Assert.False(expiry.StampInvalid);
        Assert.False(expiry.IsExpired(DateTimeOffset.MaxValue));
        Assert.Equal("", expiry.Describe(Noon));
    }

    [Fact]
    public void Date_only_is_midnight_utc()
    {
        var expiry = BuildExpiry.Parse("2026-10-15");
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), expiry.EndTimeUtc);
        Assert.False(expiry.IsExpired(new DateTimeOffset(2026, 10, 14, 23, 59, 59, TimeSpan.Zero)));
        Assert.True(expiry.IsExpired(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Timestamp_with_offset_is_converted_to_utc()
    {
        var expiry = BuildExpiry.Parse("2026-10-15T02:00:00+02:00");
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), expiry.EndTimeUtc);
    }

    [Fact]
    public void Zulu_timestamp_is_accepted()
    {
        var expiry = BuildExpiry.Parse("2026-10-15T00:00:00Z");
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), expiry.EndTimeUtc);
        Assert.Equal("This build expired on 2026-10-15 00:00 UTC.", expiry.Describe(Noon));
        Assert.Equal("This build ends 2026-10-15 00:00 UTC.", expiry.Describe(Noon.AddDays(-1)));
    }

    [Fact]
    public void Invalid_stamp_is_expired()
    {
        var expiry = BuildExpiry.Parse("tomorrow");
        Assert.True(expiry.StampInvalid);
        Assert.True(expiry.IsExpired(DateTimeOffset.MinValue));
        Assert.Equal("This build has an invalid expiry stamp and will not run.", expiry.Describe(Noon));
    }

    [Fact]
    public void Current_assembly_stamp_is_empty_or_valid()
    {
        Assert.False(BuildExpiry.Current.StampInvalid);
    }

    [Fact]
    public void FromAssembly_reads_metadata()
    {
        var expiry = BuildExpiry.FromAssembly(typeof(BuildExpiryTests).Assembly);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), expiry.EndTimeUtc);
        Assert.False(expiry.StampInvalid);
    }
}
