using Avalonia;
using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// Collects many shapes into a single geometry, so that a series showing a thousand markers is one fill rather
/// than a thousand draws.
/// </summary>
/// <remarks>
/// The batch is additive and one-shot: shapes go in, <see cref="Build"/> hands out the geometry, and the batch
/// is not meant to be added to afterwards. A series builds its geometry when its data or the mapping changes
/// and keeps the result, which is what makes a still chart free to repaint.
/// </remarks>
public sealed class GeometryBatch
{
    private readonly StreamGeometryContext _context;
    private readonly StreamGeometry _geometry = new();
    private bool _isBuilt;

    /// <summary>Starts a batch.</summary>
    public GeometryBatch() => _context = _geometry.Open();

    /// <summary>True once <see cref="Build"/> has been called.</summary>
    public bool IsBuilt => _isBuilt;

    /// <summary>Adds an open or closed run of points as one figure. Fewer than two points add nothing.</summary>
    /// <param name="points">The points, in order.</param>
    /// <param name="isClosed">Whether to close the figure, as an area or a polygon needs.</param>
    public void AddPolyline(IReadOnlyList<Point> points, bool isClosed = false)
    {
        ArgumentNullException.ThrowIfNull(points);
        ThrowIfBuilt();

        if (points.Count < 2)
        {
            return;
        }

        _context.BeginFigure(points[0], isFilled: isClosed);

        for (var index = 1; index < points.Count; index++)
        {
            _context.LineTo(points[index]);
        }

        _context.EndFigure(isClosed: isClosed);
    }

    /// <summary>
    /// Adds a run joined by a smooth curve through every point, with whatever the caller wants after it.
    /// </summary>
    /// <param name="points">The points, in order.</param>
    /// <param name="tail">Points joined to the end of the run by straight lines - the sides of an area.</param>
    /// <param name="isClosed">Whether to close the figure.</param>
    /// <remarks>
    /// <para>
    /// The curve is the monotone one - Fritsch and Carlson's - and the choice is not a matter of taste: a curve
    /// drawn through a series of readings has to stay between the neighbouring readings, and a spline that
    /// overshoots draws a peak the instrument never recorded. A monotone fit rounds the corners off without
    /// ever going above or below what is on either side of them.
    /// </para>
    /// <para>
    /// Readings that do not run left to right are joined by straight lines instead: the tangents of a monotone
    /// fit are only defined where x increases, and inventing an order for the data would be worse than drawing
    /// what it says.
    /// </para>
    /// </remarks>
    public void AddCurve(IReadOnlyList<Point> points, IReadOnlyList<Point>? tail = null, bool isClosed = false)
    {
        ArgumentNullException.ThrowIfNull(points);
        ThrowIfBuilt();

        if (points.Count < 2)
        {
            return;
        }

        if (!IsLeftToRight(points, out var tangents))
        {
            AddPolyline([.. points, .. tail ?? []], isClosed);
            return;
        }

        _context.BeginFigure(points[0], isFilled: isClosed);

        for (var index = 0; index < points.Count - 1; index++)
        {
            var here = points[index];
            var next = points[index + 1];
            var third = (next.X - here.X) / 3;

            _context.CubicBezierTo(
                new Point(here.X + third, here.Y + (tangents[index] * third)),
                new Point(next.X - third, next.Y - (tangents[index + 1] * third)),
                next);
        }

        if (tail is not null)
        {
            foreach (var point in tail)
            {
                _context.LineTo(point);
            }
        }

        _context.EndFigure(isClosed: isClosed);
    }

