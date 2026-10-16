using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Drawing;
using AvCharts.Series;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the crosshair: the line each way through the point it is on, the reading written beside each axis,
/// and the rule that it stands on the nearest valid point rather than wherever the pointer happens to be.
/// </summary>
/// <remarks>
/// The pointer handlers are thin shells over <c>MoveCrosshair</c> and <c>HideCrosshair</c>, which is what these
/// tests drive - the arrangement the gesture tests use, and for the same reason: synthesised pointer input does
/// not reach a window in this setup, so routing is the one part of this a test cannot speak for. Everything
/// above the routing is here, including what lands on the pixels.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class CrosshairTests
{
    private const int Width = 400;
    private const int Height = 200;

    /// <summary>A reading that says which value was written where it is drawn.</summary>
    private static readonly Color Chip = Colors.Magenta;

    private static readonly Color Line = Colors.Blue;

    private static readonly DateTimeOffset Epoch = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_crosshair_with_nothing_to_point_at_draws_nothing()
    {
        // A chart with no records, or a pointer that found none: the layer is dressed and laid out, and it still
        // draws none of the cross - there is no reading to point at.
        var layer = new CrosshairLayer
        {
            LinePen = new Pen(new SolidColorBrush(Line), 1),
            LabelBackground = new SolidColorBrush(Chip),
            LabelForeground = Brushes.Black,
        };

        layer.Configure(new Rect(20, 10, 200, 100), 1);

        var bitmap = Render(layer);

        Assert.Equal(0, Ink.Count(bitmap));
    }

    [Fact]
    public void The_lines_run_through_the_point_and_stop_at_the_plot()
    {
        var chart = Crosshair(out var series, across: false, up: false);
        var at = Position(series, 1);

        // Only the two lines: no readouts, so what is painted is exactly the cross itself.
        var layer = Layer(chart, lineOnly: true);

        var bitmap = Render(layer);
        var plot = chart.PlotBounds;
        var x = (int)Math.Round(at.X);
        var y = (int)Math.Round(at.Y);

        // Nothing in the four corners, which is the same as saying the ink is the two bands through the point.
        Assert.Equal(0, Ink.Count(bitmap, new Rect(0, 0, x - 3, y - 3)));
        Assert.Equal(0, Ink.Count(bitmap, new Rect(x + 4, 0, Width - x - 4, y - 3)));
        Assert.Equal(0, Ink.Count(bitmap, new Rect(0, y + 4, x - 3, Height - y - 4)));
        Assert.Equal(0, Ink.Count(bitmap, new Rect(x + 4, y + 4, Width - x - 4, Height - y - 4)));

        // And each line runs the whole way across the plot rather than stopping at the point.
        Assert.True(Ink.Count(bitmap, new Rect(x - 3, (int)plot.Top, 6, 3)) > 0, "the line should reach the top of the plot");
        Assert.True(Ink.Count(bitmap, new Rect(x - 3, (int)plot.Bottom - 3, 6, 3)) > 0, "and the bottom of it");
        Assert.True(Ink.Count(bitmap, new Rect((int)plot.Left, y - 3, 3, 6)) > 0, "the other line should reach the left edge");
        Assert.True(Ink.Count(bitmap, new Rect((int)plot.Right - 3, y - 3, 3, 6)) > 0, "and the right edge");
    }

    [Fact]
    public void Each_readout_is_written_beside_the_axis_it_belongs_to()
    {
        var chart = Crosshair(out _, across: false, up: false);

        var bitmap = Render(Layer(chart));
        var plot = chart.PlotBounds;

        // Both readings are short here - a couple of digits each - so both hang in the gutters: the horizontal one
        // below the plot, the vertical one to the left of it. Either would be useless written anywhere else.
        Assert.True(
            Ink.Count(bitmap, Chip, new Rect(0, plot.Bottom + 1, Width, Height - plot.Bottom - 1)) > 0,
            "the reading for the horizontal axis should be below the plot");
        Assert.True(
            Ink.Count(bitmap, Chip, new Rect(0, 0, plot.Left, Height)) > 0,
            "the reading for the vertical axis should be beside the plot");

        // Nothing is written above the plot, past its far edge, or over the plot itself.
        Assert.Equal(0, Ink.Count(bitmap, Chip, new Rect(0, 0, Width, plot.Top)));
        Assert.Equal(0, Ink.Count(bitmap, Chip, new Rect(plot.Right + 2, 0, Width - plot.Right - 2, Height)));
        Assert.Equal(0, Ink.Count(bitmap, Chip, new Rect(plot.Left + 1, plot.Top, plot.Width - 1, plot.Height)));
    }

    [Fact]
    public void A_readout_with_no_room_in_the_gutter_goes_inside_the_plot()
    {
        // The same chart as above, except that the series has a second vertical axis of its own writing dates -
        // and that axis is drawn on the far side, so the gutter beside the point is only wide enough for the
        // numbers of the chart's own axis. The reading goes inside the plot rather than off the edge of it or
        // over the axis labels.
        var chart = Crosshair(out var series, across: false, up: true);
        var at = Position(series, 1);
        var plot = chart.PlotBounds;

        var bitmap = Render(Layer(chart));
        var band = new Rect(0, at.Y - 20, Width, 40);

        Assert.True(
            Ink.Count(bitmap, Chip, new Rect(plot.Left + 1, band.Y, Width - plot.Left - 1, band.Height)) > 0,
            "the reading should have gone inside the plot");
        Assert.Equal(0, Ink.Count(bitmap, Chip, new Rect(0, 0, plot.Left, Height)));
    }

    [Fact]
    public void The_axes_write_the_readouts_not_the_chart()
    {
        // The same picture twice, with the long writing moved from one dimension to the other: dates along the
        // bottom, and - for the second chart - dates up a second vertical axis that only this series uses. The
        // wide chip follows the dates both times, which is what says the text comes from the axis the point
        // belongs to rather than from the chart or from the dimension.
        var across = Crosshair(out _, across: true, up: false);
        var acrossPicture = Render(Layer(across));

        var up = Crosshair(out _, across: false, up: true);
        var upPicture = Render(Layer(up));

        var datesAcross = ChipWidth(acrossPicture, across, horizontal: true);
        var numbersUp = ChipWidth(acrossPicture, across, horizontal: false);
        var numbersAcross = ChipWidth(upPicture, up, horizontal: true);
        var datesUp = ChipWidth(upPicture, up, horizontal: false);

        Assert.True(datesAcross > numbersUp * 2, $"dates across are {datesAcross}px and numbers up {numbersUp}px");
        Assert.True(datesUp > numbersAcross * 2, $"dates up are {datesUp}px and numbers across {numbersAcross}px");
    }

    [Fact]
    public void The_crosshair_is_off_by_default_and_the_pointer_does_not_put_it_up()
    {
        var chart = Crosshair(out var series, across: false, up: false, show: false);
        var at = Position(series, 1);

        Assert.False(chart.ShowCrosshair);

        chart.Hover(at);

        Assert.Null(chart.CrosshairHit);

        // Asked for, the same move has something to show - and turning it off again takes it away at once rather
        // than leaving the last position drawn.
        chart.ShowCrosshair = true;
        chart.Hover(at);

        Assert.NotNull(chart.CrosshairHit);

        chart.ShowCrosshair = false;

        Assert.Null(chart.CrosshairHit);
    }

    [Fact]
    public void The_crosshair_stands_on_the_nearest_valid_point_wherever_the_pointer_is()
    {
        // A series with a hole in it: there is no ink where the middle record would be, and the crosshair is not
        // allowed to stop there.
        var series = new LineSeries<double>
        {
            ItemsSource = [1.0, 2.0, 3.0],
            XSelector = value => value,
            YSelector = value => value == 2 ? ChartValue.Empty : value,
        };

        var chart = Chart(series);
        var where = Pixel(chart, 2, 1);

        // Nothing is within reach of the pointer - a gap is not a thing to point at - yet the crosshair still has
        // the nearest reading there is, because it is willing to travel and a pointer is not.
        Assert.Empty(chart.FindDataPoints(where));

        chart.Hover(where);

        var hit = chart.CrosshairHit;

        Assert.NotNull(hit);
        Assert.Equal(0, hit.Value.ChartItem.Index);
        Assert.Equal(1, hit.Value.Y.Number);

        // And a pointer in a corner, nowhere near a reading, still puts the crosshair on the nearest one.
        chart.Hover(new Point(chart.PlotBounds.X, chart.PlotBounds.Y));

        Assert.NotNull(chart.CrosshairHit);
        Assert.Equal(0, chart.CrosshairHit!.Value.ChartItem.Index);
    }

    [Fact]
    public void Leaving_takes_the_crosshair_away()
    {
        var chart = Crosshair(out var series, across: false, up: false);

        chart.Hover(Position(series, 1));
        Assert.NotNull(chart.CrosshairHit);

        // What the pointer-exited handler calls: the crosshair goes, and the layer is asked to repaint without it.
        chart.Leave();

        Assert.Null(chart.CrosshairHit);
        Assert.Equal(0, Ink.Count(Render(Layer(chart))));
    }

    [Fact]
    public void The_theme_dresses_the_crosshair()
    {
        var themed = new ThemedChart(chart: () => new ChartProbe
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
        });

        var chart = (ChartProbe)themed.Chart;
        chart.Series.Add(new LineSeries<double> { ItemsSource = [1.0, 2.0, 3.0], YSelector = value => value });
        themed.Layout();
        chart.ShowCrosshair = true;
        chart.Hover(Position(chart.Series[0], 1));

        // The control theme's inks land on the chart's own properties...
        var line = (Pen)themed.Value(ChartTokens.CrosshairLine, ThemeVariant.Light);

        Assert.Same(line, chart.CrosshairLinePen);

        // ...and the chart hands them to the layer that paints them, which is the half a control theme cannot
        // reach on its own.
        Assert.True(
            Ink.Count(Render(chart), ((ISolidColorBrush)line.Brush!).Color) > 0,
            "the crosshair should have been painted in the theme's ink");
    }

    /// <summary>The width of one of the two readout chips, measured from the pixels it was filled with.</summary>
    /// <param name="bitmap">The picture.</param>
    /// <param name="chart">The chart it was drawn from.</param>
    /// <param name="horizontal">Whether to measure the chip below the plot, rather than the one beside it.</param>
    /// <returns>The width in pixels.</returns>
    /// <remarks>
    /// A chip is measured in a band that only it can be in: below the plot for the horizontal reading, and level
    /// with the point for the vertical one. Where the chip landed - gutter or plot - is what other tests check.
    /// </remarks>
    private static int ChipWidth(RenderTargetBitmap bitmap, CartesianChart chart, bool horizontal)
    {
        var plot = chart.PlotBounds;
        var at = chart.CrosshairHit!.Value.Position!.Value;
        var band = horizontal
            ? new Rect(0, plot.Bottom + 1, Width, Height - plot.Bottom - 1)
            : new Rect(0, at.Y - 20, Width, 40);
        var (left, _, right, _) = Ink.Bounds(bitmap, Chip, band);

        return right < left ? 0 : right - left + 1;
    }

    /// <summary>The crosshair's layer, dressed and moved onto the chart's current hit.</summary>
    /// <param name="chart">The chart the hit came from.</param>
    /// <param name="lineOnly">Whether to leave the readouts off, so only the lines are painted.</param>
    /// <returns>The layer, laid out where the chart is.</returns>
    private static CrosshairLayer Layer(CartesianChart chart, bool lineOnly = false)
    {
        var layer = new CrosshairLayer
        {
            LinePen = new Pen(new SolidColorBrush(Line), 1),
            Hit = chart.CrosshairHit,
        };

        if (!lineOnly)
        {
            layer.LabelBackground = new SolidColorBrush(Chip);
            layer.LabelForeground = Brushes.Black;
        }

        layer.Configure(chart.PlotBounds, 1);
        return layer;
    }

    /// <summary>A chart with a crosshair standing on the middle record of a three-record series.</summary>
    /// <param name="series">The series the crosshair snapped to.</param>
    /// <param name="across">Whether the horizontal dimension carries dates rather than numbers.</param>
    /// <param name="up">Whether the series has a second vertical axis of its own, carrying dates.</param>
    /// <param name="show">Whether the crosshair is asked for at all.</param>
    /// <returns>The chart, laid out and hovered.</returns>
    private static ChartProbe Crosshair(
        out LineSeries<double> series,
        bool across,
        bool up,
        bool show = true)
    {
        series = new LineSeries<double>
        {
            ItemsSource = [1.0, 2.0, 3.0],
            XSelector = value => across ? Epoch.AddDays(value) : value,
            YSelector = value => up ? Epoch.AddDays(value) : value,
        };

        var chart = new ChartProbe
        {
            HorizontalAxis = across ? new DateTimeAxis() : new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            ShowCrosshair = show,
        };

        if (up)
        {
            series.VerticalAxis = new DateTimeAxis();
        }

        chart.Series.Add(series);
        chart.Measure(new Size(Width, Height));
        chart.Arrange(new Rect(0, 0, Width, Height));
        chart.Hover(Position(series, 1));

        return chart;
    }

    /// <summary>A chart whose crosshair is on, holding one series.</summary>
    /// <param name="series">The series to draw.</param>
    /// <returns>The chart, laid out.</returns>
    private static ChartProbe Chart(LineSeries<double> series)
    {
        var chart = new ChartProbe
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            ShowCrosshair = true,
        };

        chart.Series.Add(series);
        chart.Measure(new Size(Width, Height));
        chart.Arrange(new Rect(0, 0, Width, Height));
        return chart;
    }

    /// <summary>Where a record is drawn, which is where a pointer has to be to snap to it exactly.</summary>
    /// <param name="series">The series to look in.</param>
    /// <param name="index">The record's position in the data.</param>
    /// <returns>The position on the chart.</returns>
    private static Point Position(SeriesBase series, int index) => series.Items.Single(item => item.Index == index).Position!.Value;

    /// <summary>Where a pair of values is drawn on a chart whose axes both run from zero to four.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="x">The value on the horizontal dimension.</param>
    /// <param name="y">The value on the vertical dimension.</param>
    /// <returns>The position on the chart.</returns>
    private static Point Pixel(CartesianChart chart, double x, double y)
    {
        var plot = chart.PlotBounds;

        return new Point(plot.X + (x / 4 * plot.Width), plot.Bottom - (y / 4 * plot.Height));
    }

    /// <summary>Draws a visual the way the golden tests do: measured, arranged, painted onto a bitmap.</summary>
    /// <param name="visual">The chart or the layer to paint.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Render(Layoutable visual)
    {
        visual.Measure(new Size(Width, Height));
        visual.Arrange(new Rect(0, 0, Width, Height));

        var bitmap = new RenderTargetBitmap(new PixelSize(Width, Height), new Vector(96, 96));
        bitmap.Render(visual);

        return bitmap;
    }

    /// <summary>A chart that can be driven the way its pointer handlers drive it.</summary>
    private sealed class ChartProbe : CartesianChart
    {
        public void Hover(Point at) => MoveCrosshair(at);

        public void Leave() => HideCrosshair();
    }
}
