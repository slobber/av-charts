using Avalonia;


using AvCharts.Data;



namespace AvCharts.Series;

/// <summary>
/// A series whose records are drawn as marks standing in bands: an interval, an error bar, a box.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
/// <remarks>
/// <para>
/// What these have in common is less a shape than a way of asking about one. Every mark of this kind reaches from
/// one reading to another, stands in the slot its record names and is as wide as the band it was given; and a
/// pointer over the chart is on the mark it is inside of, which is not the place the record's own value would be
/// a mark at - for an interval that value is one end of it, and for a box it is somewhere inside the box.
/// </para>
/// <para>
/// The shape itself belongs to the series below this one. What is here is the frame all of them are read through:
/// the two ends of a record's mark, what the axis has to cover because of it, and the hit test that answers with
/// the mark rather than with the number.
/// </para>
/// </remarks>
public abstract class StatisticalSeries<T> : BandedSeries<T>
{
    /// <inheritdoc />
    protected override void DescribeData(DataDescription description)
    {
        if (AxisFor(BandDimension) is null)
        {
            return;
        }

        EnsureData();

        var across = BandDimension == ChartDimension.X;

        for (var index = 0; index < _count; index++)
        {
            // Half a band of room on each side, so that the first and the last mark are whole.
            DescribeSlot(description, across ? XValues[index] : YValues[index]);

            if (AxisFor(ChartDimension.Y) is null || !TryExtent(index, out var low, out var high))
            {
                continue;
            }

            // What the axis covers is where the mark reaches at all - its two ends and never an axis zero.
            foreach (var value in new[] { low, high })
            {
                if (!double.IsNaN(value))
                {
                    description.Add(ChartDimension.Y, value);
                }
            }
        }
    }

    /// <summary>
    /// The two ends of one record's mark, in the units of the value axis: what it reaches from and to.
    /// </summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="low">The end it reaches from, when this returns <see langword="true"/>.</param>
    /// <param name="high">The end it reaches to.</param>
    /// <returns><see langword="false"/> when the record draws no mark.</returns>
    /// <remarks>
    /// The shape a series below this one draws is built from these two numbers and from the band, so a mark that
    /// reaches no further than its readings is one series and its ends are the whole of what it says.
    /// </remarks>
    protected abstract bool TryExtent(int index, out double low, out double high);

    /// <summary>
    /// The nearest record whose mark is within reach of a point.
    /// </summary>
    /// <param name="point">The position, in the chart's own coordinates.</param>
    /// <param name="sensitivity">How far the pointer may be from the mark, in logical pixels.</param>
    /// <returns>The hit, or null.</returns>
    /// <remarks>
    /// A mark is not a point, and this is the other half of the design brief's S1 contract: what a pointer on a
    /// chart of intervals is on is the mark a record draws, not the place its value would be a dot at. A pointing
    /// device answers between frames, so the mark is worked out from the values the series is holding, by the same
    /// arithmetic the geometry pass used.
    /// </remarks>
    public override HitData? HitTestItem(Point point, double sensitivity)
    {
        EnsureData();

        if (!Visible || Transform is not { } transform || !(sensitivity > 0) || _count == 0)
        {
            return null;
        }

        var plot = transform.PlotBounds;
        HitData? best = null;

        for (var index = 0; index < _count; index++)
        {
            if (!TryMark(index, out var mark))
            {
                continue;
            }

            var distance = DistanceTo(mark, point, plot);

            if (distance <= sensitivity && (best is null || distance < best.Value.Distance))
            {
                best = Hit(index, distance);
            }
        }

        return best;
    }

    /// <summary>Where one record's mark stands, as the rectangle it is drawn inside of.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="mark">The rectangle, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the record draws no mark.</returns>
    /// <remarks>
    /// The whole mark rather than the piece of it a pointer might be nearest: a pointer near the middle of a long
    /// whisker is on the whisker, and answering with the box would be answering about ink that is not there.
    /// </remarks>
    private bool TryMark(int index, out Rect mark)
    {
        mark = default;

        if (!TryBandEdges(index, out var near, out var far) || !TryExtent(index, out var low, out var high))
        {
            return false;
        }

        var across = BandDimension == ChartDimension.X;
        var band = across ? XValues[index] : YValues[index];

        if (double.IsNaN(band) ||
            !TryPoint(across ? band : low, across ? low : band, out var one) ||
            !TryPoint(across ? band : high, across ? high : band, out var other))
        {
            return false;
        }

        var from = Math.Min(across ? one.Y : one.X, across ? other.Y : other.X);
        var size = Math.Max(Math.Abs((across ? other.Y : other.X) - (across ? one.Y : one.X)), 1);

        mark = across
            ? new Rect(near, from, far - near, size)
            : new Rect(from, near, size, far - near);
        return true;
    }

    /// <summary>How far a point is from a mark, in pixels; zero inside it.</summary>
    /// <param name="mark">The rectangle the record draws.</param>
    /// <param name="point">The position.</param>
    /// <param name="plot">The plot, which is where the ink stops.</param>
    /// <returns>The distance.</returns>
    /// <remarks>
    /// Only the part of a mark inside the plot has ink - the layer clips what a series draws - so a mark reaching
    /// past the edge is reached where it is drawn and not where it is not.
    /// </remarks>
    private static double DistanceTo(Rect mark, Point point, Rect plot)
    {
        var drawn = mark.Intersect(plot);

        if (!(drawn.Width > 0) && !(drawn.Height > 0))
        {
            return double.PositiveInfinity;
        }

        var across = Math.Max(0, Math.Max(drawn.Left - point.X, point.X - drawn.Right));
        var up = Math.Max(0, Math.Max(drawn.Top - point.Y, point.Y - drawn.Bottom));

        return Math.Sqrt((across * across) + (up * up));
    }
}
