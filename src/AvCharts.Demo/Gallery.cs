using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using iPaas.Controls;

// The chart's own SelectionMode has the same name as Avalonia's, so a host that imports both says which: one
// alias here, and the rest of the page reads the way it would in an application.
using ChartSelection = AvCharts.Controls.SelectionMode;

namespace AvCharts.Demo;

/// <summary>
/// The gallery: a page per feature of the library, each one a page a host could put beside its own navigation.
/// </summary>
/// <remarks>
/// The pages are built here rather than written as markup, so that a feature and its example cannot drift apart -
/// and so that the golden tests can render the very pages a reader looks at. Each page is a heading, a line about
/// it, and one example per thing it is showing; the navigation that lists them is <see cref="Catalog"/>.
/// </remarks>
public static class Pages
{
    /// <summary>A page: its name, the line under it, and its examples.</summary>
    /// <param name="title">The name.</param>
    /// <param name="about">The line under it.</param>
    /// <param name="examples">The examples.</param>
    /// <returns>The page.</returns>
    private static DemoPage Page(string title, string about, params DemoBlock[] examples)
    {
        var stack = new StackPanel { Spacing = 32 };

        foreach (var example in examples)
        {
            stack.Children.Add(example);
        }

        return new DemoPage { Title = title, Description = about, Content = stack };
    }

    /// <summary>One example: what it shows, and the chart itself.</summary>
    /// <param name="title">What the example is called.</param>
    /// <param name="example">The chart.</param>
    /// <returns>The block.</returns>
    /// <remarks>
    /// A chart has no size of its own - it fills what it is given - and a stack has no height to hand out, so
    /// every chart on a page is given one. Note that this is about the charts and not about the panel they are
    /// in: a panel tall enough for its grid is no help at all if the charts inside it are measured with infinite
    /// height, which is what a stack panel gives its children - they then take their own fallback height, spill
    /// out of their cells, and land on top of the next row. That is what the Axes page used to look like.
    /// </remarks>
    private static DemoBlock Example(string title, Control example)
    {
        GiveHeight(example, ExampleHeight);

        return new DemoBlock { Title = title, Content = example };
    }

    /// <summary>
    /// A playground: the example on the left, and the knobs that change it on the right.
    /// </summary>
    /// <param name="example">The chart.</param>
    /// <param name="knobs">The knobs, in the order they are stacked.</param>
    /// <returns>What goes in the block.</returns>
    /// <remarks>
    /// A picture shows what a chart looks like; knobs show what it does. Every page of the gallery has one of
    /// these, because the questions a reader has ("what does a curved line look like here", "what happens if I
    /// turn the grid off") are answered in a second by a switch and not at all by a screenshot.
    /// </remarks>
    private static Control Playground(Control example, params Control[] knobs)
    {
        var panel = new StackPanel { Spacing = 12, Width = 220 };

        foreach (var knob in knobs)
        {
            panel.Children.Add(knob);
        }

        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,220") };

        Grid.SetColumn(example, 0);
        Grid.SetColumn(panel, 2);
        page.Children.Add(example);
        page.Children.Add(panel);
        return page;
    }

    /// <summary>A knob that picks one of a few named settings.</summary>
    /// <typeparam name="T">What the settings are.</typeparam>
    /// <param name="label">What the knob is called.</param>
    /// <param name="options">The settings, in the order they are offered.</param>
    /// <param name="selected">Which one is picked to begin with.</param>
    /// <param name="onPick">What to do when another one is picked.</param>
    /// <returns>The knob.</returns>
    private static Control Choice<T>(string label, IReadOnlyList<T> options, T selected, Action<T> onPick)
    {
        // The house's own select, so the demo's chrome looks like the rest of the house's demos.
        var picker = new Select
        {
            ItemsSource = options,
            SelectedItem = selected,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is T picked)
            {
                onPick(picked);
            }
        };

