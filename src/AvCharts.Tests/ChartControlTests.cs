using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

[Collection(AvaloniaCollection.Name)]
public class ChartControlTests
{
    private static CartesianChart Chart(bool showLabels = true, double width = 200, double height = 100)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
        };

        chart.HorizontalRenderer.ShowLabels = showLabels;
        chart.VerticalRenderer.ShowLabels = showLabels;

        chart.Measure(new Size(width, height));
        chart.Arrange(new Rect(0, 0, width, height));

        return chart;
    }

    [Fact]
    public void The_plot_leaves_room_for_the_axis_labels()
    {
        var chart = Chart();

        Assert.True(chart.PlotBounds.Left > 0, "the vertical axis' labels need room on the left");
        Assert.True(chart.PlotBounds.Bottom < 100, "the horizontal axis' labels need room at the bottom");
        Assert.True(chart.PlotBounds.Width > 0);
        Assert.True(chart.PlotBounds.Height > 0);
    }

    [Fact]
    public void An_axis_without_labels_only_leaves_room_for_its_marks()
    {
        var chart = Chart(showLabels: false);

        Assert.Equal(chart.VerticalRenderer.TickLength, chart.PlotBounds.Left, 6);
        Assert.Equal(100 - chart.HorizontalRenderer.TickLength, chart.PlotBounds.Bottom, 6);
    }

    [Fact]
    public void A_chart_with_no_data_still_lays_out()
    {
        var chart = new CartesianChart();
        chart.Measure(new Size(200, 100));
        chart.Arrange(new Rect(0, 0, 200, 100));

        Assert.True(chart.PlotBounds.Width > 0);
        Assert.True(chart.PlotBounds.Height > 0);
    }

    [Fact]
    public void Arranging_at_another_size_moves_the_plot()
    {
        var chart = Chart();
        var first = chart.PlotBounds;

        chart.Measure(new Size(400, 300));
        chart.Arrange(new Rect(0, 0, 400, 300));

        Assert.NotEqual(first, chart.PlotBounds);
        Assert.True(chart.PlotBounds.Width > first.Width);
    }

    [Fact]
    public void Adding_a_series_wires_it_to_the_transform_and_removing_it_lets_it_go()
    {
        var chart = Chart();
        var series = new LineSeries<double>();

        chart.Series.Add(series);

        Assert.Same(chart.Transform, series.Transform);
        Assert.Contains(series, chart.Transform.Elements);
        Assert.Contains(series, chart.SeriesLayer.Series);

        chart.Series.Remove(series);

        Assert.Null(series.Transform);
        Assert.DoesNotContain(series, chart.Transform.Elements);
        Assert.DoesNotContain(series, chart.SeriesLayer.Series);
    }

    [Fact]
    public void The_series_layer_paints_the_series_it_holds()
    {
        var chart = Chart();
        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = [0.0, 5.0, 10.0],
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        });

        chart.Measure(new Size(200, 100));
        chart.Arrange(new Rect(0, 0, 200, 100));

        var bitmap = new RenderTargetBitmap(new PixelSize(200, 100));
        using (var context = bitmap.CreateDrawingContext())
        {
            chart.SeriesLayer.Render(context);
        }

        Assert.True(Ink.Count(bitmap) > 0, "the series layer should have painted the line");
    }

    [Fact]
    public void The_axis_layer_paints_the_grid_the_line_the_marks_and_the_labels()
    {
        var chart = Chart();
        chart.HorizontalRenderer.ShowLabels = true;
        chart.HorizontalRenderer.TickLength = 4;

        var layer = new AxisLayer(chart.HorizontalRenderer) { Transform = chart.Transform };
        layer.Configure(chart.PlotBounds, 1);
        layer.Arrange(new Rect(0, 0, 200, 100));
        layer.GridLinePen = new Pen(Brushes.LightGray, 1);
        layer.AxisLinePen = new Pen(Brushes.Gray, 1);
        layer.LabelBrush = Brushes.Black;

        var bitmap = new RenderTargetBitmap(new PixelSize(200, 100));
        using (var context = bitmap.CreateDrawingContext())
        {
            layer.Render(context);
        }

        Assert.True(Ink.Count(bitmap) > 0, "the axis layer should have painted grid, ticks and labels");
    }
}
