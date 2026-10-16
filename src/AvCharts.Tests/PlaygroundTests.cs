using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Demo;
using AvCharts.Themes;
using iPaas.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the knobs on the gallery's playground: that a group of them keeps exactly one on, whatever is clicked,
/// and that clicking one does not take the application down with it.
/// </summary>
/// <remarks>
/// The second half of that is the reason this class exists. Turning the other buttons off is a change to their
/// own state, and their handlers turned them straight back on - so clicking one of these threw a
/// <c>StackOverflowException</c> and there was no catching it. A test that clicks one is therefore a test that
/// the application survives being used.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class PlaygroundTests
{
    [Fact]
    public void One_button_of_a_group_is_on_whatever_is_clicked()
    {
        var page = Show("Lines");

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        var groups = page.GetVisualDescendants().OfType<ToggleGroup>().ToArray();

        Assert.NotEmpty(groups);

        foreach (var group in groups)
        {
            var buttons = group.GetVisualDescendants().OfType<Toggle>().ToArray();

            Assert.True(buttons.Length > 1, "a group should offer more than one choice");

            for (var clicked = 0; clicked < buttons.Length; clicked++)
            {
                buttons[clicked].IsChecked = true;

                Assert.True(buttons[clicked].IsChecked == true, "the button that was picked should be on");
                Assert.Single(buttons, button => button.IsChecked == true);

                // And clicking the one that is already on leaves it on, because a group of buttons read as one
                // control has no "none of them" state.
                buttons[clicked].IsChecked = false;

                Assert.True(buttons[clicked].IsChecked == true, "the last button cannot be turned off");
                Assert.Single(buttons, button => button.IsChecked == true);
            }
        }
    }

    [Fact]
    public void A_knob_that_changes_the_drawing_asks_for_a_repaint()
    {
        // The knobs were changing the chart's settings and the chart was not painting again, so every one of
        // them looked broken except the curve - which invalidates its own geometry, and was the only one that
        // appeared to work. What is asserted here is the outcome: the picture changes when a knob is turned.
        var page = Show("Lines");

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        // The playground's own chart, which is the one the knobs are wired to: a page of the gallery may show
        // several charts, and one of them having two series is not what makes it this one.
        var chart = Example(page);
        var layer = chart.VerticalLayer;
        var was = chart.PlotBounds;
        var before = Picture(layer);

        layer.ShowGridLines = false;
        chart.VerticalRenderer.ShowLabels = false;
        chart.InvalidateArrange();
        layer.InvalidateVisual();

        // The chart is laid out again, which is what the library's own invalidation asks for: an application's
        // layout pass would do this for us, and a test has to do it by hand - arranging the page is not enough,
        // because the page's own arrange was not invalidated.
        chart.Measure(chart.Bounds.Size);
        chart.Arrange(chart.Bounds);

        var after = Picture(layer);

        // And hiding the labels gives the plot the gutter's room back, which is a layout matter rather than a
        // painting one - that is the half a repaint alone would get wrong, and the reason a change to an axis'
        // renderer has to lay the chart out again and not merely paint it.
        Assert.True(
            chart.PlotBounds.Width > was.Width,
            $"the plot is {chart.PlotBounds.Width} wide with the labels off and was {was.Width} with them on");
    }
    /// <summary>Lays the page out again, and every chart in a frame with it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="frame">The frame the example stands in.</param>
    private static void Deep(Control page, Control frame)
    {
        Layout(page);

        foreach (var chart in frame.GetVisualDescendants().OfType<AvCharts.Controls.CartesianChart>())
        {
            chart.Measure(chart.Bounds.Size);
            chart.Arrange(chart.Bounds);
        }

        Layout(page);
    }

    /// <summary>The knobs a playground offers, which stand beside its example in the same grid.</summary>
    /// <param name="frame">The frame the example stands in.</param>
    /// <returns>The knobs.</returns>
    /// <remarks>
    /// The frame is inside a stack panel that holds it and the line about its size, and the knobs are in the column
    /// beside that panel - so they are found by walking up to the playground and down again. Read off the frame's
    /// own parent they were not found at all, which is how the check below went on passing while checking nothing.
    /// <para>
    /// The pickers, because they are the knobs a still picture can judge: turning one to another setting changes
    /// what is drawn and nothing else. The switches and the groups of buttons are driven by the checks that are
    /// about them - a live feed, a crosshair, a tip under the pointer - and a hand-driven layout in a test has no
    /// pointer and no clock to turn them with.
    /// </para>
    /// </remarks>
    private static Select[] Knobs(Control frame) =>
        frame.GetVisualAncestors()
            .Select(one => one.GetVisualDescendants().OfType<Select>().ToArray())
            .FirstOrDefault(one => one.Length > 0)
            ?? [];

    [Fact]
    public void Every_knob_a_playground_offers_changes_what_is_shown()
    {
        // A knob wired to nothing looks exactly like a knob that works, and the reader who finds out is the one
        // who turns it. So every picker every playground offers is turned to each of its settings in turn, and each
        // of them has to change the picture of the example it belongs to.
        var dead = new List<string>();

        foreach (var entry in Catalog.Groups.SelectMany(group => group.Entries))
        {
            var page = Show(entry.Title);

            Layout(page);

            foreach (var frame in Frames(page))
            {
                foreach (var knob in Knobs(frame))
                {
                    var options = ((IEnumerable)knob.ItemsSource!).Cast<object>().ToArray();
                    var before = Picture(frame);
                    var changed = false;

                    foreach (var option in options)
                    {
                        knob.SelectedItem = option;
                        Deep(page, frame);
                        changed |= Ink.Differences(before, Picture(frame)) > 0;
                    }

                    if (!changed)
                    {
                        dead.Add($"{entry.Title}: {string.Join(" / ", options)}");
                    }
                }
            }
        }

        Assert.True(dead.Count == 0, "knobs that changed nothing: " + string.Join("; ", dead));
    }

    [Fact]
    public void A_switch_and_a_picker_are_the_house_s_own_controls()
    {
        // The knobs are iPaas.Controls rather than bare Avalonia ones, so that the demo looks like the rest of
        // the house's demos - and so that a reader sees the controls they would use.
        var page = Show("Lines");

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        Assert.NotEmpty(page.GetVisualDescendants().OfType<Switch>());
        Assert.NotEmpty(page.GetVisualDescendants().OfType<Select>());
    }

    /// <summary>The pages a reader can try something on, which is every page whose subject is a chart.</summary>
    public static TheoryData<string> PagesWithAPlayground() =>
        ["Lines", "Columns", "Bars", "Points", "Candles", "Pie", "Axes", "ECG"];

    [Theory]
    [MemberData(nameof(PagesWithAPlayground))]
    public void A_playground_can_be_resized_by_its_corner(string name)
    {
        // The corner is what answers the third question a picture cannot: what a chart does with the room it is
        // given. So each of these pages must have one, it must be somewhere the pointer can take hold of it, and
        // dragging it must change the size of the example.
        var page = Show(name);

        Layout(page);

        // Every playground on the page: a page can offer more than one, and each of them is dragged by its own
        // corner rather than by the page's.
        foreach (var frame in Frames(page).ToArray())
        {
            var corner = frame.GetVisualDescendants().OfType<Thumb>().Single(one => one.Name == Gallery.CornerName);
            var chart = frame.GetVisualDescendants().OfType<ChartBase>().Single();
            var was = chart.Bounds.Size;

            // A thumb is an area for hitting dressed by whatever theme the application wears, and a theme that
            // draws nothing is a corner nobody can take hold of. So the corner brings a sheet of its own, and this
            // is the assertion that it is there: it is the one thing about the corner that a picture cannot be
            // read for, and the rest - whether the pointer reaches it - is Avalonia's own routing, which no test
            // here can drive (see the note on the library's gesture tests).
            Assert.NotNull(corner.GetVisualChildren().OfType<Border>().Single().Background);
            Assert.True(
                corner.Bounds is { Width: > 0, Height: > 0 },
                $"{name}: the corner has no room to be hit in");

            // Smaller, and then part of the way back: the example follows the corner both ways.
            Drag(corner, new Vector(-80, -60));
            Layout(page);

            Assert.Equal(was.Width - 80, chart.Bounds.Width, 1);
            Assert.Equal(was.Height - 60, chart.Bounds.Height, 1);

            Drag(corner, new Vector(50, 30));
            Layout(page);

            Assert.Equal(was.Width - 30, chart.Bounds.Width, 1);
            Assert.Equal(was.Height - 30, chart.Bounds.Height, 1);
        }
    }

    [Fact]
    public void A_live_page_stops_while_it_is_not_the_page_being_looked_at()
    {
        // The scope and the feed run on clocks, and a clock that keeps running while the reader is on another page
        // costs something for nothing - and a running timer is held by the dispatcher, so the page could not even
        // be collected. Both pages say what they are doing, so that is what this reads.
        var page = Show("Oscilloscope");
        var window = new Window
        {
            Resources = ChartTheme.Create(),
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        };

        window.Show();

        var report = page.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(one => one.Text?.Contains("samples held", StringComparison.Ordinal) == true);

        Assert.Contains("a sweep", report.Text!, StringComparison.Ordinal);

        window.Content = null;

        Assert.Contains("held:", report.Text!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_oscilloscope_lets_the_sweeps_it_cannot_reach_go()
    {
        // The page used to trim its own list on every arrival and then tell the series to read it again. The
        // series bounds the window now, from the sweep the page chose, and the page says nothing about it - so
        // this asserts what a reader is owed by either arrangement: a scope left running holds a few sweeps
        // rather than every reading it has ever taken, and the trace is still drawn on them.
        //
        // The clock is the page's own, a timer on the dispatcher, so it is pumped by hand a beat at a time and
        // the page is laid out again after each one - which is where an axis works out what it covers.
        var page = Show("Oscilloscope");

        new Window
        {
            Resources = ChartTheme.Create(),
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        }.Show();

        var report = page.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(one => one.Text?.Contains("samples held", StringComparison.Ordinal) == true);
        var chart = page.GetVisualDescendants().OfType<AvCharts.Controls.CartesianChart>().Single();
        var beats = System.Diagnostics.Stopwatch.StartNew();

        // Three quarters of a second of a probe sampling a thousand times a second: half again as many readings
        // as the four sweeps the page keeps.
        while (beats.Elapsed < TimeSpan.FromMilliseconds(750))
        {
            Thread.Sleep(20);
            Dispatcher.UIThread.RunJobs();
            Layout(page);
        }

        // The page's own words for what it is holding, which is the list its series was handed.
        var words = report.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var held = int.Parse(
            words[Array.IndexOf(words, "samples") - 1],
            NumberStyles.AllowThousands | NumberStyles.Integer,
            CultureInfo.InvariantCulture);
        var drawn = chart.Series[0].Items.Count();

        // Four sweeps of 50 ms at a thousand readings a second, which is the window the page's own sweep reaches.
        Assert.InRange(held, 150, 250);

        // And the trace is drawn on them: the sweep in view is one of the four, so the plot carries a sweep's
        // worth of readings rather than being empty.
        Assert.True(drawn >= 20, $"{drawn} readings are drawn for a 50 ms sweep at a thousand a second");
    }

    [Fact]
    public void The_legend_knob_puts_the_legend_on_the_side_it_names()
    {
        // The legend is docked by the page and says which side it is on, so that the lines run the way a reader
        // reads them there - across the plot above and below it, down it beside it - and so that the room between
        // the two is left on the side that faces the chart, which is what keeps them from touching.
        var page = Show("Two scales");

        // Shown, so that the layout passes are the platform's rather than ones done by hand: a page arranged by
        // hand does not re-arrange a subtree that is still valid, and "the legend moved to the other side of the
        // chart" is exactly that kind of change.
        new Window
        {
            Resources = ChartTheme.Create(),
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        }.Show();

        Layout(page);

        var legends = page.GetVisualDescendants().OfType<Legend>().ToArray();
        var knob = page.GetVisualDescendants()
            .OfType<Select>()
            .Single(one => (one.ItemsSource as IEnumerable<string> ?? []).Contains("under the plot"));

        Assert.NotEmpty(legends);
        Assert.All(legends, one => Assert.Equal(LegendPlacement.Right, one.Placement));

        knob.SelectedItem = "under the plot";

        // A real layout pass rather than a hand-measured one: docking is read by the panel when it lays its
        // children out, and measuring a tree by hand does not cascade into a subtree that is valid but whose own
        // children are not.
        Dispatcher.UIThread.RunJobs();
        Layout(page);

        // The one the knob moved, with the chart it belongs to: the two are docked in the same panel, so their own
        // bounds are in one space and the room between them can be read off directly.
        var legend = legends.Single(one => one.Placement == LegendPlacement.Bottom);
        var chart = (Control)legend.Chart!;

        var gap = legend.Bounds.Top - chart.Bounds.Bottom;
        var lines = legend.Items.OfType<LegendItem>().ToArray();

        Assert.True(
            gap >= 6,
            $"the legend at {legend.Bounds} (docked {DockPanel.GetDock(legend)}) sits {gap:0} px under the chart "
            + $"at {chart.Bounds}");
        Assert.True(
            lines[1].Bounds.Left > lines[0].Bounds.Left,
            $"the lines of a legend under the plot are laid out down each other: {lines[0].Bounds} and {lines[1].Bounds}");
    }

    [Fact]
    public void A_live_chart_is_ranged_by_the_window_the_page_keeps_and_not_by_the_recording()
    {
        // A page that holds a window of readings and lets the rest go has to say so: a plain list does not
        // announce its own edits, and a series that is never told goes on ranging every reading it has ever
        // seen. What a reader sees then is an axis covering the whole recording rather than the window - labels
        // that never come back to where the readings are, and readings that squeeze closer together the longer
        // the page runs, however small a window is asked for.
        //
        // This is the one test here that needs the page's clock, which is a timer on the dispatcher: it is
        // pumped by hand, a beat at a time, and the chart is laid out again after each one - which is where an
        // axis works out what it covers. A page that is merely built never has a beat, and the reading it opens
        // with is the only one it ever shows.
        var page = Show("Streaming");
        var window = new Window
        {
            Resources = ChartTheme.Create(),
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        };

        window.Show();

        // The shortest window the page offers, so that the five seconds of readings it opens with leave it
        // within a second or so - and a thousand readings a second, so that the window is a wide one.
        page.GetVisualDescendants().OfType<Toggle>().Single(one => (string?)one.Content == "2 s").IsChecked = true;

        var chart = page.GetVisualDescendants().OfType<AvCharts.Controls.CartesianChart>().Single();
        var deadline = System.Diagnostics.Stopwatch.StartNew();

        // The instant the recording starts at, which is the demo's clock - pinned by the fixture, so that this is
        // the same time on every machine and every run.
        var started = DemoClock.Now();
        var origin = DateTimeAxis.ToMilliseconds(started);

        double From() => chart.Transform.GetLayout(ChartDimension.X).UnitRange.Minimum;

        // Waited for rather than assumed: the window has left the beginning of the recording once the feed has
        // been running for longer than the window is wide.
        while (deadline.Elapsed < TimeSpan.FromSeconds(20) && !(From() > origin))
        {
            Thread.Sleep(20);
            Dispatcher.UIThread.RunJobs();
            page.InvalidateMeasure();
            page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
            page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));
        }

        var range = chart.Transform.GetLayout(ChartDimension.X).UnitRange;
        var readings = chart.Series[0].Items.Count();

        // The axis covers the window: two seconds of a recording that has been running for longer than that, so
        // it starts where the window does rather than where the recording did.
        Assert.True(
            range.Minimum > origin,
            $"the window starts {(range.Minimum - origin) / 1_000:0.0} s into the recording, which is where the "
            + "recording did rather than where the window does");
        Assert.InRange(range.Length, 1_800, 2_200);

        // And the readings on it carry the time of day they were taken at: the axis is read as a clock is read,
        // and what sits at its right-hand edge is now rather than a count of seconds since the page was opened.
        var across = Assert.IsType<DateTimeAxis>(chart.HorizontalAxis);

        Assert.InRange((across.FromUnit(range.Maximum).Instant - started).TotalSeconds, 0, 60);

        // And what the chart holds is the window rather than everything it has ever been handed: two thousand
        // readings at a thousand a second, not five thousand of them and one more every millisecond since.
        Assert.True(
            readings <= 2_500,
            $"{readings} readings are held for a two second window at a thousand a second");
    }

    [Fact]
    public void The_tokens_knob_on_the_theme_page_dresses_the_chart_in_the_host_s_colours()
    {
        // The other half of the same page, and the other way round: the theme's tokens are replaced rather than
        // the variant changed, which is a chart of the host's own rather than the same chart dressed again.
        var page = Show("Theme");

        Layout(page);

        var theme = ChartTheme.Create();
        var light = Colour(theme, ChartTokens.SurfaceBackground, ThemeVariant.Light);
        var chart = Example(page);
        var was = Picture(chart);

        Assert.True(Ink.Count(was, light) > 0, "the chart should open in the theme's colours");

        page.GetVisualDescendants()
            .OfType<Select>()
            .Single(one => (one.ItemsSource as IEnumerable<string> ?? []).Contains("the host's"))
            .SelectedItem = "the host's";

        Layout(page);

        var now = Picture(Example(page));

        Assert.Equal(0, Ink.Count(now, light));
        Assert.True(Ink.Differences(was, now) > 0, "the chart did not take the colours the page put above it");
    }

    [Fact]
    public void The_variant_knob_on_the_theme_page_dresses_the_chart_in_that_variant()
    {
        // What the page says and what the chart wears have to be the same thing. The knob used to move the
        // sentence under the chart and leave the chart in the light variant, which is the one thing here a
        // reader can check for themselves - and did.
        var page = Show("Theme");

        Layout(page);

        var theme = ChartTheme.Create();
        var light = Colour(theme, ChartTokens.SurfaceBackground, ThemeVariant.Light);
        var dark = Colour(theme, ChartTokens.SurfaceBackground, ThemeVariant.Dark);
        var chart = Example(page);

        Assert.Equal(0, Ink.Count(Picture(chart), dark));
        Assert.True(Ink.Count(Picture(chart), light) > 0, "the chart should open in the light variant");

        page.GetVisualDescendants()
            .OfType<Select>()
            .Single(one => (one.ItemsSource as IEnumerable<string> ?? []).Contains("dark"))
            .SelectedItem = "dark";

        Layout(page);

        // The same chart, dressed again: the variant is above the chart rather than written on it, so asking for
        // the other one is a repaint of the chart that is already there and not a second chart put in its place.
        Assert.Same(chart, Example(page));
        Assert.True(Ink.Count(Picture(chart), dark) > 0, "the chart is not wearing the variant the knob asked for");
        Assert.Equal(0, Ink.Count(Picture(chart), light));
    }

    [Fact]
    public void The_example_stops_before_it_reaches_the_knobs()
    {
        // Dragged as far as it will go, the example stops where the room does and the knobs are left where they
        // are: what is being shown is a chart answering to its size, not a page coming apart.
        var page = Show("Lines");

        Layout(page);

        var corner = Corner(page);
        var chart = Example(page);

        Drag(corner, new Vector(4000, 4000));
        Layout(page);

        var widest = chart.Bounds.Width;
        var tallest = chart.Bounds.Height;

        Drag(corner, new Vector(4000, 4000));
        Layout(page);

        Assert.Equal(widest, chart.Bounds.Width, 1);
        Assert.Equal(tallest, chart.Bounds.Height, 1);
        Assert.True(
            OnPage(page, chart).Right <= OnPage(page, page.GetVisualDescendants().OfType<Select>().First()).Left + 0.5,
            "the example is drawn over the knobs");
    }

    [Fact]
    public void The_export_page_shows_the_same_picture_however_dense_it_is()
    {
        // A reader reported that the preview of a picture at two or three pixels a point showed a corner of
        // itself. The cause was measured rather than guessed: Avalonia draws a bitmap denser than 96 dots to the
        // inch at its pixel size and ignores the room it is given, so the page draws its own preview now. What is
        // asserted is what the reader sees: the preview is the same drawing at every pitch, and only the raster
        // behind it is finer. A preview showing a corner of a denser picture would differ in most of itself.
        var page = Show("Export");

        Layout(page);

        var pitch = page.GetVisualDescendants()
            .OfType<Select>()
            .Single(one => (one.ItemsSource as IEnumerable<string> ?? []).Contains("3"));
        var picture = page.GetVisualDescendants().OfType<Control>().Single(one => one.Name == "export-picture");

        var shown = new List<RenderTargetBitmap>();

        foreach (var chosen in new[] { "1", "2", "3" })
        {
            pitch.SelectedItem = chosen;
            Layout(page);

            var preview = new RenderTargetBitmap(
                new PixelSize((int)picture.Bounds.Width, (int)picture.Bounds.Height));

            // On its own, which is drawn from its own origin rather than from where its parent put it - measured.
            preview.Render(picture);

            shown.Add(preview);
        }

        Assert.True(Ink.Count(shown[0]) > 0, "the preview has nothing in it at all");

        // The shape of what is drawn is the picture's own, not the box's: the chart is 480 by 300, so the ink in
        // the preview is about one and six tenths as wide as it is tall. A preview showing a corner of the picture
        // would fill the box instead, which is 170 by 120 - one and four tenths - and would be the same at every
        // pitch, which is exactly the difference the reader was looking at.
        foreach (var (preview, chosen) in shown.Zip(new[] { "1", "2", "3" }))
        {
            var ink = Ink.Bounds(preview);
            var shape = (ink.Right - ink.Left + 1.0) / (ink.Bottom - ink.Top + 1.0);

            Assert.True(
                shape is > 1.5 and < 1.8,
                $"{chosen} to a point: the preview shows something {shape:0.00} as wide as it is tall, "
                + "which is the box rather than the picture");
        }
    }

    [Fact]
    public void The_ecg_playground_keeps_its_paper_square_whatever_size_it_is_dragged_to()
    {
        // A strip chart is read on paper whose cells are square, and the playground is the one chart here whose
        // size is decided by hand - so this asserts the outcome of dragging it: the paper is still paper, at a
        // size nobody chose in advance. The playground holds that by arithmetic rather than by being built at one
        // size, which is the whole reason it can be dragged at all.
        var page = Show("ECG");

        Layout(page);

        var corner = Corner(page);
        var chart = Example(page);

        foreach (var drag in new[] { new Vector(-90, -70), new Vector(140, 260) })
        {
            Drag(corner, drag);
            Layout(page);

            var across = chart.Transform.GetLayout(ChartDimension.X);
            var up = chart.Transform.GetLayout(ChartDimension.Y);
            var horizontalCell = across.MajorStep.Length * chart.PlotBounds.Width / across.UnitRange.Length;
            var verticalCell = up.MajorStep.Length * chart.PlotBounds.Height / up.UnitRange.Length;

            Assert.Equal(horizontalCell, verticalCell, 0.5);

            // And the paper is centred on the baseline rather than the plot being stretched around it: a taller
            // frame is more paper, not a taller signal.
            Assert.Equal(0, up.UnitRange.Minimum + up.UnitRange.Maximum, 1);
        }
    }

    /// <summary>The corner of the first playground on a page, which is the one a reader drags.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The corner.</returns>
    private static Thumb Corner(Control page) => Corners(page).First();

    /// <summary>The corners of the playgrounds on a page, in the order they are on it.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The corners.</returns>
    private static IEnumerable<Thumb> Corners(Control page) =>
        page.GetVisualDescendants().OfType<Thumb>().Where(one => one.Name == Gallery.CornerName);

    /// <summary>
    /// The chart a playground is about: the one standing in the frame its corner belongs to, whichever kind of
    /// chart the page draws with.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The chart.</returns>
    private static ChartBase Chart(Control page) =>
        Corner(page)
            .GetVisualAncestors()
            .OfType<Grid>()
            .First()
            .GetVisualDescendants()
            .OfType<ChartBase>()
            .Single();

    /// <summary>The Cartesian chart a playground's example is, for the tests that drive its axes.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The chart.</returns>
    private static CartesianChart Example(Control page) =>
        Assert.IsType<CartesianChart>(Chart(page));

    /// <summary>Drags a corner by a distance, the way the pointer would.</summary>
    /// <param name="corner">The corner.</param>
    /// <param name="by">How far it moved.</param>
    private static void Drag(Thumb corner, Vector by) =>
        corner.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = by });

    /// <summary>Lays a page out again, which is what an application's layout pass does on its own.</summary>
    /// <param name="page">The page.</param>
    /// <remarks>
    /// <para>
    /// The frames are laid out by hand on top of that. Avalonia arranges a control with the rectangle it is
    /// already in without laying its children out again, so arranging the page alone leaves a frame that was
    /// resized where it was - an application's layout manager asks for the second pass, and in a test it has to
    /// be asked for (the same note the library's playground tests carry).
    /// </para>
    /// <para>
    /// Twice, because a change that asks for another layout - resizing a chart does, and so does the strip chart
    /// placing its window around the baseline - is applied on the pass after the one it was asked for on.
    /// </para>
    /// </remarks>
    private static void Layout(Control page)
    {
        page.InvalidateMeasure();
        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var frame in Frames(page))
            {
                frame.Measure(frame.Bounds.Size);
                frame.Arrange(frame.Bounds);
            }
        }
    }

    /// <summary>The frames the playgrounds on a page put their examples in.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The frames.</returns>
    private static IEnumerable<Control> Frames(Control page) =>
        Corners(page).Select(one => (Control)one.GetVisualParent()!);

    /// <summary>Where a control sits on the page, rather than where it sits inside its own parent.</summary>
    /// <param name="page">The page.</param>
    /// <param name="control">The control.</param>
    /// <returns>The rectangle, in the page's own coordinates.</returns>
    private static Rect OnPage(Control page, Visual control)
    {
        var origin = control.TranslatePoint(new Point(), page);

        Assert.NotNull(origin);
        return new Rect(origin!.Value, control.Bounds.Size);
    }

    /// <summary>Draws a chart, so that a claim about ink has something behind it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(Avalonia.Layout.Layoutable chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(Catalog.PageWidth, Catalog.PageHeight));
        bitmap.Render(chart);
        return bitmap;
    }

    /// <summary>One token of the theme as the colour it is, read in one variant.</summary>
    /// <param name="theme">The theme.</param>
    /// <param name="token">The token's key.</param>
    /// <param name="variant">The variant to read it in.</param>
    /// <returns>The colour.</returns>
    private static Color Colour(ResourceDictionary theme, string token, ThemeVariant variant)
    {
        Assert.True(theme.TryGetResource(token, variant, out var value), $"{token} is not defined");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    /// <summary>Shows a page of the gallery the way the demo window shows it.</summary>
    /// <param name="name">The page's name.</param>
    /// <returns>The page.</returns>
    private static Control Show(string name)
    {
        var page = Catalog.Groups
            .SelectMany(group => group.Entries)
            .Single(entry => entry.Title == name)
            .Create();

        _ = new Window
        {
            Resources = ChartTheme.Create(),
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        };

        return page;
    }
}
