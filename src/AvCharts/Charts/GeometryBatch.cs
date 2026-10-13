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
