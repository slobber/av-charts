using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// A line with the region under it filled: the shape a reading that is a quantity rather than a position wants.
/// </summary>
/// <remarks>
/// <para>
/// The fill goes down to the bottom of the plot, which is where Flex fills to as well: the region means "this much
/// and no more", and the axis is what says how much room that is. A record with no value breaks the region in
/// two rather than being filled across, which is the one place this deliberately parts company with Flex:
/// its <c>AreaSeries</c> reaches for the nearest valid value and adds a small offset to it, which is a value
/// nobody measured drawn as though somebody had.
/// </para>
/// <para>
/// A stacked area - regions resting on each other - needs the base of each region to be a total rather than the
/// floor of the plot, and belongs with the rest of the stacking work.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class AreaSeries<T> : LineSeries<T>
{
    private IBrush? _fill;
    private Geometry? _area;

    /// <summary>The edge the region is filled down to, kept from the last paint so that the ink can be pointed at.</summary>
    private double _bottom;

    /// <summary>The colour of the region under the line; without one, the chart's palette fills it.</summary>
    public IBrush? Fill
    {
        get => _fill;
        set
        {
            if (ReferenceEquals(_fill, value))
            {
                return;
            }

            _fill = value;
            Invalidate();
        }
    }

    /// <inheritdoc />
    public override bool HasOwnColours => Fill is not null || base.HasOwnColours;

    /// <inheritdoc />
    public override IBrush? Color => Fill ?? base.Color;

    /// <inheritdoc />
    protected override void BuildInk(in SeriesRenderContext context)
    {
        // The line first, so that whatever a subclass makes of it is there, and then the region beneath.
        base.BuildInk(in context);

        var batch = new GeometryBatch();
        var bottom = context.PlotBounds.Bottom;

        _bottom = bottom;

        foreach (var run in BuildRuns(in context))
        {
            if (run.Count > 1)
            {
                AddArea(batch, run, bottom);
            }
        }

        _area = batch.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        // The region goes down first, so the line sits on its edge rather than under it.
        var fill = Fill ?? PaletteFill;

        if (_area is not null && fill is not null)
        {
            context.DrawGeometry(fill, null, _area);
        }

        base.Draw(context, in renderContext);
    }

    /// <summary>Puts one run into a batch as a closed region: out along the run, back along the base.</summary>
    /// <param name="batch">The batch to add to.</param>
    /// <param name="run">The points of one stretch of the series, in data order.</param>
    /// <param name="bottom">The edge the region is filled down to.</param>
    protected virtual void AddArea(GeometryBatch batch, IReadOnlyList<Point> run, double bottom)
    {
        if (Curve == CurveMode.Smooth)
        {
            // The top edge is the same curve the line above it is drawn with - an area whose edge disagreed with
            // its own line would show the disagreement along the whole run - and the sides and the bottom are
            // straight, because that is what they are.
            batch.AddCurve(run, [new Point(run[^1].X, bottom), new Point(run[0].X, bottom)], isClosed: true);
            return;
        }

        batch.AddPolyline(
            [.. run, new Point(run[^1].X, bottom), new Point(run[0].X, bottom)],
            isClosed: true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A region is ink as much as its edge is: pointing inside the shaded part is pointing at this series, which is
    /// what a reader does when the edge is somewhere above their hand. The record the hit stands for is the end of
    /// the stretch they are over that they are nearer to.
    /// </remarks>
    protected override double Between(Point previous, Point current, Point point) =>
        Inside(previous, current, point) ? 0 : base.Between(previous, current, point);

    /// <summary>Whether a point is inside the region the stretch between two records shades.</summary>
    /// <param name="previous">Where the earlier record is drawn.</param>
    /// <param name="current">Where the later one is.</param>
    /// <param name="point">The point.</param>
    /// <returns>True when it is under the top edge and above the floor of the plot.</returns>
    private bool Inside(Point previous, Point current, Point point)
    {
        if (!(point.X >= Math.Min(previous.X, current.X)) || !(point.X <= Math.Max(previous.X, current.X)))
        {
            return false;
        }

        var span = current.X - previous.X;
        var top = Math.Abs(span) > 0
            ? previous.Y + ((point.X - previous.X) / span * (current.Y - previous.Y))
            : Math.Max(previous.Y, current.Y);

        var floor = _bottom > 0 ? _bottom : Transform?.PlotBounds.Bottom ?? 0;

        return point.Y >= top && point.Y <= floor;
    }
}

/// <summary>
/// An area whose edges step between the points, the region under a <see cref="StepLineSeries{T}"/>.
/// </summary>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class StepAreaSeries<T> : AreaSeries<T>
{
    /// <inheritdoc />
    protected override void AddRun(GeometryBatch batch, IReadOnlyList<Point> run) =>
        batch.AddPolyline([.. StepLineSeries<T>.Step(run)]);

    /// <inheritdoc />
    protected override void AddArea(GeometryBatch batch, IReadOnlyList<Point> run, double bottom)
    {
        var stepped = StepLineSeries<T>.Step(run).ToArray();

        batch.AddPolyline(
            [.. stepped, new Point(stepped[^1].X, bottom), new Point(stepped[0].X, bottom)],
            isClosed: true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The step and the region both: the value held across from one record to the other is what the top edge is, so
    /// the part below it and above the floor is inside the shading, and the corner where the two meet is where a
    /// reader points when they want that stretch of it.
    /// </remarks>
    protected override double Between(Point previous, Point current, Point point)
    {
        var step = base.Between(previous, current, point);

        if (step <= 0)
        {
            return step;
        }

        var corner = new Point(current.X, previous.Y);
        var floor = Transform?.PlotBounds.Bottom ?? 0;
        var held = point.X >= Math.Min(previous.X, corner.X)
            && point.X <= Math.Max(previous.X, corner.X)
            && point.Y >= previous.Y
            && point.Y <= floor;

        return held ? 0 : step;
    }
}
