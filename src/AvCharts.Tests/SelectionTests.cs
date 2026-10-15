using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks selection: which records a click and the keyboard pick out, what Control and Shift do to that, where
/// the caret goes, and that what is picked is drawn.
/// </summary>
/// <remarks>
/// The clicks and keys are driven through <c>SelectAt</c> and <c>Navigate</c> - the methods the pointer and key
/// handlers call - for the reason the gesture, crosshair and data tip tests give: the headless platform cannot
/// route input to a window in this setup, so routing is the one part of this that no test here covers.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class SelectionTests
{
    [Fact]
    public void Nothing_is_selectable_until_the_mode_asks_for_it()
    {
        var (chart, series) = Chart();

        // Off by default, and not focusable either: a chart that takes the keyboard away from a host's text
        // boxes without being asked is a chart in the way, which is why selection is opt in.
        Assert.Equal(ChartSelectionMode.None, chart.SelectionMode);
        Assert.False(chart.Focusable);

        chart.Click(Position(series, 1), KeyModifiers.None);
        Assert.Empty(chart.SelectedItems);
        Assert.Null(chart.Caret);
        Assert.False(chart.Press(Key.Right));

        // Asking for it makes the chart focusable, which is also what the focus ring is drawn for.
        chart.SelectionMode = ChartSelectionMode.Single;

        Assert.True(chart.Focusable);
    }

    [Fact]
    public void A_click_picks_the_record_under_the_pointer()
    {
        var (chart, series) = Chart(ChartSelectionMode.Single);

        chart.Click(Position(series, 1), KeyModifiers.None);

        Assert.Equal([1], chart.SelectedItems.Select(item => item.Index).ToArray());
        Assert.Equal(1, chart.Caret!.Value.Index);

        // One at a time: picking another replaces the first rather than adding to it.
        chart.Click(Position(series, 2), KeyModifiers.None);

        Assert.Equal([2], chart.SelectedItems.Select(item => item.Index).ToArray());

        // And a click where there is no record lets the whole thing go, caret and all.
        chart.Click(Pixel(chart, 3, 1), KeyModifiers.None);

        Assert.Empty(chart.SelectedItems);
        Assert.Null(chart.Caret);
    }

    [Fact]
    public void More_than_one_can_be_picked_with_control_and_shift()
    {
        var (chart, series) = Chart(ChartSelectionMode.Multiple);

        chart.Click(Position(series, 0), KeyModifiers.None);
        chart.Click(Position(series, 2), KeyModifiers.Control);

        Assert.Equal([0, 2], chart.SelectedItems.Select(item => item.Index).ToArray());

        // Control again on one that is already picked takes it out, which is the toggle a reader expects.
        chart.Click(Position(series, 0), KeyModifiers.Control);

        Assert.Equal([2], chart.SelectedItems.Select(item => item.Index).ToArray());

        // Shift takes the selection from the anchor to the record clicked: the anchor was left at the record
        // Control last picked, which is the one that stayed.
        chart.Click(Position(series, 0), KeyModifiers.None);
        chart.Click(Position(series, 2), KeyModifiers.Shift);

        Assert.Equal([0, 1, 2], chart.SelectedItems.Select(item => item.Index).ToArray());

        // One at a time, Control replaces rather than toggles: there is nothing to add to.
        chart.SelectionMode = ChartSelectionMode.Single;
        chart.Click(Position(series, 0), KeyModifiers.None);
        chart.Click(Position(series, 2), KeyModifiers.Control);

        Assert.Equal([2], chart.SelectedItems.Select(item => item.Index).ToArray());
    }

    [Fact]
    public void The_keyboard_walks_the_records_and_space_picks()
    {
        var (chart, _) = Chart(ChartSelectionMode.Single);

        // An arrow with no caret yet starts at the first record the chart is showing, and selects it: the caret
        // is where a reader is, and moving there is choosing it.
        Assert.True(chart.Press(Key.Right));
        Assert.Equal(0, chart.Caret!.Value.Index);
        Assert.Equal([0], chart.SelectedItems.Select(item => item.Index).ToArray());

        Assert.True(chart.Press(Key.Right));
        Assert.Equal([1], chart.SelectedItems.Select(item => item.Index).ToArray());

        Assert.True(chart.Press(Key.Left));
        Assert.Equal([0], chart.SelectedItems.Select(item => item.Index).ToArray());

        // Nothing wraps round: at the beginning the caret stays where it is and the key is not consumed.
        Assert.False(chart.Press(Key.Left));
        Assert.Equal(0, chart.Caret!.Value.Index);

        Assert.True(chart.Press(Key.End));
        Assert.Equal(2, chart.Caret!.Value.Index);
        Assert.Equal([2], chart.SelectedItems.Select(item => item.Index).ToArray());

        Assert.True(chart.Press(Key.Home));
        Assert.Equal(0, chart.Caret!.Value.Index);

        // Space picks the record the caret is on without moving it, and in one-at-a-time mode that is a no-op
        // on what is already picked - the selection it leaves is the same one.
        Assert.True(chart.Press(Key.Space));
        Assert.Equal([0], chart.SelectedItems.Select(item => item.Index).ToArray());
    }

    [Fact]
    public void Control_moves_the_caret_without_touching_the_selection()
    {
        var (chart, series) = Chart(ChartSelectionMode.Multiple);

        chart.Click(Position(series, 0), KeyModifiers.None);

        Assert.True(chart.Press(Key.Right, KeyModifiers.Control));

        // The caret moved and nothing was picked: this is how a reader looks along a series before choosing.
        Assert.Equal(1, chart.Caret!.Value.Index);
        Assert.Equal([0], chart.SelectedItems.Select(item => item.Index).ToArray());

        // Without the modifier, the record the caret lands on is the one that is picked - and the caret was left
        // where Control put it, so this is the record after that one rather than after the selection. The caret
        // is where the keyboard is, which is exactly what Control is for.
        Assert.True(chart.Press(Key.Right));

        Assert.Equal(2, chart.Caret!.Value.Index);
        Assert.Equal([2], chart.SelectedItems.Select(item => item.Index).ToArray());
    }

    [Fact]
    public void Shift_takes_the_selection_from_the_anchor_and_narrows_it_again()
    {
        var (chart, series) = Chart(ChartSelectionMode.Multiple, values: [1.0, 1.5, 2.0, 2.5, 3.0]);

        chart.Click(Position(series, 1), KeyModifiers.None);

        Assert.True(chart.Press(Key.Right, KeyModifiers.Shift));
        Assert.True(chart.Press(Key.Right, KeyModifiers.Shift));

        Assert.Equal([1, 2, 3], chart.SelectedItems.Select(item => item.Index).ToArray());
        Assert.Equal(3, chart.Caret!.Value.Index);

        // The anchor did not move, so the same key narrows the range again rather than starting a new one.
        Assert.True(chart.Press(Key.Left, KeyModifiers.Shift));

        Assert.Equal([1, 2], chart.SelectedItems.Select(item => item.Index).ToArray());
    }

    [Fact]
    public void The_arrows_change_series_when_they_go_up_and_down()
    {
        var lower = Line([1.0, 2.0, 3.0]);
        var upper = Half([1.0, 2.0, 3.0]);
        var chart = Bare(ChartSelectionMode.Single, lower, upper);

        chart.Click(Position(lower, 1), KeyModifiers.None);

        // Down lands on the first record of the series next door, rather than on the nearest by position: what
        // the reader is changing is which series they are walking.
        Assert.True(chart.Press(Key.Down));
        Assert.Same(upper, chart.Caret!.Value.Series);
        Assert.Equal(0, chart.Caret!.Value.Index);

        Assert.True(chart.Press(Key.Up));
        Assert.Same(lower, chart.Caret!.Value.Series);
        Assert.Equal(0, chart.Caret!.Value.Index);

        // There is no series above this one, so the caret stays and the key is not consumed.
        Assert.False(chart.Press(Key.Up));
        Assert.Same(lower, chart.Caret!.Value.Series);
    }

    [Fact]
    public void What_is_picked_out_is_drawn()
    {
        var (chart, series) = Chart(ChartSelectionMode.Multiple, [1.0, 1.5, 2.0, 2.5, 3.0]);

        chart.ItemSelectedFill = new SolidColorBrush(Colors.Magenta);
        chart.ItemSelectedStroke = new Pen(Brushes.Magenta, 2);
        chart.ItemHoverFill = new SolidColorBrush(Colors.Chartreuse);
        chart.ItemHoverStroke = new Pen(Brushes.Chartreuse, 1);

        var nothing = Picture(chart);

        Assert.Equal(0, Ink.Count(nothing, Colors.Magenta));
        Assert.Equal(0, Ink.Count(nothing, Colors.Chartreuse));

        chart.Click(Position(series, 1), KeyModifiers.None);

        var selected = Picture(chart);

        Assert.True(Ink.Count(selected, Colors.Magenta) > 0, "the selected record should have been marked");
        Assert.Equal(0, Ink.Count(selected, Colors.Chartreuse));

        // Control moves the caret without selecting: the lighter of the two accents is what a reader sees
        // instead, so they can tell where the keyboard is from what they have chosen.
        chart.Click(Position(series, 2), KeyModifiers.None);
        chart.Press(Key.Right, KeyModifiers.Control);

        var caret = Picture(chart);

        Assert.True(Ink.Count(caret, Colors.Chartreuse) > 0, "the caret should have been marked");

        // And letting go leaves no marks at all, which also says the layer is not painting something stale.
        chart.SelectionMode = ChartSelectionMode.None;

        Assert.Equal(0, Ink.Count(Picture(chart), Colors.Magenta));
        Assert.Equal(0, Ink.Count(Picture(chart), Colors.Chartreuse));
    }

    [Fact]
    public void Removing_a_series_lets_go_of_what_it_had_picked()
    {
        var first = Line([1.0, 2.0, 3.0]);
        var second = Half([1.0, 2.0, 3.0]);
        var chart = Bare(ChartSelectionMode.Multiple, first, second);

        chart.Click(Position(first, 1), KeyModifiers.None);
        chart.Click(Position(second, 1), KeyModifiers.Control);

        Assert.Equal(2, chart.SelectedItems.Count);

        chart.Series.Remove(second);

        // Taking a series off the chart is like hiding it, and more so: there is nowhere for a mark on one of its
        // records to be drawn, and nothing left to keep them selected for.
        Assert.Equal([1], chart.SelectedItems.Select(item => item.Index).ToArray());
        Assert.All(chart.SelectedItems, item => Assert.Same(first, item.Series));
        Assert.Null(chart.Caret);
    }

    [Fact]
    public void A_chart_takes_the_keyboard_when_it_can_be_selected_from()
    {
        // A chart is focusable only while something can be selected, so this is the whole of how a reader gets
        // the arrows to work: click the chart, and the keyboard is the chart's.
        var series = Line([1.0, 2.0, 3.0]);
        var chart = Bare(ChartSelectionMode.Single, series);
        chart.FocusRing = new Pen(Brushes.Magenta, 2);

        // Named in full on purpose: `SelectionMode` is the design brief's name for the chart's own enum, and
        // Avalonia has one of its own under `Avalonia.Controls`, so a file that imports both has to say which.
        var window = new Avalonia.Controls.Window { Content = chart };
        window.Show();

        chart.SelectionMode = ChartSelectionMode.Single;
        Assert.True(chart.Focus());
        Assert.True(chart.IsFocused);

        // And the ring around the chart is drawn while it has it, in the ink the theme gives that role.
        Assert.True(Ink.Count(Picture(chart), Colors.Magenta) > 0, "the focus ring should have been drawn");

        // A chart with nothing selectable does not take the keyboard from whatever had it.
        chart.SelectionMode = ChartSelectionMode.None;

        Assert.False(chart.Focus());
    }

    [Fact]
    public void Hiding_a_series_lets_go_of_what_it_had_picked()
    {
        var first = Line([1.0, 2.0, 3.0]);
        var second = Half([1.0, 2.0, 3.0]);
        var chart = Bare(ChartSelectionMode.Multiple, first, second);

        chart.Click(Position(first, 1), KeyModifiers.None);
        chart.Click(Position(second, 1), KeyModifiers.Control);

        Assert.Equal(2, chart.SelectedItems.Count);

        second.Visible = false;

        // A mark is drawn where a record is, and a hidden series has no place on the chart: its records stop
        // being selected rather than leaving marks for records nobody can see - and the caret, which was in that
        // series, is let go with them.
        Assert.Equal([1], chart.SelectedItems.Select(item => item.Index).ToArray());
        Assert.All(chart.SelectedItems, item => Assert.Same(first, item.Series));
        Assert.Null(chart.Caret);
    }

    /// <summary>Draws the chart the way the golden tests do, so a claim about ink has something behind it.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The picture.</returns>
    private static RenderTargetBitmap Picture(CartesianChart chart)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(Width, Height), new Vector(96, 96));
        bitmap.Render(chart);
        return bitmap;
    }

    /// <summary>Where a record is drawn, which is where a click has to be to pick it.</summary>
    /// <param name="series">The series to look in.</param>
    /// <param name="index">The record's position in the data.</param>
    /// <returns>The position on the chart.</returns>
    private static Point Position(Series series, int index) =>
        series.Items.Single(item => item.Index == index).Position!.Value;

    /// <summary>Where a pair of values is drawn on a chart whose axes both run from zero to four.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="x">The value on the horizontal dimension.</param>
    /// <param name="y">The value on the vertical dimension.</param>
    /// <returns>The position on the chart.</returns>
    private static Point Pixel(CartesianChart chart, double x, double y)
    {
        var plot = chart.PlotBounds;

        return new Point(plot.X + (x / 4 * plot.Width), plot.Bottom - (y / 4 * plot.Height));
    }

    /// <summary>A series of readings, one per value.</summary>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Line(double[] values) =>
        new() { ItemsSource = values, XSelector = value => value, YSelector = value => value };

    /// <summary>A series of readings half as high as they are far along, so two series can share the x axis.</summary>
    /// <param name="values">The readings.</param>
    /// <returns>The series.</returns>
    private static LineSeries<double> Half(double[] values) =>
        new() { ItemsSource = values, XSelector = value => value, YSelector = value => value / 2 };

    /// <summary>A chart with one series of readings on it, laid out and ready to be clicked.</summary>
    /// <param name="mode">How much may be selected.</param>
    /// <param name="values">The readings to draw.</param>
    /// <returns>The chart and its series.</returns>
    private static (ChartProbe Chart, LineSeries<double> Series) Chart(
        ChartSelectionMode mode = ChartSelectionMode.None,
        double[]? values = null)
    {
        var series = Line(values ?? [1.0, 2.0, 3.0]);
        return (Bare(mode, series), series);
    }

    /// <summary>A chart with nothing dressing it, laid out and ready to be clicked.</summary>
    /// <param name="mode">How much may be selected.</param>
    /// <param name="series">The series to put on it.</param>
    /// <returns>The chart.</returns>
    private static ChartProbe Bare(ChartSelectionMode mode, params Series[] series)
    {
        var chart = new ChartProbe
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            SelectionMode = mode,
        };

        foreach (var one in series)
        {
            chart.Series.Add(one);
        }

        chart.Measure(new Size(Width, Height));
        chart.Arrange(new Rect(0, 0, Width, Height));
        return chart;
    }

    private const int Width = 400;
    private const int Height = 200;

    /// <summary>A chart that can be driven the way its handlers drive it.</summary>
    private sealed class ChartProbe : CartesianChart
    {
        public void Click(Point at, KeyModifiers modifiers) => SelectAt(at, modifiers);

        public bool Press(Key key, KeyModifiers modifiers = KeyModifiers.None) => Navigate(key, modifiers);
    }
}
