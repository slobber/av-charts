namespace AvCharts.Charts;

/// <summary>
/// Names one dimension of a chart: the horizontal or vertical axis of a Cartesian chart, the radius or angle
/// of a polar one.
/// </summary>
/// <remarks>
/// The set is open on purpose. A custom transform (a radar, a sunburst) may introduce dimensions of its own,
/// and what makes them usable is the <see cref="IAxis"/> contract, not an enum someone forgot to extend.
/// The built-in names are the ones an axis renderer and a series look for by default.
/// </remarks>
/// <param name="Name">The name of the dimension, compared ordinally.</param>
public sealed record ChartDimension(string Name)
{
    /// <summary>The horizontal dimension of a Cartesian chart, "x".</summary>
    public static ChartDimension X { get; } = new("x");

    /// <summary>The vertical dimension of a Cartesian chart, "y".</summary>
    public static ChartDimension Y { get; } = new("y");

    /// <summary>The radius of a polar chart, "radius".</summary>
    public static ChartDimension Radius { get; } = new("radius");

    /// <summary>The angle of a polar chart, "angle".</summary>
    public static ChartDimension Angle { get; } = new("angle");

    /// <inheritdoc />
    public override string ToString() => Name;
}
