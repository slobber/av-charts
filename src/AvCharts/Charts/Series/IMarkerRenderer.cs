using Avalonia;

using AvCharts.Drawing;




namespace AvCharts.Series;

/// <summary>
/// Draws one data point's mark into a batch of geometry.
/// </summary>
/// <remarks>
/// An interface rather than a base class on purpose: replacing a marker is the most common customisation a
/// chart gets, it carries no state worth inheriting, and the batch is what makes a thousand of them cost one
/// fill. The built-in shapes are on <see cref="Markers"/>.
/// </remarks>
public interface IMarkerRenderer
{
    /// <summary>Adds the mark for one point to a batch.</summary>
    /// <param name="batch">The batch to add to.</param>
    /// <param name="centre">Where the point is, in the layer's coordinates.</param>
    /// <param name="radius">How far the mark reaches from the centre.</param>
    void AddTo(GeometryBatch batch, Point centre, double radius);
}

/// <summary>The marker shapes that ship with the library.</summary>
public static class Markers
{
    /// <summary>A circle: the default, and the one that reads best at small sizes.</summary>
    public static IMarkerRenderer Circle { get; } = new CircleMarker();

    /// <summary>A square.</summary>
    public static IMarkerRenderer Square { get; } = new SquareMarker();

    /// <summary>
    /// A diamond. Worth reaching for where markers overlap: a diamond leaves more of its neighbours visible
    /// than a circle of the same size does.
    /// </summary>
    public static IMarkerRenderer Diamond { get; } = new DiamondMarker();

    /// <summary>
    /// A cross: a plus whose arms reach the radius in all four directions.
    /// </summary>
    /// <remarks>
    /// Worth reaching for where marks sit on top of each other: a cross reads as a target rather than as a mass,
    /// so two of them stay apart where two squares of the same size would merge into one blob. It is the shape
    /// Flex calls <c>CrossItemRenderer</c>, arms and all.
    /// </remarks>
    public static IMarkerRenderer Cross { get; } = new CrossMarker();

    /// <summary>A triangle standing on its base: the odd one out where several series share a plot.</summary>
    public static IMarkerRenderer Triangle { get; } = new TriangleMarker();

    private sealed class CircleMarker : IMarkerRenderer
    {
        public void AddTo(GeometryBatch batch, Point centre, double radius) => batch.AddCircle(centre, radius);
    }

    private sealed class SquareMarker : IMarkerRenderer
    {
        public void AddTo(GeometryBatch batch, Point centre, double radius) =>
            batch.AddRectangle(new Rect(centre.X - radius, centre.Y - radius, radius * 2, radius * 2));
    }

    private sealed class DiamondMarker : IMarkerRenderer
    {
        public void AddTo(GeometryBatch batch, Point centre, double radius) => batch.AddDiamond(centre, radius);
    }

    private sealed class CrossMarker : IMarkerRenderer
    {
        public void AddTo(GeometryBatch batch, Point centre, double radius) => batch.AddCross(centre, radius);
    }

    private sealed class TriangleMarker : IMarkerRenderer
    {
        public void AddTo(GeometryBatch batch, Point centre, double radius) => batch.AddTriangle(centre, radius);
    }
}
