using Avalonia.Controls;
using Avalonia.Media;
using AvCharts.Charts;
using AvCharts.Controls;
using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The Axes page: one small chart per kind of scale, and the playground that fits a scale or holds it still.
internal static class AxesPage
{
    /// <summary>One small chart per kind of axis.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var page = new Grid
        {
            // Three across and two down: five scales, and a grid that fits on a page rather than running off it.
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("*,*"),
        };

        var week = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        var linear = new CartesianChart { HorizontalAxis = new LinearAxis(), VerticalAxis = new LinearAxis() };
        linear.Series.Add(Line("Linear", [3.0, 1.0, 4.0, 1.5, 5.0]));

        var logarithm = new CartesianChart { HorizontalAxis = new LogAxis(), VerticalAxis = new LinearAxis() };
        logarithm.Series.Add(Line("Logarithmic", [1.0, 10.0, 100.0, 1000.0, 10000.0]));

        var time = new CartesianChart { HorizontalAxis = new DateTimeAxis(), VerticalAxis = new LinearAxis() };
        time.Series.Add(new LineSeries<double>
        {
            DisplayName = "Time",
            ItemsSource = [.. Enumerable.Range(0, 9).Select(index => 4 + (3 * Math.Sin(index)))],
            XSelector = value => week.AddDays(value),
            YSelector = value => value,
            StrokeThickness = 2,
        });

        var duration = new CartesianChart { HorizontalAxis = new TimeSpanAxis(), VerticalAxis = new LinearAxis() };
        duration.Series.Add(new LineSeries<double>
        {
            DisplayName = "Duration",
            ItemsSource = [1.0, 3.0, 2.0, 5.0, 4.0],
            XSelector = value => TimeSpan.FromMinutes(value * 45).TotalMilliseconds,
            YSelector = value => value,
            StrokeThickness = 2,
        });

        var categories = new CartesianChart { HorizontalAxis = new CategoryAxis(), VerticalAxis = new LinearAxis() };
        categories.Series.Add(new ColumnSeries<Reading>
        {
            DisplayName = "Categorical",
            ItemsSource = [new("Q1", 4), new("Q2", 7), new("Q3", 3), new("Q4", 6)],
            XSelector = reading => reading.Name,
            YSelector = reading => reading.Value,
            Fill = new SolidColorBrush(Color.FromRgb(0x46, 0x82, 0xB4)),
        });

        page.Children.Add(Cell(linear, 0, 0, "Linear"));
        page.Children.Add(Cell(logarithm, 1, 0, "Logarithmic"));
        page.Children.Add(Cell(time, 2, 0, "Time"));
        page.Children.Add(Cell(duration, 0, 1, "Duration"));
        page.Children.Add(Cell(categories, 1, 1, "Category"));
        return Page(
            "Axes",
            "The five scales a Cartesian chart can be drawn against.",
            Example("Linear, logarithmic, time, duration and category", page),
            Try("Try it: one scale, fitted or held, with the grid you ask for", AxisPlayground()));
    }

    /// <summary>
    /// An ordinary chart with knobs on the two things its axes decide: whether the scale follows the data or is
    /// held, and how far apart the grid lines fall.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// The two scales are the reason this one is worth dragging: fitted, the same readings stretch to whatever the
    /// frame is; held, one unit keeps its size and a wider frame shows more readings rather than wider spacing.
    /// A reader who drags the corner with "fixed" on has seen what a strip chart is for.
    /// </remarks>
    private static Control AxisPlayground()
    {
        var across = new LinearAxis { Minimum = 0, Maximum = 7 };
        var up = new LinearAxis { Minimum = 0, Maximum = 10 };
        var chart = new CartesianChart { HorizontalAxis = across, VerticalAxis = up };

        chart.Series.Add(Line("Readings", [3.0, 7.0, 4.5, 9.0, 6.0, 8.5, 5.0, 7.5]));

        var intervals = new double?[] { null, 1, 2, 5 };
        var scales = new[] { "fit the readings", "fixed, 40px a unit" };

        void Scaled(string picked)
        {
            across.ScaleMode = picked == scales[0] ? AxisScaleMode.Fit : AxisScaleMode.Fixed;
            across.Minimum = 0;
            across.PixelsPerUnit = 40;
        }

        return Playground(
            chart,
            Choice("Across the chart", scales, scales[0], Scaled),
            Choices("Grid interval", ["auto", "1", "2", "5"], 0, picked => across.Interval = intervals[picked]),
            OnOff("Vertical labels", true, on => chart.VerticalRenderer.ShowLabels = on));
    }
}
