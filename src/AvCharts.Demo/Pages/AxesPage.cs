using Avalonia.Controls;
using Avalonia.Media;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
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
            Try("Try it: one scale, fitted or held, with the grid you ask for", AxisPlayground()),
            Try("Try it: more names than fit, turned or stacked", LabelPlayground()));
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

        var titles = new[] { "none", "named", "named, read the other way" };

        void Titled(string picked)
        {
            var index = Array.IndexOf(titles, picked);

            across.Title = index > 0 ? "Readings" : null;
            up.Title = index > 0 ? "Values" : null;
            chart.VerticalRenderer.TitleDirection =
                index == 2 ? AxisTitleDirection.TopToBottom : AxisTitleDirection.BottomToTop;
        }

        // The page opens with the titles on: a knob whose subject is invisible until it is turned is a knob a reader
        // has to guess the point of.
        Titled(titles[1]);

        return Playground(
            chart,
            Choice("Across the chart", scales, scales[0], Scaled),
            Choices("Grid interval", ["auto", "1", "2", "5"], 0, picked => across.Interval = intervals[picked]),
            OnOff("Vertical labels", true, on => chart.VerticalRenderer.ShowLabels = on),
            Choice("Axis titles", titles, titles[1], Titled));
    }

    /// <summary>
    /// The other half of an axis: what its labels do when there are more of them than the plot can hold upright.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// <para>
    /// Twelve names across a category axis are more than a row of upright labels can hold, so the axis would let
    /// most of them go. The two knobs are the two answers a host has - turn the labels, or stand them in two rows -
    /// and both of them buy labels back, which is a thing a reader can count off the picture for themselves.
    /// </para>
    /// <para>
    /// It is shorter than the other playgrounds because it is a page about labels rather than about a plot: the
    /// room the labels take is what is being looked at here, and a shorter frame is the room where turning them
    /// matters most.
    /// </para>
    /// </remarks>
    private static Control LabelPlayground()
    {
        var chart = new CartesianChart { HorizontalAxis = new CategoryAxis(), VerticalAxis = new LinearAxis() };

        chart.Series.Add(new ColumnSeries<Reading>
        {
            DisplayName = "Headcount",
            ItemsSource =
            [
                new("Engineering", 42),
                new("Manufacturing", 31),
                new("Distribution", 18),
                new("Research", 12),
                new("Marketing", 9),
                new("Sales", 26),
                new("Support", 15),
                new("Finance", 7),
                new("Legal", 4),
                new("Facilities", 6),
                new("Quality", 11),
                new("Logistics", 14),
            ],
            XSelector = reading => reading.Name,
            YSelector = reading => reading.Value,
        });

        var tilts = new[] { "upright", "turned 45°", "turned 90°" };
        var rows = new[] { "one row", "two rows" };

        return Playground(
            chart,
            LabelPlaygroundHeight,
            Choice(
                "Label rotation",
                tilts,
                tilts[0],
                picked => chart.HorizontalRenderer.LabelRotation = 45 * Array.IndexOf(tilts, picked)),
            Choice(
                "Label rows",
                rows,
                rows[0],
                picked => chart.HorizontalRenderer.StaggerLabels = picked == rows[1]));
    }

    /// <summary>How tall the playground about labels is: shorter than the others, since it is about the names.</summary>
    private const double LabelPlaygroundHeight = 200;
}
