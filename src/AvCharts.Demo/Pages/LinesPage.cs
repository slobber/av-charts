using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvCharts.Charts;
using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The Lines page: a line, one with marks, an area under one, two step lines, and the line chart with knobs on.
internal static class LinesPage
{
    /// <summary>Lines and the shapes that follow them: area, step, step area.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        var values = new[] { 1.0, 3.5, 2.0, 6.0, 4.5, 8.5, 7.0, 9.5, 6.5, 9.0, 10.0 };
        var chart = Chart(0, 12, 0, 12);

        chart.Series.Add(Line("Line", values));
        chart.Series.Add(Line("With markers", Lower(values, 1.5), series => series.Marker = Markers.Circle));
        chart.Series.Add(Area("Area under it", Lower(values, 3)));
        chart.Series.Add(Steps("Steps", Lower(values, 4.5)));
        chart.Series.Add(Steps("Steps with an area", Lower(values, 6), filled: true));

        return Page(
            "Lines",
            "One line with its markers, one with an area under it, two step lines, and a line chart with knobs on.",
            Example("Five ways to draw the same readings", chart),
            Try("Try it: the same readings, drawn the way you ask", LinePlayground()),
            Sourced("A line with markers, written as markup", "Lines/LineWithMarkers"));
    }

    /// <summary>
    /// The line chart with knobs on: the curve, the ticks, the grid, the fill, the labels and the marks.
    /// </summary>
    /// <returns>The playground.</returns>
    private static Control LinePlayground()
    {
        var readings = new[] { 3_200.0, 2_700.0, 3_450.0, 3_600.0, 3_250.0 };
        var chart = Chart(0, 4, 0, 4_000);
        var line = Line("Readings", readings);
        var area = Area("Under it", readings);
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x77, 0x46, 0x82, 0xB4), 0),
                new GradientStop(Color.FromArgb(0x08, 0x46, 0x82, 0xB4), 1),
            },
        };

        area.Fill = gradient;
        line.Marker = Markers.Circle;
        line.MarkerSize = 4;
        chart.Series.Add(area);
        chart.Series.Add(line);

        // Every series in the playground follows the two knobs that are about the shape of a line: both the curve
        // and the area under it, so that the fill keeps up with the line it is filling under.
        // No repaint is asked for by hand here: an axis' renderer and a layer's grid say when they change, and the
        // chart lays itself out again and paints again. That is the difference between a library a host can tune
        // at runtime and one they have to know the inside of.

        void Curve(CurveMode mode)
        {
            line.Curve = mode;
            area.Curve = mode;
        }

        void Ticks(bool across, bool up)
        {
            chart.HorizontalRenderer.TickLength = across ? 4 : 0;
            chart.VerticalRenderer.TickLength = up ? 4 : 0;
        }

        void Grid(bool across, bool up)
        {
            chart.HorizontalLayer.ShowGridLines = across;
            chart.VerticalLayer.ShowGridLines = up;
        }

        void Labels(bool across, bool up)
        {
            chart.HorizontalRenderer.ShowLabels = across;
            chart.VerticalRenderer.ShowLabels = up;
        }

        var corners = new[] { (true, true), (true, false), (false, true), (false, false) };
        var names = new[] { "x and y", "x only", "y only", "none" };

        return Playground(
            chart,
            Choice("Curve type", [CurveMode.Linear, CurveMode.Smooth], CurveMode.Linear, Curve),
            Choices("Tick line", names, 0, picked => Ticks(corners[picked].Item1, corners[picked].Item2)),
            Choices("Grid axis", names, 0, picked => Grid(corners[picked].Item1, corners[picked].Item2)),
            OnOff("With gradient", true, on => area.Fill = on ? gradient : new SolidColorBrush(Color.FromArgb(0x33, 0x46, 0x82, 0xB4))),
            OnOff("With x axis", true, on => Labels(on, chart.VerticalRenderer.ShowLabels)),
            OnOff("With y axis", true, on => Labels(chart.HorizontalRenderer.ShowLabels, on)),
            OnOff("With dots", true, on => line.Marker = on ? Markers.Circle : null));
    }
}
