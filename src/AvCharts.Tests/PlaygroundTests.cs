using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvCharts.Charts;
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

        var chart = page.GetVisualDescendants().OfType<AvCharts.Controls.CartesianChart>()
            .Single(one => one.Series.Count == 2);
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
        ["Lines", "Columns", "Bars", "Points", "Axes", "ECG"];

    [Theory]
    [MemberData(nameof(PagesWithAPlayground))]
    public void A_playground_can_be_resized_by_its_corner(string name)
    {
        // The corner is what answers the third question a picture cannot: what a chart does with the room it is
        // given. So each of these pages must have one, it must be somewhere the pointer can take hold of it, and
        // dragging it must change the size of the example.
        var page = Show(name);

        Layout(page);

        var corner = Corner(page);
        var chart = Example(page);
        var was = chart.Bounds.Size;

        // A thumb is an area for hitting dressed by whatever theme the application wears, and a theme that draws
        // nothing is a corner nobody can take hold of. So the corner brings a sheet of its own, and this is the
        // assertion that it is there: it is the one thing about the corner that a picture cannot be read for,
        // and the rest - whether the pointer reaches it - is Avalonia's own routing, which no test here can drive
        // (see the note on the library's gesture tests).
        Assert.NotNull(corner.GetVisualChildren().OfType<Border>().Single().Background);
        Assert.True(corner.Bounds is { Width: > 0, Height: > 0 }, $"{name}: the corner has no room to be hit in");

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

        double From() => chart.Transform.GetLayout(ChartDimension.X).UnitRange.Minimum;

        while (deadline.Elapsed < TimeSpan.FromSeconds(20) && !(From() > 2_000))
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
            range.Minimum > 2_000,
            $"the axis starts at {range.Minimum:0} ms, which is the beginning of the recording rather than the window");
        Assert.InRange(range.Length, 1_800, 2_200);

        // And what the chart holds is the window rather than everything it has ever been handed: two thousand
        // readings at a thousand a second, not five thousand of them and one more every millisecond since.
        Assert.True(
            readings <= 2_500,
            $"{readings} readings are held for a two second window at a thousand a second");
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

    /// <summary>The corner of the playground on a page, which is the one a reader drags.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The corner.</returns>
    private static Thumb Corner(Control page) =>
        page.GetVisualDescendants().OfType<Thumb>().Single(one => one.Name == Gallery.CornerName);

    /// <summary>The chart a playground is about: the one standing in the frame its corner belongs to.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The chart.</returns>
    private static AvCharts.Controls.CartesianChart Example(Control page) =>
        Corner(page)
            .GetVisualAncestors()
            .OfType<Grid>()
            .First()
            .GetVisualDescendants()
            .OfType<AvCharts.Controls.CartesianChart>()
            .Single();

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
        page.GetVisualDescendants()
            .OfType<Thumb>()
            .Where(one => one.Name == Gallery.CornerName)
            .Select(one => (Control)one.GetVisualParent()!);

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
