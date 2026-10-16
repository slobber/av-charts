using Avalonia;
using Avalonia.Media;






namespace AvCharts.Drawing;

/// <summary>
/// Collects many shapes into a single geometry, so that a series showing a thousand markers is one fill rather
/// than a thousand draws.
/// </summary>
/// <remarks>
/// <para>
/// The batch is additive and one-shot: shapes go in, <see cref="Build"/> hands out the geometry, and the batch
/// is not meant to be added to afterwards. A series builds its geometry when its data or the mapping changes
/// and keeps the result, which is what makes a still chart free to repaint.
/// </para>
/// <para>
/// Shapes add up where they overlap rather than cancelling each other, which is the nonzero rule and not the
/// default: a shape drawn as several pieces - a whisker with a cap at each end, a mark across both - is the
/// usual case here, and under the even-odd rule every place two of those pieces met would be a hole. Measured
/// the day the third family asked for it: an error bar whose two ends are the same reading came out as 21
/// pixels instead of 40, the mark having been carved away by the two caps laid over it.
/// </para>
/// <para>
/// A shape that wants a hole says so by walking the hole the other way round - see <see cref="AddRing"/> and
/// <see cref="AddWedge"/> - so that what a batch draws does not depend on which rule fills it.
/// </para>
/// </remarks>
public sealed class GeometryBatch
{
    private readonly StreamGeometryContext _context;
    private readonly StreamGeometry _geometry = new();
    private bool _isBuilt;

    /// <summary>Starts a batch.</summary>
    public GeometryBatch()
    {
        _context = _geometry.Open();
        _context.SetFillRule(FillRule.NonZero);
    }

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

