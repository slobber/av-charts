using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the shapes a series can be drawn as beyond a plain line: marks on their own, a line that steps, a
/// region filled under the line, and marks whose size is a third value.
/// </summary>
/// <remarks>
/// The evidence is the ink, as it is for the columns: one series painted on its own into a bitmap, and the
/// pixels in a chosen part of it counted. A shape is a shape even when nothing in the model describes it.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class ShapeSeriesTests
{
    [Fact]
    public void A_plot_draws_marks_and_nothing_between_them()
    {
        var plot = new PlotSeries<double>
        {
            ItemsSource = [1.0, 5.0, 9.0],
            XSelector = value => value,
            YSelector = value => value,
            Marker = Markers.Circle,
            MarkerSize = 4,
            MarkerFill = Brushes.Red,
        };

        var line = new LineSeries<double>
        {
            ItemsSource = [1.0, 5.0, 9.0],
            XSelector = value => value,
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        };

        var chart = Chart(plot, line);

        var marks = InkCount(chart, plot);
        var joined = InkCount(chart, line);

        Assert.True(marks > 0, "the marks should have been drawn");
        Assert.True(joined > marks, $"a line between the points covers more than marks alone ({joined} against {marks})");

        // Between the first two points there is nothing at all: a plot is not a line with the ink turned down.
        var between = Between(chart, 1, 5);

        var bitmap = InkBitmap(chart, plot);

        Assert.Equal(0, Ink.Count(bitmap, between));
    }

    [Fact]
    public void A_step_line_goes_across_before_it_goes_up()
    {
        var step = new StepLineSeries<double>
        {
            ItemsSource = [1.0, 5.0],
            XSelector = value => value,
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        };

        var chart = Chart(step);
        var corner = new Rect(Pixel(chart, 5, 1).X - 3, Pixel(chart, 5, 1).Y - 3, 6, 6);

        // The corner above the second point: a stepped line passes through it, a straight one does not.
        Assert.True(Ink.Count(InkBitmap(chart, step), corner) > 0, "the step should corner at the second point");
    }

    [Fact]
    public void A_line_does_not_pass_through_the_corner_a_step_does()
    {
        var line = new LineSeries<double>
        {
            ItemsSource = [1.0, 5.0],
            XSelector = value => value,
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        };

        var chart = Chart(line);
        var corner = new Rect(Pixel(chart, 5, 1).X - 3, Pixel(chart, 5, 1).Y - 3, 6, 6);

        Assert.Equal(0, Ink.Count(InkBitmap(chart, line), corner));
    }

    [Fact]
    public void An_area_fills_under_the_line_and_stops_at_a_gap()
    {
        var values = new double[] { 2, 4, 6, double.NaN, 6, 4, 2 };
        var area = new AreaSeries<double>
        {
            ItemsSource = values,
            XSelector = value => value is double.NaN ? 3 : Array.IndexOf(values, value),
            YSelector = value => value,
            Fill = Brushes.Red,
        };

        // The horizontal values are the positions, so the gap is the middle one of seven.
        area.XSelector = null;

        var chart = Chart(area);
        var bitmap = InkBitmap(chart, area);
        var plot = chart.PlotBounds;

        // The region is filled: the middle of the plot, under the line, has ink all the way down to the floor.
        var under = new Rect(Pixel(chart, 1, 0).X - 2, plot.Bottom - 5, 4, 4);

        Assert.True(Ink.Count(bitmap, under) > 0, "the region should reach the floor of the plot");

        // And the gap is a gap: the column where the missing record is has nothing in it, because a value that
        // was never measured is not drawn as though it had been.
        var gap = new Rect(Pixel(chart, 3, 0).X - 1, plot.Top, 3, plot.Height);

        Assert.Equal(0, Ink.Count(bitmap, gap));
    }

    [Fact]
    public void A_bubble_is_a_plot_whose_marks_differ_from_one_another()
    {
        var bubble = new BubbleSeries<Bubble>
        {
            ItemsSource = [new Bubble(1, 1, 2), new Bubble(9, 1, 12)],
            XSelector = item => item.X,
            YSelector = item => item.Y,
            RadiusSelector = item => item.Radius,
            Marker = Markers.Circle,
            MarkerFill = Brushes.Red,
        };

        var chart = Chart(bubble);
        var bitmap = InkBitmap(chart, bubble);
        var small = Pixel(chart, 1, 1);
        var large = Pixel(chart, 9, 1);

        var smallInk = Ink.Count(bitmap, new Rect(small.X - 14, small.Y - 14, 28, 28));
        var largeInk = Ink.Count(bitmap, new Rect(large.X - 14, large.Y - 14, 28, 28));

        // The big mark is a disc of radius twelve and the small one of radius two, so the ink in the same square
        // of the picture is many times what the small one covers.
        Assert.True(largeInk > smallInk * 6, $"expected a much bigger mark ({largeInk} against {smallInk})");
    }

    [Fact]
    public void A_plot_draws_a_circle_at_every_reading_without_being_asked()
    {
        // A plot is marks, so it draws them: this is the default a reader gets, and the reason a bubble series
        // stopped being a page of empty space. The evidence is the ink of the same series with and without being
        // told, which is the amount of difference "by default" makes.
        var quiet = new PlotSeries<double>
        {
            ItemsSource = [1.0, 5.0, 9.0],
            XSelector = value => value,
            YSelector = value => value,
            MarkerFill = Brushes.Red,
            MarkerSize = 4,
        };

        var asked = new PlotSeries<double>
        {
            ItemsSource = [1.0, 5.0, 9.0],
            XSelector = value => value,
            YSelector = value => value,
            Marker = Markers.Circle,
            MarkerFill = Brushes.Red,
            MarkerSize = 4,
        };

        Assert.Same(Markers.Circle, quiet.Marker);

        var drew = InkCount(Chart(quiet), quiet);

        Assert.True(drew > 0, "a plot drew nothing at all");
        Assert.Equal(InkCount(Chart(asked), asked), drew);
    }

    [Fact]
    public void A_line_draws_no_marks_until_it_is_asked_for_them()
    {
        // A line is the shape it draws, so it brings no marks: a hundred thousand readings want no dot on each of
        // them. What is asked for is one setting away, and it lands on the readings rather than along the line.
        var readings = new[] { 5.0, 5.0, 5.0 };

        // No horizontal selector, so the readings stand at their places in the list: nought, one and two, with
        // the middle one at one - which is where the square above the line goes looking for a mark.
        var bare = new LineSeries<double>
        {
            ItemsSource = readings,
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        };

        var dotted = new LineSeries<double>
        {
            ItemsSource = readings,
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            Marker = Markers.Circle,
            MarkerFill = Brushes.Red,
            MarkerSize = 8,
        };

        Assert.Null(bare.Marker);

        var plain = Chart(bare);
        var marked = Chart(dotted);

        // A square five pixels above the line at the first reading: a mark of radius eight reaches into it and a
        // line two pixels thick does not.
        static Rect Above(Point at) => new(at.X - 2, at.Y - 7, 4, 4);

        Assert.Equal(0, Ink.Count(InkBitmap(plain, bare), Above(Pixel(plain, 1, 5))));
        Assert.True(Ink.Count(InkBitmap(marked, dotted), Above(Pixel(marked, 1, 5))) > 0, "the mark asked for was not drawn");
        Assert.True(InkCount(marked, dotted) > InkCount(plain, bare));
    }

    [Fact]
    public void A_mark_is_filled_with_a_tint_and_outlined_in_the_colour_it_is_a_tint_of()
    {
        // What a mark looks like when the chart dressed it: the series' colour held back to a tint inside, and the
        // colour itself around it - which is what makes a mark read as a shape rather than a smudge of colour.
        var themed = new ThemedChart();
        var plot = new PlotSeries<double>
        {
            ItemsSource = [5.0],
            XSelector = value => 5,
            YSelector = value => value,
            MarkerSize = 12,
        };

        themed.Chart.Series.Add(plot);
        themed.Layout();

        var bitmap = InkOverWhite(themed.Chart, plot);
        var stroke = ((ISolidColorBrush)((Pen)themed.Value(ChartTokens.SeriesStroke(1), ThemeVariant.Light)).Brush!).Color;
        var tint = ((ISolidColorBrush)themed.Value(ChartTokens.SeriesFill(1), ThemeVariant.Light)).Color;

        var rim = Ink.Count(bitmap, stroke, 2);
        var inside = Ink.Count(bitmap, OverWhite(tint), 2);

        Assert.True(rim > 0, "the mark has no outline in the colour it is drawn in");
        Assert.True(inside > 0, "the inside of the mark is not that colour held back");
        Assert.True(inside > rim * 2, $"an outline that is not thinner than the fill it goes round: {rim} against {inside}");
    }

    [Fact]
    public void A_line_and_an_area_carry_no_marks_and_a_bubble_carries_circles()
    {
        // The default belongs to the series whose shape is its marks. The family built on a line opts out, and a
        // bubble is drawn as circles unless it is told otherwise, which is the one shape that says "a size".
        Assert.Null(new LineSeries<double>().Marker);
        Assert.Null(new AreaSeries<double>().Marker);
        Assert.Null(new StepLineSeries<double>().Marker);
        Assert.Null(new StepAreaSeries<double>().Marker);
        Assert.Same(Markers.Circle, new BubbleSeries<double>().Marker);
    }

    [Fact]
    public void A_series_that_fills_is_coloured_by_its_own_fill()
    {
        var area = new AreaSeries<double> { Fill = Brushes.Red };
        var plot = new PlotSeries<double>();

        // The palette rule: a series that brought a colour takes no slot, and the colour it reports is the one it
        // is drawn in.
        Assert.True(area.HasOwnColours);
        Assert.Same(Brushes.Red, area.Color);
        Assert.False(plot.HasOwnColours);
    }

    [Fact]
    public void Every_built_in_marker_draws_a_shape_of_its_own()
    {
        var shapes = new[] { Markers.Circle, Markers.Square, Markers.Diamond, Markers.Cross, Markers.Triangle };

        for (var index = 0; index < shapes.Length; index++)
        {
            var (chart, plot) = OneMark(shapes[index]);

            Assert.True(InkCount(chart, plot) > 0, "a marker the library ships drew nothing at all");

            for (var other = index + 1; other < shapes.Length; other++)
            {
                Assert.NotSame(shapes[index], shapes[other]);
            }
        }
    }

    [Fact]
    public void A_cross_has_empty_corners_where_a_square_has_ink()
    {
        // The two read differently on purpose: a square is a block and a cross is a target, so a corner of the
        // square the shape is drawn in is inside the cross' arm only if the arm reaches it.
        var (crossChart, cross) = OneMark(Markers.Cross);
        var (squareChart, square) = OneMark(Markers.Square);

        var centre = Pixel(crossChart, 5, 5);
        var corner = new Rect(centre.X - 12, centre.Y - 12, 5, 5);

        Assert.Equal(0, Ink.Count(InkBitmap(crossChart, cross), corner));
        Assert.True(Ink.Count(InkBitmap(squareChart, square), corner) > 0, "a square should fill its corner");
    }

    [Fact]
    public void A_triangle_stands_on_its_base()
    {
        var (chart, triangle) = OneMark(Markers.Triangle);
        var bitmap = InkBitmap(chart, triangle);
        var centre = Pixel(chart, 5, 5);

        var topLeft = new Rect(centre.X - 12, centre.Y - 12, 7, 5);
        var topMiddle = new Rect(centre.X - 2, centre.Y - 12, 4, 5);
        var bottomLeft = new Rect(centre.X - 12, centre.Y + 7, 7, 5);

        Assert.Equal(0, Ink.Count(bitmap, topLeft));
        Assert.True(Ink.Count(bitmap, topMiddle) > 0, "a triangle points at the reading from above it");
        Assert.True(Ink.Count(bitmap, bottomLeft) > 0, "a triangle stands on a base");
    }

    /// <summary>One reading in the middle of a chart, drawn as one marker of the given shape.</summary>
    /// <param name="marker">The shape.</param>
    /// <returns>The chart, and the series the mark belongs to.</returns>
    private static (CartesianChart Chart, PlotSeries<double> Plot) OneMark(IMarkerRenderer marker)
    {
        var plot = new PlotSeries<double>
        {
            ItemsSource = [5.0],
            XSelector = value => value,
            YSelector = value => value,
            Marker = marker,
            MarkerSize = 12,
            MarkerFill = Brushes.Red,
            MarkerStroke = new Pen(Brushes.Red),
        };

        return (Chart(plot), plot);
    }

    private static CartesianChart Chart(params Series[] series)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10 },
        };

        foreach (var one in series)
        {
            chart.Series.Add(one);
        }

        chart.Measure(new Size(600, 400));
        chart.Arrange(new Rect(0, 0, 600, 400));
        return chart;
    }

    /// <summary>The rectangle between two horizontal values, over the whole height of the plot.</summary>
    private static Rect Between(CartesianChart chart, double from, double to)
    {
        var plot = chart.PlotBounds;
        var left = Pixel(chart, from, 0).X;
        var right = Pixel(chart, to, 0).X;

        // Six pixels clear of each end, which is more than the reach of a mark.
        return new Rect(left + 6, plot.Top, Math.Max(0, right - left - 12), plot.Height);
    }

    private static Point Pixel(CartesianChart chart, double x, double y)
    {
        var plot = chart.PlotBounds;

        return new Point(plot.X + (x / 10 * plot.Width), plot.Bottom - (y / 10 * plot.Height));
    }

    private static int InkCount(CartesianChart chart, Series series) => Ink.Count(InkBitmap(chart, series));

    private static RenderTargetBitmap InkBitmap(CartesianChart chart, Series series)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(600, 400));

        using (var context = bitmap.CreateDrawingContext())
        {
            series.Paint(
                context,
                new SeriesRenderContext(chart.Transform, chart.PlotBounds, 1, chart.SeriesLayer.Bands));
        }

        return bitmap;
    }

    /// <summary>
    /// Draws a series over white, so that what a translucent fill comes to can be read off the picture.
    /// </summary>
    /// <param name="chart">The chart the series belongs to.</param>
    /// <param name="series">The series.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap InkOverWhite(CartesianChart chart, Series series)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(600, 400));

        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, 600, 400));
            series.Paint(
                context,
                new SeriesRenderContext(chart.Transform, chart.PlotBounds, 1, chart.SeriesLayer.Bands));
        }

        return bitmap;
    }

    /// <summary>What a colour that is not fully opaque comes to when it is drawn on white.</summary>
    /// <param name="colour">The colour.</param>
    /// <returns>The colour it reads as.</returns>
    private static Color OverWhite(Color colour)
    {
        var alpha = colour.A / 255.0;

        return Color.FromRgb(
            (byte)Math.Round((colour.R * alpha) + (255 * (1 - alpha))),
            (byte)Math.Round((colour.G * alpha) + (255 * (1 - alpha))),
            (byte)Math.Round((colour.B * alpha) + (255 * (1 - alpha))));
    }

    /// <summary>Something with a place and a size of its own, which is what a bubble needs.</summary>
    private sealed record Bubble(double X, double Y, double Radius);
}
