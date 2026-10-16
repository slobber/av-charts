using Avalonia.Controls;

using AvCharts.Series;




using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The Points page: a scatter of readings, and a bubble sized by a third reading - and the playground beside them.
internal static class PointsPage
{
    /// <summary>A scatter of readings, with a bubble whose size is a third reading.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var chart = Chart(0, 10, 0, 10);
        var readings = Scatter();

        chart.Series.Add(new PlotSeries<Reading>
        {
            DisplayName = "Readings",
            ItemsSource = readings,
            XSelector = reading => reading.X,
            YSelector = reading => reading.Value,
            Marker = Markers.Diamond,
            MarkerSize = 4,
        });

        chart.Series.Add(new BubbleSeries<Reading>
        {
            DisplayName = "Bubbles",
            ItemsSource = readings,
            // A little to the right of the marks, so that a bubble is not hidden inside the diamond it is about.
            XSelector = reading => reading.X + 0.25,
            YSelector = reading => reading.Value,
            // No marker is named here: a bubble is drawn as a circle out of the box, filled with the series'
            // colour held back to a tint and outlined in the colour itself.
            RadiusSelector = reading => reading.Size,
        });

        return Page(
            "Points",
            "Readings as marks, and the same readings as bubbles.",
            Sourced("Bubbles sized by a third reading", "Points/BubblesSizedByAThirdReading"),
            Try("Try it: the marks and the bubbles, drawn the way you ask", PointPlayground()));
    }

    /// <summary>
    /// The scatter with knobs on: the shape of a mark, how big it is, and what a bubble's third reading means.
    /// </summary>
    /// <returns>The playground.</returns>
    private static Control PointPlayground()
    {
        var chart = Chart(0, 10, 0, 10);
        var readings = Scatter();

        var marks = new PlotSeries<Reading>
        {
            DisplayName = "Readings",
            ItemsSource = readings,
            XSelector = reading => reading.X,
            YSelector = reading => reading.Value,
            Marker = Markers.Diamond,
            MarkerSize = 4,
        };

        var bubbles = new BubbleSeries<Reading>
        {
            DisplayName = "Bubbles",
            ItemsSource = readings,
            // A little to the right of the marks, so that a bubble is not hidden inside the diamond it is about.
            XSelector = reading => reading.X + 0.25,
            YSelector = reading => reading.Value,
            // No marker is named here either: the circle and its outline are what a bubble series draws by
            // itself. The page's Marker knob is about the marks of the scatter beside them.
            RadiusSelector = reading => reading.Size,
        };

        chart.Series.Add(marks);
        chart.Series.Add(bubbles);

        var shapes = new[] { Markers.Circle, Markers.Square, Markers.Diamond, Markers.Cross, Markers.Triangle };
        var names = new[] { "circle", "square", "diamond", "cross", "triangle" };
        var sizes = new[] { 2.0, 4.0, 7.0 };
        var scales = new[] { 0.5, 1.0, 2.0 };

        return Playground(
            chart,
            // Named rather than listed as they are: a marker is an object, and what a picker would show of one is
            // the name of the private class behind it ("AvCharts.Series.Markers+Diamond"), which is no name at all.
            Choice("Marker", names, "diamond", picked => marks.Marker = shapes[Array.IndexOf(names, picked)]),
            Choice("Marker size", sizes, 4.0, picked => marks.MarkerSize = picked),
            Choice("Bubble size", scales, 1.0, picked => bubbles.RadiusSelector = reading => reading.Size * picked),
            OnOff("With bubbles", true, on => bubbles.Visible = on));
    }
}
