using System.Globalization;

namespace AvCharts.Charts;

/// <summary>How an axis decides what to cover.</summary>
public enum AxisScaleMode
{
    /// <summary>Size the range to the data, so that the data fills the plot. What Flex's axes do.</summary>
    Fit,

    /// <summary>
    /// Hold the scale fixed at <see cref="AxisBase.PixelsPerUnit"/>: one unit of data keeps its size on
    /// screen, and a wider chart shows more data rather than wider spacing. What a real-scale reading - an
    /// ECG strip, a trading chart - needs.
    /// </summary>
    Fixed,
}

/// <summary>
/// The settings every axis has, and the arithmetic a numeric one uses to cover its data.
/// </summary>
/// <remarks>
/// <para>
/// Subclasses supply the only two things that differ between axes: how a value becomes a unit
/// (<see cref="TryToUnit"/>) and how a unit becomes a value again (<see cref="FromUnit"/>). A category axis,
/// whose unit space is slots rather than numbers, also overrides <see cref="Resolve"/>.
/// </para>
/// <para>
/// Nothing here is cached. <see cref="Resolve"/> is pure, so the results belong to whoever asked - the
/// transform - and one axis instance can serve several charts at once.
/// </para>
/// </remarks>
public abstract class AxisBase : IAxis
{
    /// <summary>The steps a value is allowed to take, per decade: 1, 2, 2.5, 5.</summary>
    /// <remarks>
    /// The 2.5 is what keeps the grid from breathing: without it the gap between 2 and 5 is a factor of 2.5,
    /// so a fixed pixel pitch would swing that much as the scale changes.
    /// </remarks>
    private static readonly double[] DefaultLadder = [1, 2, 2.5, 5];

    /// <summary>How many grid lines are drawn at most, so a silly interval cannot lock the renderer up.</summary>
    private const int MaximumTickCount = 2048;

    /// <summary>How many intervals to aim for when the room available is not known.</summary>
    private const double DefaultIntervalCount = 5;

    /// <summary>Raised when a setting changed, so whoever holds this axis can work itself out again.</summary>
    public event EventHandler? Changed;

    /// <summary>The name used in labels and legends.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The text drawn along the axis, when one is set.</summary>
    public string? Title { get; set; }

    /// <inheritdoc />
    public abstract ChartValueKind ValueKind { get; }

