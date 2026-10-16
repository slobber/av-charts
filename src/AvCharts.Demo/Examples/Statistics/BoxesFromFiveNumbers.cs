using Avalonia.Controls;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Series;

namespace AvCharts.Demo.Examples.Statistics;

/// <summary>
/// The same chart as <c>BoxesFromFiveNumbers.axaml</c>, written in code: a week's samples summed up as a box.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. The five numbers a box is drawn from
/// are the reading's own - a sample that has already been summed up - so a chart like this is the shape a summary
/// is drawn in rather than a way of working one out. The axis covers the whiskers, because that is as far as the
/// sample reached.
/// </remarks>
public sealed class BoxesFromFiveNumbers : IExample
{
    /// <summary>Six weeks of samples, summed up.</summary>
    public static IEnumerable<object> Samples { get; } =
    [
        new Sample("W1", 4, 7, 9, 12, 16),
        new Sample("W2", 5, 8, 10, 13, 15),
        new Sample("W3", 3, 6, 8, 11, 18),
        new Sample("W4", 6, 9, 11, 14, 17),
        new Sample("W5", 4, 8, 12, 15, 19),
        new Sample("W6", 5, 9, 13, 16, 18),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new CartesianChart
        {
            Height = 180,
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 20, Interval = 5 },
        };

        chart.Series.Add(
            new BoxSeries<Sample>
            {
                DisplayName = "Samples",
                ItemsSource = Samples.Cast<Sample>(),
                XSelector = sample => sample.Week,
                MinimumSelector = sample => sample.Minimum,
                LowerQuartileSelector = sample => sample.LowerQuartile,
                MedianSelector = sample => sample.Median,
                UpperQuartileSelector = sample => sample.UpperQuartile,
                MaximumSelector = sample => sample.Maximum,
            });

        return chart;
    }

    /// <summary>One week: what it is called, and the five numbers its sample came to.</summary>
    /// <param name="Week">What the week is called.</param>
    /// <param name="Minimum">The lowest reading in the sample.</param>
    /// <param name="LowerQuartile">The quarter mark: a quarter of it was below this.</param>
    /// <param name="Median">The middle reading.</param>
    /// <param name="UpperQuartile">The three quarter mark.</param>
    /// <param name="Maximum">The highest reading in the sample.</param>
    public sealed record Sample(
        string Week,
        double Minimum,
        double LowerQuartile,
        double Median,
        double UpperQuartile,
        double Maximum);
}
