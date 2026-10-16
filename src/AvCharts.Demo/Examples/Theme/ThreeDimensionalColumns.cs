using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo.Examples.Theme;

/// <summary>
/// The same chart as <c>ThreeDimensionalColumns.axaml</c>, written in code: columns with a little depth to them,
/// each in a colour of its own, on a page of a host's own making.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. The readings are a view model's, the
/// way an application's would be - static, and written as a sequence of objects, because a series is a model
/// object rather than a control and a page cannot bind one into another. The style itself is
/// <see cref="ThreeDimensionalColumnSeries{T}"/>, which is a host's own series rather than one of the library's.
/// </remarks>
public sealed class ThreeDimensionalColumns : IExample
{
    /// <summary>The readings: what a region was worth.</summary>
    public static IEnumerable<object> Regions { get; } =
    [
        new Region("China mainland", 100),
        new Region("Singapore", 200),
        new Region("Hong Kong", 300),
        new Region("United Kingdom", 400),
        new Region("United States", 300),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            // A page of the host's own: the theme under the chart is the dark one, because a chart with depth to
            // it is a chart for a screen that is dark, and the surface is one of the host's instead of the
            // theme's - which is what overriding a token for one chart looks like.
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x26, 0x3A)),
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 440, Interval = 100 },
        };

        chart.Series.Add(
            new ThreeDimensionalColumnSeries<Region>
            {
                ItemsSource = Regions.Cast<Region>(),
                XSelector = region => region.Name,
                YSelector = region => region.Value,
            });

        return new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Dark, Height = 200, Child = chart };
    }

    /// <summary>One reading: where it was, and what it was worth.</summary>
    /// <param name="Name">Where it was.</param>
    /// <param name="Value">What it was worth.</param>
    public sealed record Region(string Name, double Value);
}
