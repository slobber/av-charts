using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks what hiding a series takes out of the chart: its ink, the ranges the axes cover, the room a second
/// axis of its own took, the slots it stood in, the records a hit test or a data tip can find.
/// </summary>
/// <remarks>
/// The design brief asks for <c>Series.Visible</c> and says a hidden series takes no part in the axis ranges or
/// the layout. Flex has nothing like it - there, hiding a series is the application's business - so these tests
/// are what says the whole of "no part" is meant, rather than only the drawing.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class VisibilityTests
{
    [Fact]
    public void A_hidden_series_is_not_drawn()
    {
        var visible = Line([1.0, 2.0, 3.0], Brushes.DarkOrange);
        var hidden = Line([1.0, 2.0, 3.0], Brushes.Magenta);
        var chart = Bare(visible, hidden);

        Assert.True(Painted(chart, Colors.Magenta) > 0, "the series should have been drawn to begin with");

        hidden.Visible = false;

        Assert.Equal(0, Painted(chart, Colors.Magenta));
        Assert.True(Painted(chart, Colors.DarkOrange) > 0, "the other series should still be there");

        hidden.Visible = true;

        Assert.True(Painted(chart, Colors.Magenta) > 0, "showing it again should draw it again");
    }

    [Fact]
    public void A_hidden_series_is_out_of_the_ranges_the_axes_cover()
    {
        var small = Line([1.0, 2.0, 3.0], Brushes.DarkOrange);
        var tall = Line([1.0, 2.0, 400.0], Brushes.Magenta);
        var chart = Bare(small, tall);

        Assert.True(Range(chart) >= 100, $"the tall series should have stretched the axis: {Range(chart)}");

        tall.Visible = false;
        Lay(chart);

        // The axis covers what is left, which is the whole of "a hidden series takes no part in the ranges".
        Assert.True(Range(chart) <= 4, $"the axis should have shrunk back: {Range(chart)}");

        tall.Visible = true;
        Lay(chart);

        Assert.True(Range(chart) >= 100, "showing it again should stretch the axis again");
    }

    [Fact]
    public void A_hidden_series_is_out_of_the_hits_the_tips_and_the_view()
    {
        var visible = Line([1.0, 2.0, 3.0], Brushes.DarkOrange);
        var hidden = Line([1.0, 2.0, 3.0], Brushes.Magenta);
        var chart = Bare(visible, hidden);

        var where = Position(hidden, 1);

        // Both are under the pointer to begin with - they are drawn in the same place.
        Assert.Equal(2, chart.FindDataPoints(where).Count);
        Assert.Equal(6, chart.GetAllDataPoints().Count());

        hidden.Visible = false;

        // Nothing is there now, so nothing is found: a record with no ink is not a record to point at, which is
        // the same rule that keeps a tip off a gap.
        Assert.Single(chart.FindDataPoints(where));
        Assert.Equal(3, chart.GetAllDataPoints().Count());
        Assert.All(chart.GetAllDataPoints(), item => Assert.Same(visible, item.Series));

        var inView = chart.GetItemsInRegion(chart.PlotBounds);

        Assert.Equal(3, inView.Count);
        Assert.All(inView, item => Assert.Same(visible, item.Series));
    }

    [Fact]
    public void A_hidden_series_gives_back_the_room_its_own_axis_took()
    {
        var plain = Line([1.0, 2.0, 3.0], Brushes.DarkOrange);
        var scaled = Line([1.0, 2.0, 3.0], Brushes.Magenta);
        scaled.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 };

        var chart = Bare(plain, scaled);
        var withTheSecondAxis = chart.PlotBounds;

        // A second scale on the far side takes room from the plot, which is what its gutter is.
        Assert.True(withTheSecondAxis.Right < Width, "the second axis should have taken room on the right");

        scaled.Visible = false;
        Lay(chart);

        // Hiding the series hides the scale it brought: the plot gets the room back rather than leaving a gutter
        // for an axis with nothing measured against it.
        Assert.True(chart.PlotBounds.Right > withTheSecondAxis.Right, "the room should have come back");

        scaled.Visible = true;
        Lay(chart);

        Assert.Equal(withTheSecondAxis.Right, chart.PlotBounds.Right, 6);
    }

    [Fact]
    public void A_hidden_column_stands_in_no_band()
    {
        var first = Column(Brushes.Magenta);
        var second = Column(Brushes.DarkOrange);
        var chart = Bare(first, second);
        chart.SeriesLayout = SeriesLayout.Cluster;
        Lay(chart);

        var clustered = Painted(chart, Colors.Magenta);

        second.Visible = false;
        Lay(chart);

        // One column in the slot rather than two: it takes the whole width, so there is more of it. That is the
        // band layout hearing about the hiding at all, which is the half of "no part in the layout" that is not
        // the axes.
        Assert.True(
            Painted(chart, Colors.Magenta) > clustered,
            $"the column should have widened from {clustered} pixels");
    }

    [Fact]
    public void A_hidden_column_folds_into_no_stack()
    {
        var baseSeries = Column(Brushes.Magenta, 3.0);
        var onTop = Column(Brushes.DarkOrange, 4.0);
        var chart = Bare(baseSeries, onTop);
        chart.SeriesLayout = SeriesLayout.Stacked;
        Lay(chart);

        // Stacked, the axis grows to the sum of what is on it.
        Assert.Equal(7, chart.Transform.GetLayout(ChartDimension.Y).UnitRange.Maximum, 6);

        onTop.Visible = false;
        Lay(chart);

        // A hidden series contributes no height, so the stack is what is left rather than seven with a hole in it.
        Assert.Equal(3, chart.Transform.GetLayout(ChartDimension.Y).UnitRange.Maximum, 6);
    }

    /// <summary>How much of one colour the chart's series layer painted.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="colour">The colour to look for.</param>
    /// <returns>The number of pixels.</returns>
    private static int Painted(CartesianChart chart, Color colour)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(Width, Height), new Vector(96, 96));

        using (var context = bitmap.CreateDrawingContext())
        {
            chart.SeriesLayer.Render(context);
        }

        return Ink.Count(bitmap, colour);
    }

    /// <summary>How far up the vertical axis reaches, in data values.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The top of the range.</returns>
    private static double Range(CartesianChart chart) =>
        chart.Transform.GetLayout(ChartDimension.Y).UnitRange.Maximum;

    /// <summary>Lays the chart out again, which is where a hiding is worked out.</summary>
    /// <param name="chart">The chart.</param>
    private static void Lay(CartesianChart chart)
    {
        chart.Measure(new Size(Width, Height));
        chart.Arrange(new Rect(0, 0, Width, Height));
    }

    /// <summary>Where a record is drawn, which is where a pointer has to be to find it.</summary>
    /// <param name="series">The series to look in.</param>
    /// <param name="index">The record's position in the data.</param>
    /// <returns>The position on the chart.</returns>
    private static Point Position(Series series, int index) =>
        series.Items.Single(item => item.Index == index).Position!.Value;

    /// <summary>A series of readings drawn in a colour of its own, so a test can look for its ink.</summary>
    /// <param name="values">The readings.</param>
    /// <param name="stroke">The colour to draw it in.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Line(double[] values, IBrush stroke) =>
        new()
        {
            ItemsSource = values,
            XSelector = value => value,
            YSelector = value => value,
            Stroke = stroke,
            StrokeThickness = 3,
        };

    /// <summary>One column of a colour of its own, at the first slot.</summary>
    /// <param name="fill">The colour to fill it with.</param>
    /// <param name="height">How tall it stands.</param>
    /// <returns>The series.</returns>
    private static ColumnSeries<double> Column(IBrush fill, double height = 1) =>
        new()
        {
            ItemsSource = [height],
            XSelector = _ => 1,
            YSelector = value => value,
            Fill = fill,
        };

    /// <summary>A chart with nothing dressing it, laid out, holding the series given.</summary>
    /// <param name="series">The series to put on it.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart Bare(params Series[] series)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis(),
            VerticalAxis = new LinearAxis(),
        };

        foreach (var one in series)
        {
            chart.Series.Add(one);
        }

        Lay(chart);
        return chart;
    }

    private const int Width = 400;
    private const int Height = 200;
}
