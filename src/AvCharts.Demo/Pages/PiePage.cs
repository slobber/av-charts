using Avalonia.Controls;
using Avalonia.Layout;
using AvCharts.Charts;
using AvCharts.Controls;
using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The Pie page: a whole cut into shares, the shapes a pie is drawn in, and the playground that turns it round.
internal static class PiePage
{
    /// <summary>A circle of wedges, in the shapes a pie is drawn in.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create()
    {
        return Page(
            "Pie",
            "A whole divided into shares: one wedge per reading, sized by what that reading is worth.",
            Example("Four shapes: a pie, a pushed-out pie, a doughnut and a rose", Shapes()),
            Example("Labels: inside the wedges, and beside them with a line", Labelled()),
            Example("Nested: two pies on one circle, each in a ring of its own", Nested()),
            Try("Try it: the same shares, drawn the way you ask", PiePlayground()));
    }

    /// <summary>The same readings as a pie, pushed out, as a doughnut, and as a rose.</summary>
    /// <returns>The row of four.</returns>
    private static Control Shapes()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };

        var pie = Shares(out _);
        var pushed = Shares(out var out_);
        var doughnut = Shares(out var ring);
        var rose = Shares(out var petals);

        out_.ExplodeRadius = 0.12;
        ring.InnerRadius = 0.55;
        petals.RadiusByValue = true;

        page.Children.Add(Cell(pie, 0, 0, "Pie"));
        page.Children.Add(Cell(pushed, 1, 0, "Pushed out"));
        page.Children.Add(Cell(doughnut, 2, 0, "Doughnut"));
        page.Children.Add(Cell(rose, 3, 0, "Rose: the radius is the value"));
        return page;
    }

    /// <summary>Two ways to label a wedge: in it, and beside it with a line that reaches back.</summary>
    /// <returns>The pair.</returns>
    /// <remarks>
    /// The label in a wedge carries two lines - what the reading is called and how much of the whole it is - which
    /// is what a host gets for nothing by putting a line break in the text. The one beside the circle is the same
    /// words on one line, because a line that reaches back has to reach back to something.
    /// </remarks>
    private static Control Labelled()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };

        var inside = Shares(out var written);
        var outside = Shares(out var beside);

        written.LabelSelector = share => $"{share.Name}\n{Percent(share)}";
        written.LabelPosition = PieLabelPosition.Inside;
        // Short words for the labels beside the circle: a label has to fit in the margin the plot has left over,
        // and a cell of the gallery is a narrow place for a sentence. The wedge it belongs to is what the line
        // that reaches it says, and the reading's name is what a tip is for.
        beside.LabelSelector = share => Percent(share);
        beside.LabelPosition = PieLabelPosition.Outside;

        page.Children.Add(Cell(inside, 0, 0, "In the wedges"));
        page.Children.Add(Cell(outside, 1, 0, "Beside them, with a line"));
        return page;
    }

    /// <summary>Two pies on one circle, which is what a chart of several series is.</summary>
    /// <returns>The chart, with a legend that says which ring is which.</returns>
    /// <remarks>
    /// The legend names the <em>rings</em> rather than the wedges, because a legend is a list of series and a
    /// nested pie's series are its rings. Which wedge is which is what the labels are for, and here they are on.
    /// </remarks>
    private static Control Nested()
    {
        // Shorter than an example usually is, because this page has four blocks on it and a nested pie needs
        // its legend beside it rather than above it.
        var chart = new PieChart { InnerRadius = 0.1, Height = CellHeight };
        var page = new DockPanel();

        chart.Series.Add(
            new PieSeries<Share>
            {
                DisplayName = "This year",
                ItemsSource = Sales,
                ValueSelector = share => share.Value,
                NameSelector = share => share.Name,
                LabelSelector = share => $"{share.Name} {Percent(share)}",
                LabelPosition = PieLabelPosition.Inside,
                LabelFontSize = 9,
            });

        chart.Series.Add(
            new PieSeries<Share>
            {
                DisplayName = "Last year",
                ItemsSource = LastYear,
                ValueSelector = share => share.Value,
                NameSelector = share => share.Name,
                LabelSelector = share => $"{share.Name} {PercentOf(share, LastYear)}",
                LabelPosition = PieLabelPosition.Inside,
                LabelFontSize = 9,
            });

        var legend = new Legend
        {
            Chart = chart,
            Placement = LegendPlacement.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };

        DockPanel.SetDock(legend, Dock.Right);

        page.Children.Add(legend);
        page.Children.Add(chart);
        return page;
    }

    /// <summary>
    /// The pie with knobs on: the shape it is drawn in, the hole in the middle, which way round it runs, where
    /// the first slice starts, and whether the wedges are written on.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// The knobs are the whole of what a pie is read by: a rose is the same readings read the other way round, a
    /// doughnut is the same picture with its middle taken out, the two that turn the circle are how a reader makes
    /// the slice they care about stand where they are looking - a pie has no axes to zoom - and the labels are how
    /// the picture says what it is made of without a pointer.
    /// </remarks>
    private static Control PiePlayground()
    {
        var chart = Shares(out var series);

        var shapes = new[] { "a pie", "a rose" };
        var directions = new[] { "anticlockwise", "clockwise" };
        var labels = new[] { "none", "in the wedges", "beside them" };

        // Two hundred rather than the usual two hundred and forty: this page carries four blocks, and the
        // picture a playground shows is about a chart answering to a size rather than about the size itself.
        return Playground(
            chart,
            200,
            Choice(
                "Shape",
                shapes,
                shapes[0],
                picked => series.RadiusByValue = picked == shapes[1]),
            Choice(
                "Direction",
                directions,
                directions[0],
                picked => series.Clockwise = picked == directions[1]),
            Choice(
                "Labels",
                labels,
                labels[0],
                picked =>
                {
                    series.LabelSelector = share => $"{share.Name}: {Percent(share)}";
                    series.LabelPosition = picked switch
                    {
                        "in the wedges" => PieLabelPosition.Inside,
                        "beside them" => PieLabelPosition.Outside,
                        _ => PieLabelPosition.None,
                    };
                }));
    }

    /// <summary>A pie of the shares the page and its playground are drawn from.</summary>
    /// <param name="series">The series, so that a playground can turn its knobs.</param>
    /// <returns>The chart.</returns>
    /// <remarks>
    /// Nothing is said about the colours: every wedge takes a slot of the chart's palette in the order the
    /// readings were recorded, which is what a pie is coloured by - the theme's ten colours, and round again
    /// when there are more wedges than that.
    /// </remarks>
    private static PieChart Shares(out PieSeries<Share> series)
    {
        var chart = new PieChart();

        series = new PieSeries<Share>
        {
            DisplayName = "Sales",
            ItemsSource = Sales,
            ValueSelector = share => share.Value,
            NameSelector = share => share.Name,
        };

        chart.Series.Add(series);
        return chart;
    }

    /// <summary>Where the sales came from, which is what the wedges are worth.</summary>
    private static readonly Share[] Sales =
    [
        new("North", 42),
        new("South", 28),
        new("East", 17),
        new("West", 13),
    ];

    /// <summary>The year before, which is the ring inside the one above.</summary>
    private static readonly Share[] LastYear =
    [
        new("North", 38),
        new("South", 33),
        new("East", 12),
        new("West", 17),
    ];

    /// <summary>How much of this year's whole a reading is.</summary>
    /// <param name="share">The reading.</param>
    /// <returns>The share, written as a percentage.</returns>
    private static string Percent(Share share) => PercentOf(share, Sales);

    /// <summary>How much of a whole a reading is.</summary>
    /// <param name="share">The reading.</param>
    /// <param name="of">The readings the whole is made of.</param>
    /// <returns>The share, written as a percentage.</returns>
    private static string PercentOf(Share share, Share[] of) =>
        $"{share.Value / of.Sum(one => one.Value) * 100:0.#}%";

    /// <summary>One share of the whole: what it is called, and what it is worth.</summary>
    /// <param name="Name">What it is called.</param>
    /// <param name="Value">What it is worth.</param>
    private sealed record Share(string Name, double Value);
}