    /// <summary>Adds a ring: a circle with a hole in it.</summary>
    /// <param name="centre">The centre.</param>
    /// <param name="outerRadius">How far the rim reaches; zero or less adds nothing.</param>
    /// <param name="innerRadius">How far the hole reaches; zero or less adds a plain circle.</param>
    /// <remarks>
    /// One figure, walked out along the rim and back along the inside, so that the hole is a hole whichever rule
    /// fills the batch rather than something the fill rule happens to leave empty.
    /// </remarks>
    public void AddRing(Point centre, double outerRadius, double innerRadius)
    {
        ThrowIfBuilt();

        var inner = Math.Clamp(innerRadius, 0, outerRadius);

        if (!(outerRadius > 0))
        {
            return;
        }

        if (!(inner > 0))
        {
            AddCircle(centre, outerRadius);
            return;
        }

        var outside = new Size(outerRadius, outerRadius);
        var inside = new Size(inner, inner);
        var left = new Point(centre.X - outerRadius, centre.Y);
        var right = new Point(centre.X + outerRadius, centre.Y);
        var innerLeft = new Point(centre.X - inner, centre.Y);
        var innerRight = new Point(centre.X + inner, centre.Y);

        // The rim all the way round, then the hole all the way round the other way: one figure, and the hole is
        // the part the two directions cancel in.
        _context.BeginFigure(left, true);
        _context.ArcTo(right, outside, 0, false, SweepDirection.Clockwise);
        _context.ArcTo(left, outside, 0, false, SweepDirection.Clockwise);
        _context.LineTo(innerLeft);
        _context.ArcTo(innerRight, inside, 0, false, SweepDirection.CounterClockwise);
        _context.ArcTo(innerLeft, inside, 0, false, SweepDirection.CounterClockwise);
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

    /// <summary>Adds a cross: a plus whose arms reach as far as the radius in all four directions.</summary>
    /// <param name="centre">The centre.</param>
    /// <param name="radius">How far an arm reaches from the centre; zero or less adds nothing.</param>
    /// <remarks>
    /// The arms are as thick as Flex's <c>CrossItemRenderer</c> makes them - three pixels of an eight-pixel glyph -
    /// held as a fraction of the radius rather than as pixels, so that a cross stays a cross at any size. A cross
    /// reads as a target rather than as a mass, which is what makes two of them legible where two squares would
    /// merge.
    /// </remarks>
    public void AddCross(Point centre, double radius)
    {
        ThrowIfBuilt();

        if (!(radius > 0))
        {
            return;
        }

        var arm = radius * 3 / 8;

        _context.BeginFigure(new Point(centre.X - arm, centre.Y - radius), true);
        _context.LineTo(new Point(centre.X + arm, centre.Y - radius));
        _context.LineTo(new Point(centre.X + arm, centre.Y - arm));
        _context.LineTo(new Point(centre.X + radius, centre.Y - arm));
        _context.LineTo(new Point(centre.X + radius, centre.Y + arm));
        _context.LineTo(new Point(centre.X + arm, centre.Y + arm));
        _context.LineTo(new Point(centre.X + arm, centre.Y + radius));
        _context.LineTo(new Point(centre.X - arm, centre.Y + radius));
        _context.LineTo(new Point(centre.X - arm, centre.Y + arm));
        _context.LineTo(new Point(centre.X - radius, centre.Y + arm));
        _context.LineTo(new Point(centre.X - radius, centre.Y - arm));
        _context.LineTo(new Point(centre.X - arm, centre.Y - arm));
        _context.EndFigure(true);
    }

    /// <summary>Adds a triangle standing on its base.</summary>
    /// <param name="centre">The centre of the square it is drawn in.</param>
    /// <param name="radius">Half the width of that square; zero or less adds nothing.</param>
    /// <remarks>
    /// The apex is at the top and the base at the bottom, the way Flex's <c>TriangleItemRenderer</c> draws one, so
    /// that a triangle points at the reading it marks rather than away from it.
    /// </remarks>
    public void AddTriangle(Point centre, double radius)
    {
        ThrowIfBuilt();

        if (!(radius > 0))
        {
            return;
        }

        _context.BeginFigure(new Point(centre.X, centre.Y - radius), true);
        _context.LineTo(new Point(centre.X + radius, centre.Y + radius));
        _context.LineTo(new Point(centre.X - radius, centre.Y + radius));
        _context.EndFigure(true);
    }

    /// <summary>Adds a wedge of a circle: what a pie is made of.</summary>
    /// <param name="centre">The middle of the circle.</param>
    /// <param name="outerRadius">How far the rim of the wedge reaches; zero or less adds nothing.</param>
    /// <param name="innerRadius">How far the rim starts, which a ring-shaped pie is what makes nought or more of.</param>
    /// <param name="fromTurns">Where the wedge begins, in turns round the circle.</param>
    /// <param name="toTurns">Where it ends.</param>
    /// <remarks>
    /// <para>
    /// Anticlockwise when the end is further round than the start and clockwise when it is less, which is how a
    /// series asks for either direction without a second argument saying so. Nought turns is three o'clock, the
    /// convention the rest of the library's polar arithmetic uses.
    /// </para>
    /// <para>
    /// A wedge is one figure rather than the two shapes it looks like - a rim and a middle - so that its hole is
    /// a hole because the figure walks round it rather than because of which rule fills the batch.
    /// </para>
    /// </remarks>
    public void AddWedge(Point centre, double outerRadius, double innerRadius, double fromTurns, double toTurns)
    {
        ThrowIfBuilt();

        if (!(outerRadius > 0) || double.IsNaN(fromTurns) || double.IsNaN(toTurns))
        {
            return;
        }

        var inner = Math.Clamp(innerRadius, 0, outerRadius);
        var sweep = toTurns - fromTurns;
        var large = Math.Abs(sweep) > 0.5;
        var way = sweep >= 0 ? SweepDirection.CounterClockwise : SweepDirection.Clockwise;
        var back = sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;

        _context.BeginFigure(At(fromTurns, outerRadius), true);
        _context.ArcTo(At(toTurns, outerRadius), new Size(outerRadius, outerRadius), 0, large, way);

        if (inner > 0)
        {
            _context.LineTo(At(toTurns, inner));
            _context.ArcTo(At(fromTurns, inner), new Size(inner, inner), 0, large, back);
        }
        else
        {
            _context.LineTo(centre);
        }

        _context.EndFigure(true);

        Point At(double turns, double radius)
        {
            var radians = turns * 2 * Math.PI;

            return new Point(
                centre.X + (Math.Cos(radians) * radius),
                centre.Y - (Math.Sin(radians) * radius));
        }
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
