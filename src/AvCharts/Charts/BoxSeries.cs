using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// Readings drawn as boxes: one per record, with a box from one quartile to the other, a line across it at the
/// median, and a whisker reaching to the smallest and the largest reading.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
/// <remarks>
/// <para>
/// Five readings to a record - the smallest, the two quartiles, the median and the largest - which is what a box
/// plot is made of, and what a series of samples says about itself: where the middle half of it lies, where the
/// middle is, and how far the ends reach. A reading that is not given is read as the median, so a series given
/// nothing but its readings draws a line of hairlines where they are rather than nothing at all.
/// </para>
/// <para>
/// The box is the shape a reader looks at: it is filled with <see cref="Fill"/> and outlined with
/// <see cref="Stroke"/>, while the median, the whiskers and their caps are drawn in the pen's brush - so a chart
/// of boxes says, at a glance, which part of it is the middle half and which part is the reach, and a host that
/// sets one ink still gets both drawn in it.
/// </para>
/// <para>
/// Flex has nothing of this shape to copy - its <c>BoxItemRenderer</c> is a legend swatch, a rounded square - so
/// the shape is the conventional one: Tukey's box and whiskers, drawn to what the design brief asks for.
/// </para>
/// </remarks>
public class BoxSeries<T> : StatisticalSeries<T>
{
    private Func<T, ChartValue>? _minimum;
    private Func<T, ChartValue>? _lowerQuartile;
    private Func<T, ChartValue>? _median;
    private Func<T, ChartValue>? _upperQuartile;
    private Func<T, ChartValue>? _maximum;
    private Geometry? _boxes;
    private Geometry? _lines;

    /// <summary>How to read the smallest reading of a record; unset, the median is taken for it.</summary>
    public Func<T, ChartValue>? MinimumSelector
    {
        get => _minimum;
        set => Read(ref _minimum, value);
    }

    /// <summary>How to read the lower quartile of a record; unset, the median is taken for it.</summary>
    public Func<T, ChartValue>? LowerQuartileSelector
    {
        get => _lowerQuartile;
        set => Read(ref _lowerQuartile, value);
    }

    /// <summary>How to read the median of a record, which is the record's own value.</summary>
    /// <remarks>
    /// Setting it sets <see cref="CartesianSeries{T}.YSelector"/> too, because the median is what a record of
    /// samples stands for on the value axis: the slot it stands in, the axis that covers it and the tip that reads
    /// it are all about this number.
    /// </remarks>
    public Func<T, ChartValue>? MedianSelector
    {
        get => _median;
        set
        {
            if (ReferenceEquals(_median, value))
            {
                return;
            }

            _median = value;
            YSelector = value;
        }
    }

    /// <summary>How to read the upper quartile of a record; unset, the median is taken for it.</summary>
    public Func<T, ChartValue>? UpperQuartileSelector
    {
        get => _upperQuartile;
        set => Read(ref _upperQuartile, value);
    }

    /// <summary>How to read the largest reading of a record; unset, the median is taken for it.</summary>
    public Func<T, ChartValue>? MaximumSelector
    {
        get => _maximum;
        set => Read(ref _maximum, value);
    }

    /// <summary>Where a record's smallest reading is written, as a path; for a markup file.</summary>
    public string? MinimumPath
    {
        get;
        set => Path(ref field, value, next => MinimumSelector = Paths.Value<T>(next));
    }

    /// <summary>Where a record's lower quartile is written, as a path.</summary>
    public string? LowerQuartilePath
    {
        get;
        set => Path(ref field, value, next => LowerQuartileSelector = Paths.Value<T>(next));
    }

    /// <summary>Where a record's median is written, as a path.</summary>
    /// <remarks><c>MedianPath</c> is <see cref="MedianSelector"/> for a page, so it sets <c>YPath</c> as well.</remarks>
    public string? MedianPath
    {
        get;
        set => Path(ref field, value, next =>
        {
            MedianSelector = Paths.Value<T>(next);
            YPath = next;
        });
    }

    /// <summary>Where a record's upper quartile is written, as a path.</summary>
    public string? UpperQuartilePath
    {
        get;
        set => Path(ref field, value, next => UpperQuartileSelector = Paths.Value<T>(next));
    }

    /// <summary>Where a record's largest reading is written, as a path.</summary>
    public string? MaximumPath
    {
        get;
        set => Path(ref field, value, next => MaximumSelector = Paths.Value<T>(next));
    }

    /// <summary>The colour a box is filled with; without one, the chart's palette fills it.</summary>
    public IBrush? Fill
    {
        get;
        set => Paint(ref field, value);
    }

    /// <summary>The pen around a box, and the ink the median, the whiskers and the caps are drawn in.</summary>
    public Pen? Stroke
    {
        get;
        set => Paint(ref field, value);
    }

    /// <inheritdoc />
    public override bool HasOwnColours => Fill is not null || Stroke is not null;

    /// <inheritdoc />
    public override IBrush? Color => Fill ?? Stroke?.Brush ?? base.Color;

    /// <inheritdoc />
    protected override bool TryExtent(int index, out double low, out double high)
    {
        low = MinimumOf(index);
        high = MaximumOf(index);
        return !double.IsNaN(low) && !double.IsNaN(high);
    }

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        var boxes = new GeometryBatch();
        var lines = new GeometryBatch();

        if (TryBandFacts(in context, out var facts))
        {
            foreach (var (index, _, _, position) in PlacedRecords(context))
            {
                AddBox(boxes, lines, index, position, in context, in facts);
            }
        }

        _boxes = boxes.Build();
        _lines = lines.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        var fill = Fill ?? PaletteFill;