    /// <summary>Whether the range follows the data or holds a fixed scale.</summary>
    public AxisScaleMode ScaleMode
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    /// <summary>
    /// The anchor of the range, in unit space. Set, it beats the data; unset, the data decides. In
    /// <see cref="AxisScaleMode.Fixed"/> it is also the position the window starts from.
    /// </summary>
    public double? Minimum
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    /// <summary>
    /// The top of the range, in unit space. Set, it beats the data in <see cref="AxisScaleMode.Fit"/>;
    /// in <see cref="AxisScaleMode.Fixed"/> it is ignored, because the scale decides where the window ends
    /// (see <see cref="IsMaximumIgnored"/>).
    /// </summary>
    public double? Maximum
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    /// <summary>
    /// The distance between labelled lines, in unit space. Left unset, it is worked out so that the gaps come
    /// out near <see cref="MajorGridPitch"/> and land on a readable number.
    /// </summary>
    /// <remarks>
    /// A gap of exactly <see cref="MajorGridPitch"/> pixels is only possible if the labels are allowed to be
    /// ugly ("0.0372"). Since they are not, the gap steps between round numbers and so breathes within a
    /// factor of two around the pitch. A finer ladder narrows the breathing, at the cost of more numbers
    /// nobody wants to read.
    /// </remarks>
    public double? Interval
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    /// <summary>How much of one unit of data is one pixel across, in <see cref="AxisScaleMode.Fixed"/>.</summary>
    public double PixelsPerUnit
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    } = 1;

    /// <summary>The pixel gap the grid lines aim for; 60 by default.</summary>
    public double MajorGridPitch
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    } = 60;

    /// <summary>The pixel gap the fine grid lines aim for; 12 by default, so five of them make a major gap.</summary>
    public double MinorGridPitch
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    } = 12;

    /// <summary>
    /// Explicit positions, in unit space, to draw labelled lines at instead of the generated ones - where a
    /// domain has its own idea of a meaningful value. Assigning a new list is what notifies; mutating the
    /// list in place does not.
    /// </summary>
    public IReadOnlyList<double>? Separators
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    /// <summary>
    /// True when <see cref="Maximum"/> was set but <see cref="ScaleMode"/> is <see cref="AxisScaleMode.Fixed"/>,
    /// where the window ends where the scale says it does. Exposed so a host can tell the user instead of
    /// leaving them wondering.
    /// </summary>
    public bool IsMaximumIgnored => ScaleMode == AxisScaleMode.Fixed && Maximum.HasValue;

    /// <inheritdoc />
    public abstract bool TryToUnit(ChartValue value, out double unit);

    /// <inheritdoc />
    public abstract ChartValue FromUnit(double unit);

    /// <summary>Writes a unit value the way a label should read it.</summary>
    /// <param name="unit">The value, in unit space.</param>
    /// <returns>The text.</returns>
    public virtual string FormatValue(double unit) => unit.ToString("G", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public AxisLayout Resolve(IReadOnlyList<BoundedValue> bounds, double lengthInPixels)
    {
        ArgumentNullException.ThrowIfNull(bounds);

        // Fixed mode only means anything when there is a scale to fix and room to apply it to; without
        // either, the axis behaves like a fitting one rather than covering nothing.
        var usesFixedScale = ScaleMode == AxisScaleMode.Fixed && PixelsPerUnit > 0 && lengthInPixels > 0;

        var (minimum, maximum) = ResolveDataRange(bounds, usesFixedScale);
        var interval = ResolveInterval(minimum, maximum, lengthInPixels, usesFixedScale);
        var minorInterval = interval / MinorDivisions();

        if (usesFixedScale)
        {
            var span = lengthInPixels / PixelsPerUnit;
            minimum = Math.Floor(minimum / interval) * interval;
            maximum = minimum + span;
        }

        var range = new AxisUnitRange(minimum, maximum);

        return new AxisLayout(
            range,
            interval,
            minorInterval,
            BuildTicks(range, interval, Separators),
            BuildTicks(range, minorInterval, null));
    }

    /// <summary>
    /// Snaps a wanted gap to the nearest readable value at or above it: 1, 2, 2.5 or 5 times a power of ten.
    /// </summary>
    /// <param name="target">The gap that would be ideal, in unit space.</param>
    /// <returns>The gap to use.</returns>
    protected virtual double NiceInterval(double target)
    {
        if (!(target > 0) || double.IsInfinity(target))
        {
            return 1;
        }

        var scale = Math.Pow(10, Math.Floor(Math.Log10(target)));

        foreach (var step in DefaultLadder)
        {
            var candidate = step * scale;

            if (candidate >= target * (1 - 1e-9))
            {
                return candidate;
            }
        }

        return 10 * scale;
    }

    /// <summary>Raises <see cref="Changed"/>.</summary>
    protected void Invalidate() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>How many fine gaps make up one major gap.</summary>
    /// <returns>The count, at least one.</returns>
    protected virtual double MinorDivisions() =>
        MajorGridPitch > 0 && MinorGridPitch > 0 ? Math.Max(1, Math.Round(MajorGridPitch / MinorGridPitch)) : 1;

    private static IReadOnlyList<double> BuildTicks(
        AxisUnitRange range,
        double interval,
        IReadOnlyList<double>? separators)
    {
        var ticks = new List<double>();

        if (range.IsEmpty || !(interval > 0) || double.IsInfinity(interval))
        {
            return ticks;
        }

        if (separators is { Count: > 0 })
        {
            foreach (var separator in separators)
            {
                if (separator >= range.Minimum && separator <= range.Maximum)
                {
                    ticks.Add(separator);
                }
            }

            return ticks;
        }

        var first = Math.Ceiling(range.Minimum / interval) * interval;
        var epsilon = interval * 1e-9;

        for (var index = 0; index < MaximumTickCount; index++)
        {
            var value = first + (index * interval);

            if (value > range.Maximum + epsilon)
            {
                break;
            }

            ticks.Add(value);
        }

        return ticks;
    }

    private double ResolveInterval(double minimum, double maximum, double lengthInPixels, bool usesFixedScale)
    {
        if (Interval is { } fixedInterval && fixedInterval > 0)
        {
            return fixedInterval;
        }

        var target = usesFixedScale
            ? MajorGridPitch / PixelsPerUnit
            : (maximum - minimum) / IntervalCount(lengthInPixels);

        // A range with no width has no grid to draw: inventing one would put lines where there is nothing to
        // line up with.
        return target > 0 ? NiceInterval(target) : 0;
    }

    private double IntervalCount(double lengthInPixels) =>
        lengthInPixels > 0 && MajorGridPitch > 0
            ? Math.Max(1, Math.Round(lengthInPixels / MajorGridPitch))
            : DefaultIntervalCount;

    private (double Minimum, double Maximum) ResolveDataRange(IReadOnlyList<BoundedValue> bounds, bool usesFixedScale)
    {
        var dataMinimum = double.NaN;
        var dataMaximum = double.NaN;

        foreach (var bound in bounds)
        {
            dataMinimum = double.IsNaN(dataMinimum) ? bound.Minimum : Math.Min(dataMinimum, bound.Minimum);
            dataMaximum = double.IsNaN(dataMaximum) ? bound.Maximum : Math.Max(dataMaximum, bound.Maximum);
        }

        // In Fixed mode the scale says where the window ends, so Maximum has nothing to say.
        var wantedMaximum = usesFixedScale ? null : Maximum;

        var minimum = Minimum ?? dataMinimum;
        var maximum = wantedMaximum ?? dataMaximum;

        if (double.IsNaN(minimum) && double.IsNaN(maximum))
        {
            // Nothing to cover: the range collapses to the origin, which reads as empty and leaves the
            // transform mapping whatever it is given to the middle of the plot.
            return (0, 0);
        }

        if (double.IsNaN(minimum))
        {
            minimum = maximum;
        }
        else if (double.IsNaN(maximum))
        {
            maximum = minimum;
        }

        return maximum < minimum ? (maximum, minimum) : (minimum, maximum);
    }
}
