using Avalonia;
using Avalonia.Media;
using AvCharts.Charts;
using MediaColor = Avalonia.Media.Color;

namespace AvCharts.Demo.Examples.Theme;

/// <summary>Columns with a little depth to them: each one is three faces, and each face has its own light.</summary>
/// <remarks>
/// <para>
/// This is a style of a host's own rather than anything the library draws - the theme page's example of how far
/// a host can take one. It is the design brief's S1 contract and nothing else: two hooks that are already public
/// (<c>PlacedRecords</c> for where the readings are, <see cref="Series.TakePalette"/> for the colours that
/// belong to them), the drawing context the series is handed, and <see cref="GeometryBatch"/> to build the shapes
/// with. A column's slot is worked out here as well, because a series like this one stands on its own: the band
/// arithmetic columns share - how wide a column is when two series are side by side - belongs to
/// <c>ColumnSeries</c>, and a host writing its own shape does the arithmetic of its own shape.
/// </para>
/// <para>
/// Each column is drawn a face at a time, with a gradient down each face and its own colour, so this is a style
/// for the handful of columns a reader is meant to read one of; the library's own columns are one batched
/// geometry and are what a hundred thousand columns want. See the design log.
/// </para>
/// </remarks>
public class ThreeDimensionalColumnSeries<T> : CartesianSeries<T>
{
    /// <summary>How much of its slot a column takes.</summary>
    public double ColumnWidthRatio { get; set; } = 0.55;

    /// <summary>How deep the end of a column looks, as a fraction of its width.</summary>
    public double DepthRatio { get; set; } = 0.3;

    /// <summary>How far past a column its value is written.</summary>
    public double LabelGap { get; set; } = 8;

    /// <summary>The size that value is written in.</summary>
    public double LabelFontSize { get; set; } = 13;

    /// <summary>The face that value is written in.</summary>
    public Typeface LabelTypeface { get; set; } = new(FontFamily.Default);

    private readonly List<Face> _faces = [];
    private readonly List<(Point At, string Value)> _labels = [];
    private IReadOnlyList<IBrush?>? _fills;
    private int _first;
    private IBrush? _ink;

    /// <summary>The colour the legend writes this series in: the first of the colours its columns wear.</summary>
    public override IBrush? Color => BrushAt(0) ?? base.Color;

    /// <summary>Takes the colours its columns are painted in - a slot each, in the order they were added.</summary>
    /// <param name="strokes">The palette's pens.</param>
    /// <param name="fills">The palette's fills.</param>
    /// <param name="first">The slot this series would have taken as a whole.</param>
    /// <remarks>
    /// The same contract the pie takes its colours by, and for the same reason: a chart of one series whose
    /// records are worth reading one at a time wants a colour a record rather than a colour a series.
    /// </remarks>
    public override void TakePalette(IReadOnlyList<Pen?> strokes, IReadOnlyList<IBrush?> fills, int first)
    {
        if (ReferenceEquals(_fills, fills) && _first == first)
        {
            return;
        }

        _fills = fills;
        _first = first;
    }

    /// <summary>Takes the ink the chart writes its labels in.</summary>
    /// <param name="ink">The ink.</param>
    public override void TakeLabelInk(IBrush ink) => _ink = ink;

    /// <summary>
    /// Says how much room the readings want: the slots across, and the values up - from zero, because a column
    /// stands on the floor of the chart rather than at its lowest reading.
    /// </summary>
    /// <param name="description">What the axes are told.</param>
    protected override void DescribeData(DataDescription description)
    {
        base.DescribeData(description);

        // Half a slot on each side of every reading, so that the first and the last column are whole: the window
        // an axis fits to the readings would end at the last one and cut its column in half. That is what a
        // column does and a line does not, and it is the one thing a shape standing in a slot owes its axis.
        if (AxisFor(ChartDimension.X) is { } horizontalAxis
            && ItemsSource is { } readings
            && XSelector is { } horizontalValue)
        {
            foreach (var reading in readings)
            {
                var value = horizontalValue(reading);

                if (value.Kind != ChartValueKind.Empty && horizontalAxis.TryToUnit(value, out var slot))
                {
                    description.Add(ChartDimension.X, new BoundedValue(slot, 0.5, 0.5));
                }
            }
        }

        description.Add(ChartDimension.Y, 0);
    }

    /// <summary>Builds the faces of every column: two sides and the end that gives the bar its depth.</summary>
    /// <param name="context">Where the plot is and how values are placed in it.</param>
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        _faces.Clear();
        _labels.Clear();

        var plot = context.PlotBounds;
        var across = context.Transform.GetLayout(ChartDimension.X);

        if (plot.Width <= 0 || across.UnitRange.Length <= 0)
        {
            return;
        }

        // A slot is one unit across on a category axis, which is the axis this style is drawn against - so the
        // plot's width over the units it covers is how much room a reading has.
        var width = Math.Max(2, plot.Width / across.UnitRange.Length * ColumnWidthRatio);
        var half = width / 2;
        var depth = width * DepthRatio / 2;

