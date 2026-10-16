using Avalonia;

namespace AvCharts.Charts;

/// <summary>
/// Maps values onto a circle: how far round it a reading sits, and how far out from the middle.
/// </summary>
/// <remarks>
/// <para>
/// The other coordinate system the library knows, and the one a pie needs: a wedge is placed by an angle and a
/// distance rather than by a value across and a value up. Everything above the transform - the layers, hit
/// testing, the data tips - goes on asking the same questions it always asks, which is why a chart of wedges
/// needs no machinery of its own beyond this and the series that draws them.
/// </para>
/// <para>
/// The angle is measured in turns and grows anticlockwise from three o'clock; the radius is a fraction of the
/// circle's own, one at the rim and nought at the middle. Those are Flex's own conventions rather than choices
/// of ours: its wedge renderer places a point at <c>cos(angle)</c> across and <c>-sin(angle)</c> up, and its pie
/// series starts at nought degrees and draws anticlockwise unless a host asks for clockwise. A chart that wants
/// the other direction turns its angles round rather than its transform.
/// </para>
/// <para>
/// The circle is the largest that fits the plot and is centred on it, so a plot that is wider than it is tall
/// still holds a whole circle rather than an ellipse.
/// </para>
/// <para>
/// No axes are needed, and that is the difference between this and the transform a Cartesian chart uses: a pie's
/// wedges are shares of a whole rather than readings on a scale, so there is nothing for an axis to cover. An
/// angle or a radius axis may still be given - a chart with a scale of its own, a gauge, wants them - and then
/// the values are read through that axis the way every other dimension is.
/// </para>
/// </remarks>
public class PolarTransform : DataTransform
{
    /// <summary>The middle of the circle: the middle of the plot.</summary>
    public Point Origin => PlotBounds.Center;

    /// <summary>How far the circle reaches from the middle, in pixels: the largest circle the plot holds.</summary>
    public double Radius => Math.Min(PlotBounds.Width, PlotBounds.Height) / 2;

    /// <inheritdoc />
    /// <remarks>
    /// The two arguments are the angle and the radius. Both are fractions of the circle - a turn, and the
    /// circle's own radius - unless the dimension has an axis, which is what turns a value into either of them.
    /// </remarks>
    public override bool TryValueToPoint(ChartValue angle, ChartValue radius, out Point point)
    {
        point = default;

        if (!TryToUnit(ChartDimension.Angle, angle, out var turns) ||
            !TryToUnit(ChartDimension.Radius, radius, out var reach))
        {
            return false;
        }

        return TryUnitToPoint(turns, reach, out point);
    }

    /// <inheritdoc />
    /// <remarks>The two arguments are the angle in turns and the radius as a fraction of the circle's own.</remarks>
    public override bool TryUnitToPoint(double angle, double radius, out Point point)
    {
        point = default;

        Update();

        var circle = Radius;

        if (double.IsNaN(angle) || double.IsNaN(radius) || !(circle > 0))
        {
            return false;
        }

        var radians = Fraction(ChartDimension.Angle, angle) * 2 * Math.PI;
        var distance = Fraction(ChartDimension.Radius, radius) * circle;
        var middle = Origin;

        point = new Point(
            middle.X + (Math.Cos(radians) * distance),
            middle.Y - (Math.Sin(radians) * distance));

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The values that come back are the angle in turns and the radius as a fraction, unless the dimension has
    /// an axis, in which case they are read through it. Which is the whole of what a hit test on a wedge needs:
    /// ask where the pointer is, and whether it is inside the wedge is a comparison of two angles and two radii.
    /// </remarks>
    public override bool TryPointToValue(Point point, out ChartValue angle, out ChartValue radius)
    {
        angle = ChartValue.Empty;
        radius = ChartValue.Empty;

        Update();

        var circle = Radius;

        if (!(circle > 0))
        {
            return false;
        }

        var middle = Origin;
        var across = point.X - middle.X;
        var up = middle.Y - point.Y;

        // Anticlockwise from three o'clock, as the rest of this class is: the angle of a point straight above
        // the middle is a quarter turn, one straight below it is three quarters, and one to the left of it is
        // half a turn - which is where the sign of the arc tangent has to be turned back into a whole turn.
        var turns = Math.Atan2(up, across) / (2 * Math.PI);

        turns -= Math.Floor(turns);

        var reach = Math.Sqrt((across * across) + (up * up)) / circle;

        angle = FromFraction(ChartDimension.Angle, turns);
        radius = FromFraction(ChartDimension.Radius, reach);
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A turn is as long as the circle it is drawn on, which is what an angle axis needs to decide how many
    /// labels fit round it; a radius is the circle's radius.
    /// </remarks>
    protected override double GetDimensionLength(ChartDimension dimension)
    {
        if (dimension == ChartDimension.Angle)
        {
            return 2 * Math.PI * Radius;
        }

        return dimension == ChartDimension.Radius ? Radius : 0;
    }

    /// <summary>Reads a value through the dimension's axis, or takes it as it stands when there is none.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="value">The value.</param>
    /// <param name="unit">The value in unit space, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the value is missing or the axis cannot read it.</returns>
    /// <remarks>
    /// Without an axis the unit space of a dimension is the circle's own - turns, and fractions of the radius -
    /// so a number is taken as it stands, which is what a pie hands over.
    /// </remarks>
    private bool TryToUnit(ChartDimension dimension, ChartValue value, out double unit) =>
        GetAxis(dimension) is { } axis ? axis.TryToUnit(value, out unit) : value.TryGetNumber(out unit);

    /// <summary>Puts a unit value of a dimension in its range as a fraction; the unit itself when it has no axis.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="unit">The unit value.</param>
    /// <returns>The fraction.</returns>
    private double Fraction(ChartDimension dimension, double unit) =>
        GetAxis(dimension) is { } axis ? GetLayout(axis).UnitRange.Normalize(unit) : unit;

    /// <summary>Turns a fraction of a turn, or of the radius, back into a value through the dimension's axis.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="fraction">The fraction.</param>
    /// <returns>The value; the fraction itself when the dimension has no axis.</returns>
    private ChartValue FromFraction(ChartDimension dimension, double fraction) =>
        GetAxis(dimension) is { } axis
            ? axis.FromUnit(GetLayout(axis).UnitRange.Denormalize(fraction))
            : ChartValue.FromNumber(fraction);
}
