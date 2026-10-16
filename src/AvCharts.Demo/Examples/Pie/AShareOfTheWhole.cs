using Avalonia.Controls;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo.Examples.Pie;

/// <summary>
/// The same chart as <c>AShareOfTheWhole.axaml</c>, written in code: one series of shares, written on the wedges.
/// </summary>
/// <remarks>
/// This file is part of the gallery rather than of the library: it is what the "code" tab of the example shows,
/// and what the check beside the gallery draws and compares with the markup. A pie has no axes: a reading is a
/// share of the whole the series adds up to, and the chart is the polar transform rather than a cartesian one.
/// The label is a word of the reading's own, so an example works it out itself; what is drawn on a wedge is
/// thrown away rather than shrunk when it does not fit.
/// </remarks>
public sealed class AShareOfTheWhole : IExample
{
    /// <summary>What the shares came to, and what each of them is called.</summary>
    public static IEnumerable<object> Shares { get; } =
    [
        new Share("North", 42),
        new Share("South", 28),
        new Share("East", 17),
        new Share("West", 13),
    ];

    /// <summary>The chart the markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    public Control Build()
    {
        var chart = new PieChart { Height = 180 };

        chart.Series.Add(
            new PieSeries<Share>
            {
                DisplayName = "Sales",
                ItemsSource = Shares.Cast<Share>(),
                ValueSelector = share => share.Value,
                NameSelector = share => share.Name,
                LabelSelector = share => share.Label,
                LabelPosition = PieLabelPosition.Inside,
                LabelFontSize = 9,
            });

        return chart;
    }

    /// <summary>One share: what it is called, and what it is worth.</summary>
    /// <param name="Name">What it is called.</param>
    /// <param name="Value">What it is worth.</param>
    public sealed record Share(string Name, double Value)
    {
        /// <summary>The words a reader reads on this share's wedge.</summary>
        /// <remarks>
        /// A property as well as a selector, because a path is how markup names a word a reading works out: the two
        /// halves of an example are one chart, and this is what lets the markup half say what the code half says.
        /// </remarks>
        public string Label => $"{Name} {Value / Shares.Cast<Share>().Sum(one => one.Value) * 100:0}%";
    }
}
