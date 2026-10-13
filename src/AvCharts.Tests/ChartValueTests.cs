using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

public class ChartValueTests
{
    [Fact]
    public void Empty_is_default_and_says_so()
    {
        Assert.True(ChartValue.Empty.IsEmpty);
        Assert.Equal(ChartValueKind.Empty, ChartValue.Empty.Kind);
        Assert.True(default(ChartValue).IsEmpty);
    }

    [Fact]
    public void A_number_keeps_its_payload()
    {
        ChartValue value = 12.5;

        Assert.Equal(ChartValueKind.Number, value.Kind);
        Assert.Equal(12.5, value.Number);
        Assert.False(value.IsEmpty);
        Assert.True(value.TryGetNumber(out var number));
        Assert.Equal(12.5, number);
    }

    [Fact]
    public void Ints_and_floats_reach_the_double_overload()
    {
        Assert.Equal(7d, ((ChartValue)7).Number);
        Assert.Equal(0.5d, ((ChartValue)0.5f).Number);
    }

    [Fact]
    public void NaN_reads_as_missing()
    {
        ChartValue value = double.NaN;

        Assert.True(value.IsEmpty);
        Assert.False(value.TryGetNumber(out _));
    }

    [Fact]
    public void A_null_nullable_reads_as_missing_and_a_value_does_not()
    {
        double? missing = null;
        double? present = 3;

        Assert.True(((ChartValue)missing).IsEmpty);
        Assert.Equal(3d, ((ChartValue)present).Number);
    }

    [Fact]
    public void An_instant_keeps_its_payload_and_is_not_a_number()
    {
        var instant = new DateTimeOffset(2026, 10, 13, 8, 30, 0, TimeSpan.Zero);
        ChartValue value = instant;

        Assert.Equal(ChartValueKind.DateTime, value.Kind);
        Assert.Equal(instant, value.Instant);
        Assert.False(value.TryGetNumber(out _));
    }

    [Fact]
    public void A_moment_keeps_the_clock_it_arrived_with()
    {
        // An unspecified DateTime is taken as it stands: reading it as the machine's time zone would make a
        // chart depend on which machine drew it.
        ChartValue unspecified = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(TimeSpan.Zero, unspecified.Instant.Offset);
        Assert.Equal(10, unspecified.Instant.Hour);

        // A local one keeps the offset it had, so ordering by UtcTicks still puts it in the right place.
        var local = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Local);
        ChartValue fromLocal = local;
        Assert.Equal(new DateTimeOffset(local).Offset, fromLocal.Instant.Offset);

        // And an offset that was given is carried through untouched.
        var withOffset = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.FromHours(8));
        ChartValue carried = withOffset;
        Assert.Equal(TimeSpan.FromHours(8), carried.Instant.Offset);
        Assert.Equal(withOffset.UtcTicks, carried.Instant.UtcTicks);
    }

    [Fact]
    public void A_null_or_empty_category_reads_as_missing()
    {
        string? nothing = null;
        string empty = string.Empty;

        Assert.True(((ChartValue)nothing).IsEmpty);
        Assert.True(((ChartValue)empty).IsEmpty);
        Assert.Equal("north", ((ChartValue)"north").Category);
    }

    [Fact]
    public void Equality_is_over_kind_and_payload()
    {
        Assert.Equal((ChartValue)1, (ChartValue)1);
        Assert.NotEqual((ChartValue)1, (ChartValue)2);
        Assert.NotEqual((ChartValue)1, (ChartValue)ChartValue.FromDateTime(new DateTime(2026, 1, 1)));
        Assert.NotEqual((ChartValue)"a", (ChartValue)"b");
        Assert.Equal((ChartValue)"a", (ChartValue)"a");
        Assert.Equal(ChartValue.Empty, (ChartValue)(double?)null);
        Assert.Equal(ChartValue.Empty.GetHashCode(), ((ChartValue)(double?)null).GetHashCode());
        Assert.True((ChartValue)1 != (ChartValue)2);
    }

    [Fact]
    public void ToString_is_invariant()
    {
        Assert.Equal("12.5", ((ChartValue)12.5).ToString());
        Assert.Equal("north", ((ChartValue)"north").ToString());
        Assert.Equal(string.Empty, ChartValue.Empty.ToString());
        Assert.StartsWith("2026-10-13T", ((ChartValue)new DateTime(2026, 10, 13)).ToString(), StringComparison.Ordinal);
    }
}