        foreach (var (index, x, y, position) in PlacedRecords(context))
        {
            // Zero is unit zero on the value axis, which is what a linear scale is: the value axis of this style
            // is a plain one, starting at zero.
            if (!context.TryUnitPoint(x, 0, out var floor))
            {
                continue;
            }

            var colour = ColourAt(index);
            var value = position.Y;
            var stand = floor.Y;
            var upwards = value <= stand ? -1 : 1;
            var left = position.X - half;
            var right = position.X + half;
            var middle = position.X;

            // The two sides: one catching the light, one in its own shade.
            _faces.Add(new Face(
                Polygon([new(left, value), new(middle, value), new(middle, stand), new(left, stand)]),
                Shade(colour, 0.34, 0)));
            _faces.Add(new Face(
                Polygon([new(middle, value), new(right, value), new(right, stand), new(middle, stand)]),
                Shade(colour, 0, 0.42)));

            // And the end, seen a little from above: a rhombus whose far point is the middle of the edge.
            _faces.Add(new Face(
                Polygon(
                [
                    new(left, value),
                    new(middle, value + (depth * upwards)),
                    new(right, value),
                    new(middle, value - (depth * upwards)),
                ]),
                Shade(colour, 0.55, 0)));

            _labels.Add((new Point(middle, value + (depth * upwards) - ((LabelGap + LabelFontSize) * upwards)), Value(y)));
        }
    }

    /// <summary>Draws every face, and the value over each column.</summary>
    /// <param name="context">The context to draw into.</param>
    /// <param name="renderContext">Where the plot is.</param>
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        foreach (var (shape, brush) in _faces)
        {
            context.DrawGeometry(brush, null, shape);
        }

        if (_ink is not { } ink || LabelFontSize <= 0)
        {
            return;
        }

        foreach (var (at, value) in _labels)
        {
            var text = new FormattedText(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                LabelFontSize,
                ink);

            context.DrawText(text, new Point(at.X - (text.Width / 2), at.Y));
        }
    }

    /// <summary>One face of one column: the shape, and the light it is painted in.</summary>
    /// <param name="Shape">The shape.</param>
    /// <param name="Brush">The light.</param>
    private sealed record Face(Geometry Shape, IBrush Brush);

    /// <summary>The colour the reading at a position wears, out of the chart's palette.</summary>
    /// <param name="index">The position of the reading.</param>
    /// <returns>The colour.</returns>
    private MediaColor ColourAt(int index) =>
        BrushAt(index) is ISolidColorBrush solid ? solid.Color : MediaColor.FromRgb(0x46, 0x82, 0xB4);

    /// <summary>One of the palette's fills, by the reading's place in the series.</summary>
    /// <param name="index">The position of the reading.</param>
    /// <returns>The fill, or null when the chart gave none.</returns>
    private IBrush? BrushAt(int index) =>
        _fills is { Count: > 0 } fills ? fills[(_first + index) % fills.Count] : null;

    /// <summary>A face's light: the column's colour, taken towards white or towards black.</summary>
    /// <param name="colour">The colour.</param>
    /// <param name="lighter">How far towards white.</param>
    /// <param name="darker">How far towards black.</param>
    /// <returns>The brush, fading down the face so that the end of a column reads brighter than its foot.</returns>
    private static IBrush Shade(MediaColor colour, double lighter, double darker)
    {
        var top = lighter > 0 ? Towards(colour, Colors.White, lighter) : Towards(colour, Colors.Black, darker);

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(top, 0),
                new GradientStop(colour, 1),
            },
        };
    }

    /// <summary>Two colours mixed, half way by how far.</summary>
    /// <param name="from">Where the mix starts.</param>
    /// <param name="to">Where it goes.</param>
    /// <param name="how">How far, from none to all.</param>
    /// <returns>The colour.</returns>
    private static MediaColor Towards(MediaColor from, MediaColor to, double how) => MediaColor.FromArgb(
        from.A,
        (byte)Math.Round(from.R + ((to.R - from.R) * how)),
        (byte)Math.Round(from.G + ((to.G - from.G) * how)),
        (byte)Math.Round(from.B + ((to.B - from.B) * how)));

    /// <summary>What a column's value is written as.</summary>
    /// <param name="unit">The value, in unit space, which for a linear value axis is the value.</param>
    /// <returns>The words.</returns>
    private static string Value(double unit) =>
        unit.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A closed shape out of points.</summary>
    /// <param name="points">The corners.</param>
    /// <returns>The shape.</returns>
    private static Geometry Polygon(IReadOnlyList<Point> points)
    {
        var batch = new GeometryBatch();

        batch.AddPolyline(points, isClosed: true);
        return batch.Build();
    }
}

/// <summary>The same style for a page written as markup, which takes any objects rather than a typed list.</summary>
/// <remarks>One line, the way the library's own series do it: markup has no type arguments to give.</remarks>
public class ThreeDimensionalColumnSeries : ThreeDimensionalColumnSeries<object>;
