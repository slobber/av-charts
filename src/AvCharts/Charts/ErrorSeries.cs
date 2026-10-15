using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// Readings drawn as error bars: one bar per record, with a mark where the reading is, a whisker from one end of
/// its error to the other, and a cap at each end.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
/// <remarks>
/// <para>
/// One reading to a record - what <see cref="CartesianSeries{T}.YSelector"/> holds - and one error either side of
/// it, which is what an error bar says: this is the number, and this is how much of it is doubt. A record with no
/// error given is drawn as its mark alone, because a reading whose doubt was not measured is still a reading.
/// </para>
/// <para>
/// The three pieces are the ones a reader of error bars expects, and their lengths are what tells them apart: the
/// mark runs the whole width of the band, the whisker is a hairline up the middle from one end of the error to
/// the other, and a cap reaches half as far as the mark does from the middle - so the value can be told from the
/// two ends at a glance rather than by measuring.
/// </para>
/// <para>
/// The error is read in the data's own units and added and taken away there before the two ends are mapped, so
/// that an error of five means five of whatever the axis measures. A mark whose two ends are worked out in the
/// axis' own space instead would say "five" on a linear axis and "five times" on a logarithmic one.
/// </para>
/// </remarks>
public class ErrorSeries<T> : StatisticalSeries<T>
{
    private Func<T, double>? _error;
    private Geometry? _marks;

    /// <summary>How to read the error either side of the reading, in the data's own units; unset, no whisker.</summary>
    /// <remarks>
    /// A number rather than a reading, which is what an error is: not a value the axis places but a distance
    /// either side of one - the same distinction the bubble series makes about the size of its marks.
    /// </remarks>
    public Func<T, double>? ErrorSelector
    {
        get => _error;
        set => Read(ref _error, value);
    }

    /// <summary>Where the error is written, as a path; for a markup file.</summary>
    /// <remarks><c>ErrorPath</c> is <see cref="ErrorSelector"/> for a page.</remarks>
    public string? ErrorPath
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            ErrorSelector = Paths.Number<T>(value);
        }
    }

    /// <summary>The colour the mark, the whisker and the caps are drawn in.</summary>
    public IBrush? Fill
    {
        get;
        set => Paint(ref field, value);
    }

    /// <summary>The pen the bar's three hairlines are drawn in, read for its brush.</summary>
    /// <remarks>A host that sets a pen gets its colour, the way it would on a bar of prices; see <see cref="Series"/>'s own notes on the two.</remarks>
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
        low = 0;
        high = 0;

        var value = ValueOf(index);

        if (double.IsNaN(value))
        {
            return false;
        }

        var error = ErrorOf(index);

        if (double.IsNaN(error) || !(error > 0))
        {
            // Nothing measured about the doubt: the mark at the value is the whole of what there is to draw.
            low = value;
            high = value;
            return true;
        }

        var axis = AxisFor(ChartDimension.Y);

        if (axis is not null && axis.FromUnit(value) is { IsEmpty: false } read &&
            read.TryGetNumber(out var number) &&
            axis.TryToUnit(number - error, out low) && axis.TryToUnit(number + error, out high))
        {
            return true;
        }

        // An axis that cannot give a reading back as a number - a category axis, which measures names - leaves the
        // error to be counted in the axis' own space, which is the best an axis without units can be told.
        low = value - error;
        high = value + error;
        return true;
    }

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        var batch = new GeometryBatch();

        if (TryBandFacts(in context, out var facts))
        {
            foreach (var (index, _, _, position) in PlacedRecords(context))
            {
                AddBar(batch, index, position, in context, in facts);
            }
        }

        _marks = batch.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        var ink = InkOf(Stroke, Fill, PaletteFill ?? PaletteStroke?.Brush);

        if (_marks is not null && ink is not null)
        {
            context.DrawGeometry(ink, null, _marks);
        }
    }

    /// <summary>The reading a bar stands for, in the units of the value axis.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The value, in unit space; NaN when the record has none.</returns>
    protected double ValueOf(int index) => index >= 0 && index < _count ? YValues[index] : double.NaN;

    /// <summary>The error either side of a reading, in the data's own units; NaN when it was not given.</summary>
    /// <param name="index">The position of the record.</param>
    /// <returns>The error.</returns>
    protected double ErrorOf(int index) =>
        _error is not null && index >= 0 && index < _count && _items[index] is T item ? _error(item) : double.NaN;

    /// <summary>Adds one bar's mark, whisker and two caps to the batch, unless the record draws none.</summary>
    /// <param name="batch">The batch to add them to.</param>
    /// <param name="index">The position of the record.</param>
    /// <param name="position">Where the record was laid out.</param>
    /// <param name="context">Where the plot is and how finely it is drawn.</param>
    /// <param name="facts">How the bars stand in their slots.</param>
    private void AddBar(
        GeometryBatch batch,
        int index,
        Point position,
        in SeriesRenderContext context,
        in BandFacts facts)
    {
        if (double.IsNaN(ValueOf(index)) || !TryExtent(index, out var low, out var high))
        {
            return;
        }

        var x = XValues[index];

        if (!TryPoint(x, low, out var lowAt) || !TryPoint(x, high, out var highAt))
        {
            return;
        }

        var value = ValueOf(index);

        if (!TryPoint(x, value, out var valueAt))
        {
            return;
        }

        // The bar is centred on its slot and its sides are snapped to the pixel grid so that a run of them tiles.
        var edge = Math.Max(context.Snap(facts.Width), 1);
        var left = Math.Round(context.Snap(position.X + facts.Shift) - (edge / 2));
        var middle = Math.Round(left + ((edge - 1) / 2));
        var cap = Math.Max(Math.Round(edge / 4), 1);
        var top = Math.Min(lowAt.Y, highAt.Y);
        var bottom = Math.Max(lowAt.Y, highAt.Y);

        // The whisker first, so that the two caps and the mark are drawn over it rather than around it.
        Add(batch, new Rect(middle, top, 1, Math.Max(bottom - top, 1)));
        Add(batch, new Rect(left, context.Snap(valueAt.Y), edge, 1));
        Add(batch, new Rect(middle - cap + 1, context.Snap(top), cap, 1));
        Add(batch, new Rect(middle, context.Snap(bottom), cap, 1));
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

    /// <summary>Sets the error selector, reading the source again only when it is a different one.</summary>
    /// <param name="field">The selector being set.</param>
    /// <param name="value">The selector to set.</param>
    private void Read(ref Func<T, double>? field, Func<T, double>? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;
        InvalidateData();
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
