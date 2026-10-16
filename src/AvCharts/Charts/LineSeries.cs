using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// A series whose points are joined by a line, with an optional mark at each point.
/// </summary>
/// <remarks>
/// The line and the marks are two geometries, because they are two different fills: one cloud of marks in the
/// marker colour and one run through them in the line colour. Both are built once per change and kept, so a still
/// chart repaints from cached geometry. There are no marks on the points unless a host asks for them: a line is
/// the shape it draws, and the readings are where it passed.
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class LineSeries<T> : PlotSeries<T>
{
    private IBrush? _stroke;
    private double _strokeThickness = 2;
    private Geometry? _line;

    /// <summary>Creates a line: a series joined by a line, with nothing at the points until it is asked for.</summary>
    /// <remarks>
    /// A plot draws a mark at every reading because marks are what a plot is made of; a line draws the line,
    /// which is what it is made of. The marks are one setting away - <see cref="PlotSeries{T}.Marker"/> - for a
    /// line of a few readings that reads better with them on.
    /// </remarks>
    public LineSeries() => Marker = null;

    /// <summary>
    /// How the readings are joined: straight from one to the next, or a curve through them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Straight by default, which is what a reading is: the value between two of them was never measured, and a
    /// line drawn straight between them says only what is known. A curve is for a shape that is read rather than
    /// measured - a sweep, a temperature, a smooth trend - and it passes through every reading without ever
    /// going past the ones either side of it, so it rounds the corners without inventing a peak.
    /// </para>
    /// <para>
    /// A stepped series joins its readings by stepping whatever this says: its shape is the point of it.
    /// </para>
    /// </remarks>
    public CurveMode Curve
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateGeometry();
        }
    }
    /// <summary>The colour of the line; without one, the chart's palette gives it one.</summary>
    public IBrush? Stroke
    {
        get => _stroke;
        set
        {
            if (ReferenceEquals(_stroke, value))
            {
                return;
            }

            _stroke = value;
            Invalidate();
        }
    }

    /// <summary>The width of the line, in logical pixels.</summary>
    public double StrokeThickness
    {
        get => _strokeThickness;
        set
        {
            if (_strokeThickness.Equals(value))
            {
                return;
            }

            _strokeThickness = value;
            Invalidate();
        }
    }

    /// <inheritdoc />
    public override bool HasOwnColours => Stroke is not null || base.HasOwnColours;

    /// <inheritdoc />
    public override IBrush? Color => Stroke ?? base.Color;

    /// <summary>Puts one run of points into a batch, as this series would have it drawn.</summary>
    /// <param name="batch">The batch to add to.</param>
    /// <param name="run">The points of one stretch of the series, in data order.</param>
    /// <remarks>
    /// The hook a series that joins its points differently overrides - a stepped line - while everything else
    /// about the line, its colour and its width stays the same.
    /// </remarks>
    protected virtual void AddRun(GeometryBatch batch, IReadOnlyList<Point> run)
    {
        if (Curve == CurveMode.Smooth)
        {
            batch.AddCurve(run);
            return;
        }

        batch.AddPolyline(run);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The segment between two records, which is the ink a line is: pointing at the middle of a climb is pointing at
    /// the series, and the reader gets the record at the end of the climb they are nearer to. A smoothed run is
    /// measured as the straight segments between its readings, which is where the curve is drawn to within a pixel
    /// of anyway.
    /// </remarks>
    protected override double Between(Point previous, Point current, Point point) =>
        DistanceToSegment(previous, current, point);

    /// <inheritdoc />
    protected override void BuildInk(in SeriesRenderContext context)
    {
        var batch = new GeometryBatch();

        foreach (var run in BuildRuns(in context))
        {
            if (run.Count > 1)
            {
                AddRun(batch, run);
            }
        }

        _line = batch.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        base.Draw(context, in renderContext);

        // The line is drawn in the series' own colour when it has one, and in the colour the chart took out of
        // its palette for this series otherwise.
        var stroke = Stroke ?? PaletteStroke?.Brush;

        if (_line is not null && stroke is not null)
        {
            context.DrawGeometry(null, new Pen(stroke, StrokeThickness), _line);
        }
    }
}

/// <summary>
/// A line that steps between its points: across, then up, the way a reading that holds its value until the next
/// one is drawn.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class StepLineSeries<T> : LineSeries<T>
{
    /// <inheritdoc />
    protected override void AddRun(GeometryBatch batch, IReadOnlyList<Point> run) =>
        batch.AddPolyline([.. Step(run)]);

    /// <inheritdoc />
    /// <remarks>
    /// The ink between two records is what a step is: the value held across from one to the other, and then the
    /// step up or down to the next one. Both halves are answerable, so a reader pointing at a corner - which is
    /// where a step is read from - gets the series rather than nothing.
    /// </remarks>
    protected override double Between(Point previous, Point current, Point point)
    {
        var corner = new Point(current.X, previous.Y);

        return Math.Min(
            DistanceToSegment(previous, corner, point),
            DistanceToSegment(corner, current, point));
    }

    /// <summary>The points of a run with a corner inserted between each pair of them.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The stepped points.</returns>
    internal static IEnumerable<Point> Step(IReadOnlyList<Point> run)
    {
        yield return run[0];

        for (var index = 1; index < run.Count; index++)
        {
            yield return new Point(run[index].X, run[index - 1].Y);
            yield return run[index];
        }
    }
}
