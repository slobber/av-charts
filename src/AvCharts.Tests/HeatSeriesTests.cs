using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the heat map: a value at a pair of positions becomes a filled cell, its colour follows the scale,
/// and a pointer over the cell finds the record behind it.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class HeatSeriesTests
{
    [Fact]
    public void Values_become_cells_coloured_by_the_scale()
    {
        var cells = new[]
        {
            new Cell("A", "low", 0),
            new Cell("B", "low", 100),
            new Cell("A", "high", 50),
            new Cell("B", "high", 25),
        };

        var series = new HeatSeries<Cell>
        {
            ItemsSource = cells,
            XSelector = cell => cell.Column,
            YSelector = cell => cell.Row,
            ValueSelector = cell => cell.Value,
            ColorScale = new HeatColorScale(Colors.Blue, Colors.Red),
        };

        var chart = Chart(series);
        var picture = Picture(chart, series);
        var plot = chart.PlotBounds;
        var column = plot.Width / 2;
        var row = plot.Height / 2;

        Assert.Equal(4, series.GetItemsInRegion(plot).Count);
        Assert.True(Ink.Count(picture, Colors.Blue, new Rect(plot.Left, plot.Top + row, column, row)) > 100);
        Assert.True(Ink.Count(picture, Colors.Red, new Rect(plot.Left + column, plot.Top + row, column, row)) > 100);

        var hit = series.HitTestItem(
            new Point(plot.Left + (column / 2), plot.Top + row + (row / 2)),
            1);

        Assert.NotNull(hit);
        Assert.Equal(0, hit!.Value.ChartItem.Index);
        Assert.Equal("0", hit.Value.DisplayText);
    }

    [Fact]
    public void Missing_values_leave_their_cell_unpainted_and_unhittable()
    {
        var cells = new[]
        {
            new Cell("A", "low", double.NaN),
            new Cell("B", "low", 5),
            new Cell("A", "high", 6),
            new Cell("B", "high", 7),
        };

        var series = new HeatSeries<Cell>
        {
            ItemsSource = cells,
            XSelector = cell => cell.Column,
            YSelector = cell => cell.Row,
            ValueSelector = cell => cell.Value,
        };

        var chart = Chart(series);
        var picture = Picture(chart, series);
        var plot = chart.PlotBounds;
        var column = plot.Width / 2;
        var row = plot.Height / 2;

        Assert.Equal(3, series.GetItemsInRegion(plot).Count);
        Assert.Null(series.HitTestItem(new Point(plot.Left + (column / 2), plot.Top + row + (row / 2)), 1));

        // The three painted cells still tile the whole plot: the range comes from the positions, not from which
        // records happened to have a value.
        var ink = Ink.Bounds(picture);
        Assert.InRange(ink.Left, (int)plot.Left - 1, (int)plot.Left + 1);
        Assert.InRange(ink.Top, (int)plot.Top - 1, (int)plot.Top + 1);
        Assert.InRange(ink.Right, (int)plot.Right - 1, (int)plot.Right + 1);
        Assert.InRange(ink.Bottom, (int)plot.Bottom - 1, (int)plot.Bottom + 1);
    }

    private static CartesianChart Chart(SeriesBase series)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new CategoryAxis(),
        };

        chart.Series.Add(series);
        chart.Measure(new Size(400, 200));
        chart.Arrange(new Rect(0, 0, 400, 200));
        return chart;
    }

    private static RenderTargetBitmap Picture(CartesianChart chart, SeriesBase series)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 200));

        using (var context = bitmap.CreateDrawingContext())
        {
            series.Paint(
                context,
                new SeriesRenderContext(chart.Transform, chart.PlotBounds, 1, chart.SeriesLayer.Bands));
        }

        return bitmap;
    }

    private sealed record Cell(string Column, string Row, double Value);
}