        return Labelled(label, picker);
    }

    /// <summary>A knob that turns something on or off.</summary>
    /// <param name="label">What the knob is called.</param>
    /// <param name="on">Whether it starts on.</param>
    /// <param name="onChange">What to do when it changes.</param>
    /// <returns>The knob.</returns>
    private static Control OnOff(string label, bool on, Action<bool> onChange)
    {
        var pill = new Switch { IsChecked = on };

        pill.IsCheckedChanged += (_, _) => onChange(pill.IsChecked == true);

        // The switch is the pill and nothing else - the house says so in its own demo - so the name is a row of
        // its own beside it.
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        row.Children.Add(pill);
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    /// <summary>A row of choices read as one control, which is what the house's toggle group is for.</summary>
    /// <param name="label">What the knob is called.</param>
    /// <param name="options">The choices, in the order they are offered.</param>
    /// <param name="selected">Which one is on to begin with.</param>
    /// <param name="onPick">What to do when another one is turned on.</param>
    /// <returns>The knob.</returns>
    /// <remarks>
    /// A group of toggles does not manage its own selection - the group is the well they sit in - so the rule
    /// that one of them is on lives here: turning one on turns the rest off, and the one that is on cannot be
    /// turned off by clicking it again.
    /// </remarks>
    private static Control Choices(string label, IReadOnlyList<string> options, int selected, Action<int> onPick)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var toggles = new List<Toggle>();

        for (var index = 0; index < options.Count; index++)
        {
            var at = index;
            var toggle = new Toggle { Content = options[index], IsChecked = index == selected, Size = ToggleSize.Sm };

            toggle.IsCheckedChanged += (_, _) =>
            {
                if (toggle.IsChecked != true)
                {
                    toggle.IsChecked = true;
                    return;
                }

                foreach (var other in toggles.Where(other => !ReferenceEquals(other, toggle)))
                {
                    other.IsChecked = false;
                }

                onPick(at);
            };

            toggles.Add(toggle);
            row.Children.Add(toggle);
        }

        return Labelled(label, new ToggleGroup { Content = row });
    }
    /// <summary>Puts a name above a knob, which is how the house's demos label them.</summary>
    /// <param name="label">The name.</param>
    /// <param name="knob">The knob.</param>
    /// <returns>The two together.</returns>
    private static Control Labelled(string label, Control knob)
    {
        var stack = new StackPanel { Spacing = 4 };

        stack.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeight.SemiBold });
        stack.Children.Add(knob);
        return stack;
    }
    /// <summary>Gives every chart in an example a height, wherever it is nested.</summary>
    /// <param name="control">The example, or something inside it.</param>
    /// <param name="height">The height to give a chart that has none.</param>
    /// <remarks>
    /// A chart has no size of its own: it fills what it is given, and what a stack panel gives is unlimited
    /// height - so a chart left to itself takes its own fallback height and spills out of whatever was supposed
    /// to hold it. Every chart in the gallery is therefore given one, however deep it is.
    /// </remarks>
    private static void GiveHeight(Control control, double height)
    {
        switch (control)
        {
            case CartesianChart chart:
                if (double.IsNaN(chart.Height))
                {
                    chart.Height = height;
                }

                break;

            case Panel panel:
                foreach (var child in panel.Children.OfType<Control>())
                {
                    GiveHeight(child, height);
                }

                break;

            case Decorator decorator when decorator.Child is { } child:
                GiveHeight(child, height);
                break;

            case ContentControl content when content.Content is Control inner:
                GiveHeight(inner, height);
                break;

            default:
                break;
        }
    }

    /// <summary>Lines and the shapes that follow them: area, step, step area.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Lines()
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
            Example("Try it: the same readings, drawn the way you ask", LinePlayground()));
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
    /// <summary>Columns three ways: side by side, stacked, and with a line over them.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Columns()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        var one = new[] { 3.0, 5.0, 2.0, 6.0, 4.0, 7.0 };
        var other = new[] { 2.0, 1.0, 4.0, 2.0, 3.0, 1.0 };

        // The window is the readings' own bounds rather than a neat nought to six: a column stands in a slot that
        // reaches half a slot past each of its readings, so six readings of a column series occupy minus a half to
        // five and a half - and a window of nought to six would cut the first column and draw it in the axis'
        // gutter, which is what this page used to do.
        var clustered = Chart(-0.5, 5.5, 0, 9);
        clustered.SeriesLayout = SeriesLayout.Cluster;
        clustered.Series.Add(Column("This year", one));
        clustered.Series.Add(Column("Last year", other));

        var stacked = Chart(-0.5, 5.5, 0, 9);
        stacked.SeriesLayout = SeriesLayout.Stacked;
        stacked.Series.Add(Column("This year", one));
        stacked.Series.Add(Column("Last year", other));

        var withALine = Chart(-0.5, 5.5, 0, 9);
        withALine.Series.Add(Column("Readings", one));
        withALine.Series.Add(Line("Average", [3.0, 4.0, 3.0, 4.0, 3.5, 4.0]));

        page.Children.Add(Cell(clustered, 0, 0, "Clustered"));
        page.Children.Add(Cell(stacked, 1, 0, "Stacked"));
        page.Children.Add(Cell(withALine, 2, 0, "Columns with a line"));
        return Page("Columns", "Columns three ways: side by side, stacked, and with a line over them.", Example("Clustered, stacked, and a line over the columns", page));
    }

    /// <summary>Bars, which are columns lying down, against category names.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Bars()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 8 },
            VerticalAxis = new CategoryAxis(),
        };

        chart.Series.Add(new BarSeries<Reading>
        {
            DisplayName = "This year",
            ItemsSource = [new("North", 6), new("South", 3), new("East", 7), new("West", 4), new("Middle", 5)],
            XSelector = reading => reading.Value,
            YSelector = reading => reading.Name,
            Fill = new SolidColorBrush(Color.FromRgb(0x46, 0x82, 0xB4)),
        });

        chart.Series.Add(new BarSeries<Reading>
        {
            DisplayName = "Last year",
            ItemsSource = [new("North", 3), new("South", 2), new("East", 4), new("West", 2), new("Middle", 3)],
            XSelector = reading => reading.Value,
            YSelector = reading => reading.Name,
            Fill = new SolidColorBrush(Color.FromRgb(0x9E, 0xC5, 0xE8)),
        });

        return Page("Bars", "Columns lying down, against the names of things.", Example("Two years of readings, one bar each", chart));
    }

    /// <summary>A scatter of readings, with a bubble whose size is a third reading.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Points()
    {
        var chart = Chart(0, 10, 0, 10);
        var readings = Enumerable.Range(0, 24)
            .Select(index => new Reading($"P{index}", 1 + ((index * 7) % 8), 0.5 + (index % 9), 2 + (index % 5)))
            .ToArray();

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
            RadiusSelector = reading => reading.Size,
        });

        return Page("Points", "Readings as marks, and the same readings as bubbles.", Example("A scatter and a bubble per reading", chart));
    }

    /// <summary>One small chart per kind of axis.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Axes()
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
        return Page("Axes", "The five scales a Cartesian chart can be drawn against.", Example("Linear, logarithmic, time, duration and category", page));
    }

    /// <summary>
    /// Everything the first version promised, on one page: a combination chart, two scales, a time axis, stacked
    /// columns, a legend, tips, a crosshair and selection - and a hundred thousand readings beside it.
    /// </summary>
    /// <returns>The page.</returns>
    /// <remarks>
    /// This is the rehearsal rather than a demonstration: it is the list a reader accepts the library against,
    /// and the test beside it walks the same list. The ECG strip has a page of its own, because a strip chart is
    /// read on its own.
    /// </remarks>
    internal static DemoPage Acceptance()
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new DateTimeAxis { Interval = 86_400_000 },
            VerticalAxis = new LinearAxis(),
            SeriesLayout = SeriesLayout.Stacked,
            ShowCrosshair = true,
            ShowAllDataTips = true,
            SelectionMode = ChartSelection.Single,
        };

        var days = Enumerable.Range(0, 10).ToArray();
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        var records = days
            .Select(day => new Day(start.AddDays(day), 120 + (30 * Math.Sin(day)), 96 + (8 * Math.Cos(day))))
            .ToArray();

        chart.Series.Add(new ColumnSeries<Day>
        {
            DisplayName = "Received",
            ItemsSource = records,
            XSelector = day => day.When,
            YSelector = day => day.Received,
        });

        chart.Series.Add(new ColumnSeries<Day>
        {
            DisplayName = "Rejected",
            ItemsSource = records,
            XSelector = day => day.When,
            YSelector = day => day.Rejected,
        });

        // A scale of its own, in per cent, against the same days.
        var rate = new LineSeries<Day>
        {
            DisplayName = "Success rate",
            ItemsSource = records,
            XSelector = day => day.When,
            YSelector = day => 92 + (3 * Math.Cos((day.When - start).TotalDays)),
            StrokeThickness = 2,
        };

        rate.VerticalAxis = new LinearAxis { Minimum = 80, Maximum = 100 };
        chart.Series.Add(rate);

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        var page = new DockPanel();

        DockPanel.SetDock(legend, Dock.Right);
        page.Children.Add(legend);
        page.Children.Add(chart);

        return Page(
            "Everything at once",
            "The acceptance list on one page: a combination chart, two scales, a time axis, stacked columns, a legend, tips, a crosshair, selection - and a hundred thousand readings.",
            Example("Columns stacked under a line, with a scale each", page),
            Example("A hundred thousand readings, sampled to the pixels there are", HundredThousand()));
    }

    /// <summary>A hundred thousand readings of a signal, sampled down rather than drawn point for point.</summary>
    /// <returns>The chart.</returns>
    private static CartesianChart HundredThousand()
    {
        var chart = Chart(0, 50_000, 0, 100);

        chart.Series.Add(new LineSeries<double>
        {
            DisplayName = "Signal",
            ItemsSource = [.. Enumerable.Range(0, 100_000).Select(index => 50 + (40 * Math.Sin(index / 500.0)))],
            YSelector = value => value,
            StrokeThickness = 1.5,
            Sampling = SamplingMode.MinMax,
        });

        return chart;
    }

    /// <summary>A day of readings, with the moment it is about: a selector is handed the record, not its place.</summary>
    /// <param name="When">The day.</param>
    /// <param name="Received">How many arrived.</param>
    /// <param name="Rejected">How many of them were rejected.</param>
    private sealed record Day(DateTimeOffset When, double Received, double Rejected);
    /// <summary>Two vertical scales on one plot, with a legend that says which series is which.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Scales()
    {
        var page = new DockPanel();
        var chart = Chart(0, 6, 0, 100);
        var millimetres = Line("Millimetres", [10, 40, 25, 70, 55, 90]);

        millimetres.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 };
        chart.Series.Add(millimetres);

        var inches = Line("Inches", [1.0, 2.0, 1.5, 3.0, 2.5, 3.5]);

        inches.VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 };
        chart.Series.Add(inches);

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(legend, Dock.Right);

        page.Children.Add(legend);
        page.Children.Add(chart);
        return Page("Two scales", "One plot, two vertical scales, and a legend that says which is which.", Example("Millimetres on the left, inches on the right", page));
    }

    /// <summary>The ten colours of the palette, in both variants, side by side.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Palette()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };

        foreach (var (variant, column) in new[] { (ThemeVariant.Light, 0), (ThemeVariant.Dark, 2) })
        {
            var chart = Chart(0, 12, 0, 10);

            for (var slot = 1; slot <= ChartTokens.PaletteSize; slot++)
            {
                chart.Series.Add(Line($"S{slot}", [.. Enumerable.Range(0, 13).Select(index => 5 + (4 * Math.Sin((index / 2.0) + (slot / 1.5))))]));
            }

            // One scope per variant: each chart reads the tokens - and the palette - of the variant it is in.
            var scope = new ThemeVariantScope { RequestedThemeVariant = variant, Child = chart };

            Grid.SetColumn(scope, column);
            page.Children.Add(scope);
        }

        return Page("Palette", "The ten colours, with the same data drawn light and dark.", Example("The palette in both variants", page));
    }

    /// <summary>Tips, a legend, and a crosshair: what a reader gets when they point at the chart.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Interaction()
    {
        var page = new DockPanel();
        var chart = Chart(0, 6, 0, 10);

        chart.Series.Add(Line("Depth", [2.0, 5.0, 3.0, 7.0, 6.0, 9.0]));
        chart.Series.Add(Line("Speed", [1.0, 2.0, 1.5, 3.0, 2.5, 4.0]));
        chart.ShowCrosshair = true;
        chart.SelectionMode = ChartSelection.Single;

        // Every reading is written out, because a page is drawn once and there is no pointer to hover with: in the
        // window a reader points at a chart and gets one tip at a time.
        chart.ShowAllDataTips = true;

        var legend = new Legend { Chart = chart, VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(legend, Dock.Right);

        page.Children.Add(legend);
        page.Children.Add(chart);
        return Page("Interaction", "What a reader gets when they point at a chart.", Example("A tip per reading, a legend, and a crosshair", page));
    }

    /// <summary>The things on a chart that are not series: a band, a threshold, a mark for an event.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Annotations()
    {
        var chart = Chart(0, 8, 0, 10);
        var readings = Line("Readings", [4.0, 6.0, 5.0, 7.0, 6.5, 8.0, 7.5, 9.0]);

        // A band is behind the readings, because the layer it is drawn in is behind them.
        chart.Series.Add(readings);
        chart.Elements.Add(new Band(3, 6));
        chart.Elements.Add(new Threshold(8));
        chart.Elements.Add(new EventMark(5, "released"));

        return Page("Annotations", "The things on a chart that are not series.", Example("A band, a threshold, and a mark for an event", chart));
    }

    /// <summary>A strip of ECG: a fixed scale, an even grid, sampling, and gaps where the lead came off.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Ecg()
    {
        // A hundred seconds at two hundred samples a second - twenty thousand readings - sampled down to the
        // pixels there are, with three dropouts in them.
        //
        // The scales are the paper of a strip chart: forty pixels a second across and eighty a unit up, so one
        // small square is eight pixels each way - a grid of squares rather than a grid of rectangles. The height
        // is what makes that come out: three units over two hundred and forty pixels of plot is eighty a unit.
        var chart = new CartesianChart
        {
            // Starting at twenty-nine seconds rather than at nought so that the first dropout is on the page: a
            // strip chart is read at a paper speed, and a reader can pan along it to the others.
            // Forty pixels a second across and eighty a unit up, with an interval of one second and of half a
            // unit: the small squares come out eight pixels either way, which is the paper a strip chart is read
            // on. An interval left to the axis itself would have been two seconds - eighty pixels against forty -
            // and the grid would have been a grid of rectangles.
            HorizontalAxis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 40, Minimum = 29, Interval = 1 },
            VerticalAxis = new LinearAxis { Minimum = -1.5, Maximum = 1.5, Interval = 0.5 },
            Height = SquareGridHeight,
            ShowCrosshair = true,
        };

        // The reading carries its own time, because a selector is handed the record rather than its place in the
        // list: asking for "index / 200" of a sample that is a plain number gives the reading divided by two
        // hundred, which is a range of a hundredth of a second - and a curve drawn in one pixel column, which is
        // exactly what this page used to show.
        chart.Series.Add(new LineSeries<Sample>
        {
            DisplayName = "Lead II",
            ItemsSource = [.. Enumerable.Range(0, 20_000).Select(index => new Sample(index / 200.0, Beat(index)))],
            XSelector = sample => sample.Seconds,
            YSelector = sample => sample.Reading,
            StrokeThickness = 1.5,
            Sampling = SamplingMode.MinMax,
        });

        return Page(
            "ECG",
            "Twenty thousand samples at a fixed scale, sampled down, with dropouts.",
            Example("A hundred seconds of a synthetic lead", chart));
    }

    /// <summary>One reading of the strip, with the moment it was taken at.</summary>
    /// <param name="Seconds">How far into the recording it is.</param>
    /// <param name="Reading">What the lead read there, or not-a-number where it came off.</param>
    private sealed record Sample(double Seconds, double Reading);

    /// <summary>One beat of a synthetic signal, and not-a-number where the lead came off.</summary>
    /// <param name="index">The sample.</param>
    /// <returns>The reading.</returns>
    private static double Beat(int index)
    {
        var second = index / 200.0;

        if (second is (> 31 and < 32.2) or (> 58 and < 58.4) or (> 74 and < 76))
        {
            return double.NaN;
        }

        var phase = (second % 0.8) / 0.8;

        return phase switch
        {
            < 0.04 => 0.1 * Math.Sin(phase * Math.PI / 0.04),
            < 0.08 => -0.15,
            < 0.11 => 1.2 * Math.Sin((phase - 0.08) * Math.PI / 0.03),
            < 0.15 => -0.35,
            < 0.3 => 0.05 * Math.Sin((phase - 0.15) * Math.PI / 0.15),
            _ => 0.02 * Math.Sin(second * 6),
        };
    }

    /// <summary>A chart with a window on it and nothing else.</summary>
    /// <param name="minimumX">The left of the horizontal window.</param>
    /// <param name="maximumX">The right of it.</param>
    /// <param name="minimumY">The bottom of the vertical window.</param>
    /// <param name="maximumY">The top of it.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart Chart(double minimumX, double maximumX, double minimumY, double maximumY) =>
        new()
        {
            HorizontalAxis = new LinearAxis { Minimum = minimumX, Maximum = maximumX },
            VerticalAxis = new LinearAxis { Minimum = minimumY, Maximum = maximumY },
        };

    /// <summary>A line of readings.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="values">The readings.</param>
    /// <param name="set">Anything else to set on it.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Line(
        string name,
        double[] values,
        Action<LineSeries<double>>? set = null)
    {
        var series = new LineSeries<double>
        {
            DisplayName = name,
            ItemsSource = values,
            YSelector = value => value,
            StrokeThickness = 2,
        };

        set?.Invoke(series);
        return series;
    }

    /// <summary>A line with the area under it filled.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static AreaSeries<double> Area(string name, double[] values)
    {
        var series = new AreaSeries<double>
        {
            DisplayName = name,
            ItemsSource = values,
            YSelector = value => value,
            StrokeThickness = 2,
        };

        // The area is filled with the colour the line is drawn in, which is what the palette slot gives it.
        series.Fill = series.Color is { } colour ? new SolidColorBrush(((ISolidColorBrush)colour).Color, 0.35) : null;
        return series;
    }

    /// <summary>A line that goes across and then up, rather than straight to the next reading.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="values">The readings.</param>
    /// <param name="filled">Whether the area under it is filled.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Steps(string name, double[] values, bool filled = false)
    {
        LineSeries<double> series = filled ? new StepAreaSeries<double>() : new StepLineSeries<double>();

        series.DisplayName = name;
        series.ItemsSource = values;
        series.YSelector = value => value;
        series.StrokeThickness = 2;

        if (series is StepAreaSeries<double> area && series.Color is { } colour)
        {
            area.Fill = new SolidColorBrush(((ISolidColorBrush)colour).Color, 0.35);
        }

        return series;
    }

    /// <summary>The same readings, lower down, so that a page of five lines can be read.</summary>
    /// <param name="values">The readings.</param>
    /// <param name="by">How much lower.</param>
    /// <returns>The readings.</returns>
    private static double[] Lower(double[] values, double by) => [.. values.Select(value => value - by)];

    /// <summary>A column of readings, one per slot.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static ColumnSeries<double> Column(string name, double[] values) =>
        new()
        {
            DisplayName = name,
            ItemsSource = values,
            YSelector = value => value,
        };

    /// <summary>Puts a chart in a cell of a page, with a caption above it.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="column">Which column.</param>
    /// <param name="row">Which row.</param>
    /// <param name="caption">The caption.</param>
    /// <returns>The cell.</returns>
    private static Control Cell(Control chart, int column, int row, string caption)
    {
        var cell = new StackPanel { Margin = new Thickness(4) };

        // The chart is sized here rather than the cell: the stack panel in the cell would otherwise measure it
        // with unlimited height, and a chart answers that with a height of its own that overflows the cell.
        if (chart is CartesianChart sized && double.IsNaN(sized.Height))
        {
            sized.Height = CellHeight;
        }

        cell.Children.Add(new TextBlock { Text = caption, FontSize = 11, Opacity = 0.7 });
        cell.Children.Add(chart);

        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        return cell;
    }

    /// <summary>How tall an example is drawn, so that a page is the same shape wherever it is shown.</summary>
    private const double ExampleHeight = 240;

    /// <summary>How tall a chart in a cell of a page of several is drawn.</summary>
    private const double CellHeight = 150;

    /// <summary>
    /// How tall the strip chart is: three units of signal over a plot of two hundred and forty pixels is eighty
    /// pixels a unit, and at forty pixels a second one small square of the grid is eight pixels either way.
    /// </summary>
    private const double SquareGridHeight = 272;

    /// <summary>A reading with a name, the value charts stand it at, and the size of its bubble.</summary>
    /// <param name="Name">What it is called.</param>
    /// <param name="Value">The reading.</param>
    /// <param name="X">Where it is across, when a chart puts it somewhere.</param>
    /// <param name="Size">How far its bubble reaches, in pixels.</param>
    private sealed record Reading(string Name, double Value, double X = 0, double Size = 0);

    /// <summary>A band the readings are meant to stay inside.</summary>
    /// <param name="from">The bottom of the band.</param>
    /// <param name="to">The top of it.</param>
    private sealed class Band(double from, double to) : ChartElement
    {
        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(from), ChartValue.FromNumber(from), out var low) ||
                !renderContext.TryPoint(ChartValue.FromNumber(to), ChartValue.FromNumber(to), out var high))
            {
                return;
            }

            var plot = renderContext.PlotBounds;

            context.FillRectangle(
                new SolidColorBrush(Color.FromRgb(0x46, 0x82, 0xB4), 0.12),
                new Rect(plot.Left, high.Y, plot.Width, low.Y - high.Y));
        }
    }

    /// <summary>A line across the plot at a reading nobody is meant to go past.</summary>
    /// <param name="value">The reading.</param>
    private sealed class Threshold(double value) : ChartElement
    {
        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(value), ChartValue.FromNumber(value), out var at))
            {
                return;
            }

            var plot = renderContext.PlotBounds;
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22)), 1, new DashStyle([4, 3], 0));

            context.DrawLine(pen, new Point(plot.Left, at.Y), new Point(plot.Right, at.Y));
        }
    }

    /// <summary>A mark on the plot where something happened.</summary>
    /// <param name="at">The reading it happened at.</param>
    /// <param name="what">What happened.</param>
    private sealed class EventMark(double at, string what) : ChartElement
    {
        protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
        {
            if (!renderContext.TryPoint(ChartValue.FromNumber(at), ChartValue.FromNumber(at), out var where))
            {
                return;
            }

            var plot = renderContext.PlotBounds;
            var colour = new SolidColorBrush(Color.FromRgb(0x4A, 0x53, 0x5A));
            var text = new FormattedText(
                what,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default),
                10,
                colour);

            context.DrawLine(new Pen(colour, 1), new Point(where.X, plot.Top), new Point(where.X, plot.Bottom));
            context.DrawText(text, new Point(where.X + 3, plot.Top + 2));
        }
    }
}