    /// <summary>
    /// Whether the points run strictly left to right, and if so the tangent of the curve at each of them.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="tangents">The tangents, one per point, when this returns true.</param>
    /// <returns><see langword="true"/> when a monotone curve through them is defined.</returns>
    /// <remarks>
    /// The slopes between the readings are averaged, and then damped by the usual rule: where a slope would take
    /// a tangent past three times the slope of the segment beside it, all of them are pulled back together. That
    /// damping is what keeps the curve monotone, and so what keeps it inside the readings it joins.
    /// </remarks>
    private static bool IsLeftToRight(IReadOnlyList<Point> points, out double[] tangents)
    {
        var count = points.Count;
        var slopes = new double[count - 1];
        tangents = new double[count];

        for (var index = 0; index < count - 1; index++)
        {
            var width = points[index + 1].X - points[index].X;

            if (!(width > 0) || double.IsNaN(points[index].Y) || double.IsNaN(points[index + 1].Y))
            {
                return false;
            }

            slopes[index] = (points[index + 1].Y - points[index].Y) / width;
        }

        if (count == 2)
        {
            tangents[0] = slopes[0];
            tangents[1] = slopes[0];
            return true;
        }

        for (var index = 1; index < count - 1; index++)
        {
            tangents[index] = slopes[index - 1] * slopes[index] <= 0
                ? 0
                : (slopes[index - 1] + slopes[index]) / 2;
        }

        // The ends lean the way of the segment beside them, and the damping pass below has the last word.
        tangents[0] = slopes[0];
        tangents[count - 1] = slopes[count - 2];

        for (var index = 0; index < count - 1; index++)
        {
            var slope = slopes[index];

            if (slope == 0)
            {
                tangents[index] = 0;
                tangents[index + 1] = 0;
                continue;
            }

            var here = tangents[index] / slope;
            var next = tangents[index + 1] / slope;
            var room = (here * here) + (next * next);

            if (room > 9)
            {
                var pull = 3 / Math.Sqrt(room);

                tangents[index] = pull * here * slope;
                tangents[index + 1] = pull * next * slope;
            }
        }

        return true;
    }

    /// <summary>Adds a rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    public void AddRectangle(Rect rect)
    {
        ThrowIfBuilt();

        _context.BeginFigure(rect.TopLeft, true);
        _context.LineTo(rect.TopRight);
        _context.LineTo(rect.BottomRight);
        _context.LineTo(rect.BottomLeft);
        _context.EndFigure(true);
    }

    /// <summary>Adds a circle, as two half arcs.</summary>
    /// <param name="centre">The centre.</param>
    /// <param name="radius">The radius; a radius of zero or less adds nothing.</param>
    public void AddCircle(Point centre, double radius)
    {
        ThrowIfBuilt();

        if (!(radius > 0))
        {
            return;
        }

        var size = new Size(radius, radius);
        var left = new Point(centre.X - radius, centre.Y);
        var right = new Point(centre.X + radius, centre.Y);

        _context.BeginFigure(left, true);
        _context.ArcTo(right, size, 0, false, SweepDirection.Clockwise);
        _context.ArcTo(left, size, 0, false, SweepDirection.Clockwise);
        _context.EndFigure(true);
    }

    /// <summary>Adds a diamond, the shape a marker often wants because it stays legible when it is small.</summary>
    /// <param name="centre">The centre.</param>
    /// <param name="radius">The distance from the centre to a corner; zero or less adds nothing.</param>
    public void AddDiamond(Point centre, double radius)
    {
        ThrowIfBuilt();

        if (!(radius > 0))
        {
            return;
        }

        _context.BeginFigure(new Point(centre.X, centre.Y - radius), true);
        _context.LineTo(new Point(centre.X + radius, centre.Y));
        _context.LineTo(new Point(centre.X, centre.Y + radius));
        _context.LineTo(new Point(centre.X - radius, centre.Y));
        _context.EndFigure(true);
    }

    /// <summary>Finishes the batch and hands out the geometry. A batch can only be built once.</summary>
    /// <returns>The geometry, ready to fill or stroke.</returns>
    public Geometry Build()
    {
        ThrowIfBuilt();

        _isBuilt = true;
        _context.Dispose();

        return _geometry;
    }

    private void ThrowIfBuilt()
    {
        if (_isBuilt)
        {
            throw new InvalidOperationException("This batch has already been built. Start a new one to add more shapes.");
        }
    }
}