        // The middle, the whiskers and their caps are lines a pixel wide, so they are drawn in a pen: the host's,
        // the chart's, or the fill's own colour at full strength - never in the tint the box itself is filled with,
        // which is how a box plot loses its middle.
        var ink = InkOf(Stroke, PaletteStroke?.Brush, OutlineOf(fill)?.Brush ?? fill);

        if (_boxes is not null && (fill is not null || Stroke is not null))
        {
            context.DrawGeometry(fill, Stroke, _boxes);
        }

        if (_lines is not null && ink is not null)
        {
            context.DrawGeometry(ink, null, _lines);
        }
    }

    /// <summary>The smallest reading of a record, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double MinimumOf(int index) => ReadingOf(index, MinimumSelector) is { } value && !double.IsNaN(value)
        ? value
        : MedianOf(index);

    /// <summary>The lower quartile of a record, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double LowerQuartileOf(int index) =>
        ReadingOf(index, LowerQuartileSelector) is { } value && !double.IsNaN(value) ? value : MedianOf(index);

    /// <summary>The median of a record, in the units of the value axis; the record's own value.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double MedianOf(int index) => index >= 0 && index < _count ? YValues[index] : double.NaN;

    /// <summary>The upper quartile of a record, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double UpperQuartileOf(int index) =>
        ReadingOf(index, UpperQuartileSelector) is { } value && !double.IsNaN(value) ? value : MedianOf(index);

    /// <summary>The largest reading of a record, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double MaximumOf(int index) => ReadingOf(index, MaximumSelector) is { } value && !double.IsNaN(value)
        ? value
        : MedianOf(index);

    /// <summary>Adds one record's box to one batch and its hairlines to the other, unless it draws neither.</summary>
    /// <param name="boxes">The batch the box belongs in.</param>
    /// <param name="lines">The batch the median, the whiskers and the caps belong in.</param>
    /// <param name="index">The position of the record.</param>
    /// <param name="position">Where the record was laid out.</param>
    /// <param name="context">Where the plot is and how finely it is drawn.</param>
    /// <param name="facts">How the boxes stand in their slots.</param>
    private void AddBox(
        GeometryBatch boxes,
        GeometryBatch lines,
        int index,
        Point position,
        in SeriesRenderContext context,
        in BandFacts facts)
    {
        var minimum = MinimumOf(index);
        var lower = LowerQuartileOf(index);
        var median = MedianOf(index);
        var upper = UpperQuartileOf(index);
        var maximum = MaximumOf(index);
        var x = XValues[index];

        if (double.IsNaN(median) || double.IsNaN(minimum) || double.IsNaN(maximum) || double.IsNaN(lower) ||
            double.IsNaN(upper) || !TryPoint(x, minimum, out var minimumAt) || !TryPoint(x, lower, out var lowerAt) ||
            !TryPoint(x, median, out var medianAt) || !TryPoint(x, upper, out var upperAt) ||
            !TryPoint(x, maximum, out var maximumAt))
        {
            return;
        }

        // The box is centred on its slot and its sides are snapped to the pixel grid so that a run of them tiles;
        // the quartiles stay where the readings put them.
        var edge = Math.Max(context.Snap(facts.Width), 1);
        var left = Math.Round(context.Snap(position.X + facts.Shift) - (edge / 2));
        var middle = Math.Round(left + ((edge - 1) / 2));
        var cap = Math.Max(Math.Round(edge / 4), 1);
        var top = Math.Min(lowerAt.Y, upperAt.Y);
        var bottom = Math.Max(lowerAt.Y, upperAt.Y);
        var reach = Math.Min(minimumAt.Y, maximumAt.Y);
        var reachEnd = Math.Max(minimumAt.Y, maximumAt.Y);

        boxes.AddRectangle(new Rect(left, top, edge, Math.Max(bottom - top, 1)));

        // The whiskers first, so that the caps are drawn over their ends rather than beside them. A cap reaches as
        // far either way as half the mark does, for the same reason an error bar's does.
        Add(lines, new Rect(middle, reach, 1, Math.Max(top - reach, 1)));
        Add(lines, new Rect(middle, bottom, 1, Math.Max(reachEnd - bottom, 1)));
        Add(lines, new Rect(middle - cap + 1, context.Snap(reach), (2 * cap) - 1, 1));
        Add(lines, new Rect(middle - cap + 1, context.Snap(reachEnd), (2 * cap) - 1, 1));
        Add(lines, new Rect(left, context.Snap(medianAt.Y), edge, 1));
    }

    /// <summary>Adds one hairline, unless it is too thin to draw.</summary>
    /// <param name="batch">The batch to add it to.</param>
    /// <param name="line">The line.</param>
    private static void Add(GeometryBatch batch, Rect line)
    {
        if (line.Width >= 1 && line.Height >= 1)
        {
            batch.AddRectangle(line);
        }
    }

    /// <summary>Sets a selector, reading the source again only when it is a different one.</summary>
    /// <param name="field">The selector being set.</param>
    /// <param name="value">The selector to set.</param>
    private void Read(ref Func<T, ChartValue>? field, Func<T, ChartValue>? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;
        InvalidateData();
    }

    /// <summary>Sets one of the five paths, doing whatever its reading needs doing.</summary>
    /// <param name="field">The path being set.</param>
    /// <param name="value">The path to set.</param>
    /// <param name="apply">What to do with the path.</param>
    private static void Path(ref string? field, string? value, Action<string?> apply)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;
        apply(value);
    }

    /// <summary>Sets one of the two inks, asking for a repaint only when it is a different one.</summary>
    /// <typeparam name="TInk">The type of ink.</typeparam>
    /// <param name="field">The ink being set.</param>
    /// <param name="value">The ink to set.</param>
    private void Paint<TInk>(ref TInk? field, TInk? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;
        Repaint();
    }
}
