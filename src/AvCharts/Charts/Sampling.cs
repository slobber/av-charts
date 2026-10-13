using Avalonia;

namespace AvCharts.Charts;

/// <summary>How a series thins its points out when there are more of them than there are pixels to draw them.</summary>
public enum SamplingMode
{
    /// <summary>Draw everything, however dense it gets.</summary>
    None,

    /// <summary>
    /// Keep the lowest and the highest point in every column. What a trace whose peaks are the point - an ECG,
    /// a vibration - needs, because averaging or skipping can delete the very thing being read.
    /// </summary>
    MinMax,

    /// <summary>Keep one point per column, at the average of the points in it: the calmest picture of a noisy signal.</summary>
    Average,

    /// <summary>
    /// Keep a fixed number of points, chosen so the silhouette of the data is kept - the largest-triangle
    /// three-buckets algorithm. The best-looking choice for a line, and the one that costs the most.
    /// </summary>
    Lttb,
}

/// <summary>Thins a run of points down to what can be seen.</summary>
/// <remarks>
/// Sampling is a property of how a series is drawn rather than of the data, so it lives here as arithmetic over
/// already-mapped positions and knows nothing about axes, series or styles - which is what makes it testable
/// without a chart.
/// </remarks>
public static class Sampler
{
    /// <summary>The least a reduction is allowed to leave behind, so a silly bucket count cannot empty a series.</summary>
    public const int MinimumPoints = 2;

    /// <summary>
    /// Reduces a run to at most as many points as there are columns to put them in.
    /// </summary>
    /// <param name="points">The run, in x order.</param>
    /// <param name="columns">How many columns the run has room for.</param>
    /// <param name="mode">How to choose what to keep.</param>
    /// <returns>
    /// The points to draw: the same list when nothing is to be done, otherwise a shorter one. A run that already
    /// fits is never touched, and a missing value in a run is never reintroduced.
    /// </returns>
    public static IReadOnlyList<Point> Reduce(IReadOnlyList<Point> points, int columns, SamplingMode mode)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (mode == SamplingMode.None || columns < MinimumPoints || points.Count <= columns)
        {
            return points;
        }

        return mode switch
        {
            SamplingMode.MinMax => MinMax(points, columns),
            SamplingMode.Average => Average(points, columns),
            _ => Lttb(points, columns),
        };
    }

    private static IReadOnlyList<Point> MinMax(IReadOnlyList<Point> points, int columns)
    {
        // Two points per column: the lowest and the highest, so a spike can never be dropped by a bucket that
        // happens to hold it in the middle of a run of quiet samples.
        var buckets = Math.Max(1, columns / 2);
        var first = points[0].X;
        var last = points[^1].X;
        var width = last > first ? (last - first) / buckets : 0;
        var kept = new List<Point>(buckets * 2);

        for (var bucket = 0; bucket < buckets; bucket++)
        {
            var from = first + (bucket * width);
            var to = bucket == buckets - 1 ? last : first + ((bucket + 1) * width);
            var lowest = (Point?)null;
            var highest = (Point?)null;

            foreach (var point in points)
            {
                if (point.X < from || (bucket < buckets - 1 && point.X >= to))
                {
                    continue;
                }

                if (lowest is null || point.Y < lowest.Value.Y)
                {
                    lowest = point;
                }

                if (highest is null || point.Y > highest.Value.Y)
                {
                    highest = point;
                }
            }

            if (lowest is null || highest is null)
            {
                continue;
            }

            // In x order, so the run stays a run.
            if (lowest.Value.Equals(highest.Value) || lowest.Value.X <= highest.Value.X)
            {
                kept.Add(lowest.Value);

                if (!lowest.Value.Equals(highest.Value))
                {
                    kept.Add(highest.Value);
                }
            }
            else
            {
                kept.Add(highest.Value);
                kept.Add(lowest.Value);
            }
        }

        if (kept.Count < MinimumPoints)
        {
            return points;
        }

        // The two ends of a run are the two the eye looks for - and the newest sample of a live feed is the
        // whole point of the chart - so they survive even when neither is an extreme of its column.
        if (!kept[0].Equals(points[0]))
        {
            kept.Insert(0, points[0]);
        }

        if (!kept[^1].Equals(points[^1]))
        {
            kept.Add(points[^1]);
        }

        return kept;
    }

    private static IReadOnlyList<Point> Average(IReadOnlyList<Point> points, int columns)
    {
        var first = points[0].X;
        var last = points[^1].X;
        var width = last > first ? (last - first) / columns : 0;
        var kept = new List<Point>(columns);
        var sumX = 0d;
        var sumY = 0d;
        var count = 0;
        var bucket = 0;

        foreach (var point in points)
        {
            var belongsTo = width > 0 ? Math.Min(columns - 1, (int)((point.X - first) / width)) : 0;

            if (belongsTo != bucket && count > 0)
            {
                kept.Add(new Point(sumX / count, sumY / count));
                sumX = 0;
                sumY = 0;
                count = 0;
                bucket = belongsTo;
            }

            sumX += point.X;
            sumY += point.Y;
            count++;
        }

        if (count > 0)
        {
            kept.Add(new Point(sumX / count, sumY / count));
        }

        return kept.Count >= MinimumPoints ? kept : points;
    }

    private static IReadOnlyList<Point> Lttb(IReadOnlyList<Point> points, int columns)
    {
        var kept = new List<Point>(columns);
        var every = (double)(points.Count - 2) / (columns - 2);
        var selected = 0;

        kept.Add(points[0]);

        for (var index = 0; index < columns - 2; index++)
        {
            // The average of the next bucket is the far corner of the triangle.
            var nextFrom = (int)Math.Floor((index + 1) * every) + 1;
            var nextTo = (int)Math.Min(Math.Floor((index + 2) * every) + 1, points.Count);
            var averageX = 0d;
            var averageY = 0d;

            for (var point = nextFrom; point < nextTo; point++)
            {
                averageX += points[point].X;
                averageY += points[point].Y;
            }

            var span = Math.Max(1, nextTo - nextFrom);
            averageX /= span;
            averageY /= span;

            var from = (int)Math.Floor((index + 0) * every) + 1;
            var to = (int)Math.Floor((index + 1) * every) + 1;
            var anchor = points[selected];
            var bestArea = -1d;
            var best = from;

            for (var point = from; point < to && point < points.Count; point++)
            {
                var area = Math.Abs(
                    ((anchor.X - averageX) * (points[point].Y - anchor.Y)) -
                    ((anchor.X - points[point].X) * (averageY - anchor.Y)));

                if (area > bestArea)
                {
                    bestArea = area;
                    best = point;
                }
            }

            kept.Add(points[best]);
            selected = best;
        }

        kept.Add(points[^1]);

        return kept;
    }
}
