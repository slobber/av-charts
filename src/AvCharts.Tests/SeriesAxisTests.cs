using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Series;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the axis a series can bring of its own: that its values are measured against it, that the chart draws
/// it on the far edge, and that one axis instance still holds nothing that belongs to a chart.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class SeriesAxisTests
{
    [Fact]
    public void A_series_is_measured_against_the_axis_it_named()
    {
        // Two scales on one chart: a plain series and one on a logarithmic axis, over data that has nothing to do
        // with each other.
        var logarithmic = new LogAxis();
        var plain = Line([0.0, 5.0, 10.0]);
        var wide = Line([1.0, 10.0, 1000.0]);
        wide.VerticalAxis = logarithmic;

        var chart = Chart(plain, wide);

        // Each axis covers the series measured against it and nothing else: the plain one is in values, the
        // logarithmic one in exponents, and neither has been stretched to accommodate the other's data.
        Assert.Equal((0, 10), Range(chart, chart.VerticalAxis));
        Assert.Equal((0, 3), Range(chart, logarithmic));
    }

    [Fact]
    public void A_second_axis_is_drawn_on_the_far_edge_and_takes_its_own_room()
    {
        var plain = Line([0.0, 5.0, 10.0]);
        var withItsOwn = Line([1.0, 10.0, 1000.0]);
        withItsOwn.VerticalAxis = new LogAxis();

        var alone = Chart(plain);
        var together = Chart(plain, withItsOwn);

        // The second axis needs room on the right, which the plot gives up rather than the labels overlapping.
        Assert.True(
            together.PlotBounds.Width < alone.PlotBounds.Width,
            $"the second axis took no room ({together.PlotBounds.Width} against {alone.PlotBounds.Width})");

        // And something is drawn out there: the marks and labels of the axis itself.
        var bitmap = new RenderTargetBitmap(new PixelSize(600, 400));
        bitmap.Render(together);

        var right = new Rect(together.PlotBounds.Right, 0, together.Bounds.Width - together.PlotBounds.Right, 400);

        Assert.True(Ink.Count(bitmap, right) > 0, "the second axis should have drawn its labels on the right");
    }

    [Fact]
    public void Taking_the_series_away_takes_its_axis_away_too()
    {
        var plain = Line([0.0, 5.0, 10.0]);
        var withItsOwn = Line([1.0, 10.0, 1000.0]);
        withItsOwn.VerticalAxis = new LogAxis();

        var chart = Chart(plain, withItsOwn);
        var together = chart.PlotBounds.Width;

        chart.Series.Remove(withItsOwn);
        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));

        // The room the second axis took is given back when the series that needed it goes: an axis nobody is
        // measured against is not drawn, and does not go on holding a gutter.
        Assert.True(chart.PlotBounds.Width > together, $"the gutter stayed behind ({chart.PlotBounds.Width} against {together})");
    }

    [Fact]
    public void A_series_is_placed_and_hit_tested_by_its_own_axis()
    {
        var wide = Line([1.0, 10.0, 1000.0]);
        wide.VerticalAxis = new LogAxis();
        var chart = Chart(wide);

        var item = chart.GetAllDataPoints().First(point => point.Index == 1);
        var where = item.Position!.Value;

        // Where the series says the record is, and what a pointer finds there, are the same answer - which is
        // what one mapping for the whole series buys.
        Assert.Equal(item.Index, wide.HitTestItem(where, 5)!.Value.ChartItem.Index);

        // A tenth of the way up a logarithmic axis of three decades is not where a tenth of the value would be.
        var plot = chart.PlotBounds;

        Assert.Equal(plot.Bottom - (plot.Height / 3), where.Y, 3);
    }

    [Fact]
    public void One_series_axis_can_drive_several_charts_at_once()
    {
        // The property the design brief asks to have locked: an axis holds settings and not results, so two
        // charts can share one - including when it is a series that brought it.
        // Shared horizontally, because the two charts differ in width: a vertical axis in two charts of the same
        // height would come out the same either way and prove nothing.
        var shared = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 10, Minimum = 0 };

        var narrow = Line([0.0, 1.0]);
        narrow.HorizontalAxis = shared;
        var wide = Line([0.0, 1.0]);
        wide.HorizontalAxis = shared;

        var first = Chart(200, narrow);
        var second = Chart(400, wide);

        // Each chart works the shared axis out against its own plot, which is the property: the axis holds settings
        // and no results, so the wider chart can show more of it without touching what the narrow one shows.
        Assert.Equal(first.PlotBounds.Width / 10, first.Transform.GetLayout(shared).UnitRange.Length, 3);
        Assert.Equal(second.PlotBounds.Width / 10, second.Transform.GetLayout(shared).UnitRange.Length, 3);
        Assert.True(
            second.Transform.GetLayout(shared).UnitRange.Length > first.Transform.GetLayout(shared).UnitRange.Length,
            "the wider chart should show more of the same scale");
        Assert.Same(shared, narrow.AxisFor(ChartDimension.X));
        Assert.Same(shared, wide.AxisFor(ChartDimension.X));
    }

    private static (double Minimum, double Maximum) Range(CartesianChart chart, IAxis axis)
    {
        var range = chart.Transform.GetLayout(axis).UnitRange;

        return (range.Minimum, range.Maximum);
    }

    private static CartesianChart Chart(params SeriesBase[] series) => Chart(600, series);

    private static CartesianChart Chart(double width, params SeriesBase[] series)
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

        // The inks a theme would have set, so that an axis actually draws: a bare chart has none, which is right
        // for a chart that has not been dressed and useless for a test about what is on screen.
        chart.LabelBrush = Brushes.Black;
        chart.AxisLinePen = new Pen(Brushes.Gray, 1);
        chart.TickPen = new Pen(Brushes.Gray, 1);

        chart.Measure(new Size(width, 400));
        chart.Arrange(new Rect(0, 0, width, 400));
        return chart;
    }

    private static LineSeries<double> Line(double[] values) =>
        new() { ItemsSource = values, XSelector = value => value, YSelector = value => value };
}
