using Avalonia.Controls;
using Avalonia.Media;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;
using static AvCharts.Demo.Gallery;

namespace AvCharts.Demo.Pages;

// The heat map page: values at a grid of positions, coloured by a continuous scale.
internal static class HeatmapPage
{
    /// <summary>A week of load, one cell an hour, and the same reading with a few knobs on.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create() => Page(
        "Heatmap",
        "A grid of readings, coloured by the value at each position.",
        Example("A week of load, measured at every hour", Heatmap()),
        Try("Try it: the same grid in another scale", HeatmapPlayground()));

    private static CartesianChart Heatmap()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new CategoryAxis(),
        };

        chart.Series.Add(new HeatSeries<Sample>
        {
            DisplayName = "Load",
            ItemsSource = Samples(),
            XSelector = sample => sample.Day,
            YSelector = sample => sample.Hour,
            ValueSelector = sample => sample.Value,
        });

        return chart;
    }

    private static Control HeatmapPlayground()
    {
        var series = new HeatSeries<Sample>
        {
            DisplayName = "Load",
            ItemsSource = Samples(),
            XSelector = sample => sample.Day,
            YSelector = sample => sample.Hour,
            ValueSelector = sample => sample.Value,
        };

        var chart = new CartesianChart
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new CategoryAxis(),
        };

        chart.Series.Add(series);

        return Playground(
            chart,
            Choice("Colours", ["thermal", "aqua", "mono"], "thermal", picked => series.ColorScale = Scale(picked)),
            OnOff("Pinned range", false, on => series.Maximum = on ? 100 : null));
    }

    private static HeatColorScale Scale(string name) => name switch
    {
        "aqua" => new HeatColorScale(
            Color.Parse("#F3FBFF"),
            Color.Parse("#64C7E8"),
            Color.Parse("#174A7E")),
        "mono" => new HeatColorScale(
            Color.Parse("#F7F7F7"),
            Color.Parse("#777777"),
            Color.Parse("#171717")),
        _ => new HeatColorScale(
            Color.Parse("#173A7A"),
            Color.Parse("#30A46C"),
            Color.Parse("#F2D94E"),
            Color.Parse("#E4572E")),
    };

    private static Sample[] Samples()
    {
        var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var samples = new List<Sample>(days.Length * 8);

        for (var day = 0; day < days.Length; day++)
        {
            for (var hour = 0; hour < 8; hour++)
            {
                var value = 35
                    + (28 * Math.Sin((hour + (day * 0.7)) / 2))
                    + (12 * Math.Cos((day * 1.3) + (hour * 0.4)));

                samples.Add(new Sample(days[day], $"{hour * 3:00}:00", Math.Round(value, 1)));
            }
        }

        return [.. samples];
    }

    private sealed record Sample(string Day, string Hour, double Value);
}
