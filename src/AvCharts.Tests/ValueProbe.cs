using Avalonia;

using AvCharts.Series;





namespace AvCharts.Tests;

/// <summary>
/// A series that shows what it worked out: the values it kept, in unit space, so a test can look at the pipeline
/// rather than at a picture of it.
/// </summary>
/// <typeparam name="TItem">The type of one item in the source.</typeparam>
internal class ValueProbe<TItem> : CartesianSeries<TItem>
{
    /// <summary>The horizontal values, in unit space.</summary>
    public double[] XExposed => XValues.ToArray();

    /// <summary>The vertical values, in unit space.</summary>
    public double[] YExposed => YValues.ToArray();

    /// <summary>The runs the values are drawn as.</summary>
    /// <param name="context">Where the plot is and how to map values into it.</param>
    /// <returns>The runs, in data order.</returns>
    public IReadOnlyList<IReadOnlyList<Point>> Runs(in SeriesRenderContext context) => BuildRuns(in context);

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
    }

    /// <inheritdoc />
    protected override void Draw(Avalonia.Media.DrawingContext context, in SeriesRenderContext renderContext)
    {
    }
}

/// <summary>A <see cref="ValueProbe{TItem}"/> of numbers, which is what most of the pipeline tests want.</summary>
internal sealed class ValueProbe : ValueProbe<double>;
