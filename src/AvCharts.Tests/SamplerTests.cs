using Avalonia;
using AvCharts.Charts;
using Xunit;

namespace AvCharts.Tests;

public class SamplerTests
{
    /// <summary>A thousand columns of mostly quiet data with one spike in the middle.</summary>
    private static List<Point> WithSpike(int count = 2_000, int spikeAt = 1_000)
    {
        var points = new List<Point>(count);

        for (var index = 0; index < count; index++)
        {
            var y = index == spikeAt ? 900d : 10 + (index % 3);
            points.Add(new Point(index, y));
        }

        return points;
    }

    [Fact]
    public void Nothing_is_touched_when_nothing_has_to_be()
    {
        var points = WithSpike(50);

        Assert.Same(points, Sampler.Reduce(points, 100, SamplingMode.Lttb));
        Assert.Same(points, Sampler.Reduce(points, 100, SamplingMode.MinMax));
        Assert.Same(points, Sampler.Reduce(points, 10, SamplingMode.None));
    }

    [Fact]
    public void A_column_count_too_small_to_draw_with_is_refused()
    {
        var points = WithSpike(500);

        Assert.Same(points, Sampler.Reduce(points, 1, SamplingMode.MinMax));
        Assert.Same(points, Sampler.Reduce(points, 0, SamplingMode.Lttb));
    }

    [Fact]
    public void Keeping_the_extremes_never_drops_the_spike()
    {
        // This is the reason MinMax exists: a peak in one column of a thousand must not be averaged away.
        var reduced = Sampler.Reduce(WithSpike(), 200, SamplingMode.MinMax);

        // Two points to a column, low and high, and the two ends of the run may add one each.
        Assert.True(reduced.Count <= 402, $"kept {reduced.Count} points for 200 columns");
        Assert.Contains(reduced, point => point.Y > 800);
        Assert.Equal(0, reduced[0].X);
        Assert.Equal(1_999, reduced[^1].X);
    }

    [Fact]
    public void Keeping_the_extremes_keeps_the_run_in_order()
    {
        var reduced = Sampler.Reduce(WithSpike(), 200, SamplingMode.MinMax);

        // Ascending, or the line would double back on itself.
        for (var index = 1; index < reduced.Count; index++)
        {
            Assert.True(reduced[index].X >= reduced[index - 1].X, $"went backwards at {index}");
        }
    }

    [Fact]
    public void Averaging_calms_a_noisy_run_down()
    {
        var reduced = Sampler.Reduce(WithSpike(), 100, SamplingMode.Average);

        Assert.True(reduced.Count <= 100);
        Assert.True(reduced.Count >= 2);

        // The average of a quiet neighbourhood is still quiet.
        Assert.All(reduced.Take(20), point => Assert.True(point.Y < 100, $"a quiet column averaged {point.Y}"));
    }

    [Fact]
    public void The_largest_triangles_keep_the_shape_of_the_run()
    {
        var reduced = Sampler.Reduce(WithSpike(), 100, SamplingMode.Lttb);

        Assert.Equal(100, reduced.Count);
        Assert.Equal(0, reduced[0].X);
        Assert.Equal(1_999, reduced[^1].X);
        Assert.Contains(reduced, point => point.Y > 800);

        for (var index = 1; index < reduced.Count; index++)
        {
            Assert.True(reduced[index].X > reduced[index - 1].X, $"went backwards at {index}");
        }
    }

    [Fact]
    public void An_empty_run_stays_empty()
    {
        Assert.Empty(Sampler.Reduce([], 100, SamplingMode.Lttb));
        Assert.Empty(Sampler.Reduce([], 100, SamplingMode.MinMax));
        Assert.Empty(Sampler.Reduce([], 100, SamplingMode.Average));
    }

    [Fact]
    public void Points_sharing_one_column_still_come_out()
    {
        // Everything at the same x - a vertical run, which a bucket edge must not divide by zero over.
        var points = Enumerable.Range(0, 100).Select(index => new Point(5, index)).ToList();

        var reduced = Sampler.Reduce(points, 10, SamplingMode.MinMax);

        Assert.True(reduced.Count >= 2);
        Assert.Contains(reduced, point => point.Y == 0);
        Assert.Contains(reduced, point => point.Y == 99);
    }
}
