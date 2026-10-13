using System.Windows.Input;
using Avalonia;
using Avalonia.Input;
using AvCharts.Charts;
using AvCharts.Controls;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks what a chart says about the records a reader points at: which record an event is about, when a pointer
/// has entered one and when it has left, when a click is a click rather than a drag, when the selection changed,
/// and that a host can bind a command instead of subscribing to an event.
/// </summary>
/// <remarks>
/// The events are driven through <c>TrackPointer</c>, <c>PressAt</c>, <c>ReleaseAt</c> and <c>Navigate</c> - the
/// methods the handlers call - for the reason the gesture, crosshair, tip and selection tests give: the headless
/// platform cannot route input to a window in this setup, so routing is the one part of this that no test here
/// covers.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class EventTests
{
    [Fact]
    public void The_pointer_says_when_it_is_on_a_record_and_when_it_is_not()
    {
        var (chart, series) = Chart();
        var entered = new List<int>();
        var exited = new List<int>();
        var moved = 0;

        chart.ItemPointerEntered += (_, e) => entered.Add(e.ChartItem!.Value.Index);
        chart.ItemPointerExited += (_, e) => exited.Add(e.ChartItem!.Value.Index);
        chart.ItemPointerMoved += (_, _) => moved++;

        chart.Track(Position(series, 1));

        // Onto a record: entered, and moved - both, because the pointer moved and it was on something.
        Assert.Equal([1], entered);
        Assert.Empty(exited);
        Assert.Equal(1, moved);

        // Moving within the same record is a move and nothing else; a record is not a visual with edges to cross.
        chart.Track(Position(series, 1) + new Point(2, 0));

        Assert.Equal([1], entered);
        Assert.Equal(2, moved);

        // Away from every record: left that one, entered nothing.
        chart.Track(new Point(chart.PlotBounds.Right - 2, chart.PlotBounds.Top + 2));

        Assert.Equal([1], exited);
        Assert.Equal([1], entered);
        Assert.Equal(2, moved);

        // And there is nothing left to leave, so moving about out there says nothing at all.
        chart.Track(new Point(chart.PlotBounds.Right - 3, chart.PlotBounds.Top + 2));

        Assert.Single(exited);
    }

    [Fact]
    public void A_click_is_a_press_a_release_and_a_click()
    {
        var (chart, series) = Chart();
        var said = new List<string>();

        chart.ItemPointerPressed += (_, e) => said.Add($"press {e.ChartItem!.Value.Index}");
        chart.ItemPointerReleased += (_, e) => said.Add($"release {e.ChartItem!.Value.Index}");
        chart.ItemClick += (_, e) => said.Add($"click {e.ChartItem!.Value.Index}");
        chart.ItemDoubleClick += (_, _) => said.Add("double");

        var at = Position(series, 2);

        chart.Press(at);
        chart.Release(at, KeyModifiers.None);

        Assert.Equal(["press 2", "release 2", "click 2"], said);

        // The platform says when a click is the second of two, and the chart says so too rather than timing it.
        chart.Press(at, clickCount: 2);
        chart.Release(at, KeyModifiers.None);

        Assert.Equal("double", said[^1]);
    }

    [Fact]
    public void A_drag_over_a_record_is_not_a_click()
    {
        var (chart, series) = Chart(selection: SelectionMode.Single);
        var said = new List<string>();

        chart.ItemPointerPressed += (_, _) => said.Add("press");
        chart.ItemPointerReleased += (_, _) => said.Add("release");
        chart.ItemClick += (_, _) => said.Add("click");

        var at = Position(series, 1);

        chart.Press(at);
        chart.Release(at, KeyModifiers.None, clicked: false);

        // The press did happen on that record, and so did the release; what did not happen is a click, because
        // the press turned into a pan - and nothing was selected either.
        Assert.Equal(["press", "release"], said);
        Assert.Empty(chart.SelectedItems);
    }

    [Fact]
    public void The_selection_is_announced_when_it_changes_and_not_when_it_does_not()
    {
        var (chart, series) = Chart(selection: SelectionMode.Multiple);
        var announced = new List<int[]>();

        chart.SelectionChanged += (_, e) => announced.Add([.. e.Selection.Select(item => item.Index)]);

        chart.Release(Position(series, 1), KeyModifiers.None);

        Assert.Equal([[1]], announced);

        // The same record again: the selection is what it was, so there is nothing to say.
        chart.Release(Position(series, 1), KeyModifiers.None);

        Assert.Single(announced);

        // Control turned the caret and left the selection alone, which is also not a change.
        chart.Press(Key.Right, KeyModifiers.Control);

        Assert.Single(announced);
        Assert.Equal(2, chart.Caret!.Value.Index);

        // Adding one is a change, and letting go of everything is another.
        chart.Release(Position(series, 2), KeyModifiers.Control);

        Assert.Equal([1, 2], announced[^1]);

        chart.Release(Pixel(chart, 3, 1), KeyModifiers.None);

        Assert.Empty(announced[^1]);
        Assert.Equal(3, announced.Count);
    }

    [Fact]
    public void A_host_can_bind_a_command_instead_of_subscribing()
    {
        var (chart, series) = Chart(selection: SelectionMode.Single);
        var clicked = new Recording();
        var changed = new Recording();

        chart.ItemClickCommand = clicked;
        chart.SelectionChangedCommand = changed;

        chart.Release(Position(series, 1), KeyModifiers.None);

        Assert.Equal(1, clicked.Runs);
        Assert.Equal(1, changed.Runs);

        // The record came through as the caller's own object, which is the whole point of carrying the hit.
        var args = Assert.IsType<ChartItemEventArgs>(clicked.Last);

        Assert.Equal("b", Assert.IsType<Reading>(args.Item).Name);
        Assert.Equal(1, args.ChartItem!.Value.Index);

        // And a command that says it cannot run is not run.
        clicked.CanRun = false;
        chart.Release(Position(series, 2), KeyModifiers.None);

        Assert.Equal(1, clicked.Runs);
        Assert.Equal(2, changed.Runs);
    }

    /// <summary>A command that remembers what it was asked to do.</summary>
    private sealed class Recording : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanRun { get; set; } = true;

        public int Runs { get; private set; }

        public object? Last { get; private set; }

        public bool CanExecute(object? parameter) => CanRun;

        public void Execute(object? parameter)
        {
            Runs++;
            Last = parameter;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Where a record is drawn, which is where a pointer has to be to be on it.</summary>
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

    /// <summary>A chart with three named readings on it, laid out.</summary>
    /// <param name="selection">How much may be selected.</param>
    /// <returns>The chart and its series.</returns>
    private static (ChartProbe Chart, LineSeries<Reading> Series) Chart(
        SelectionMode selection = SelectionMode.None)
    {
        var series = new LineSeries<Reading>
        {
            ItemsSource = [new Reading("a", 1), new Reading("b", 2), new Reading("c", 3)],
            XSelector = reading => reading.Value,
            YSelector = reading => reading.Value,
            Stroke = Avalonia.Media.Brushes.DarkOrange,
        };

        var chart = new ChartProbe
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 4 },
            SelectionMode = selection,
        };

        chart.Series.Add(series);
        chart.Measure(new Size(400, 200));
        chart.Arrange(new Rect(0, 0, 400, 200));
        return (chart, series);
    }

    /// <summary>Something a caller handed over, so a test can see it come back.</summary>
    private sealed record Reading(string Name, double Value);

    /// <summary>A chart that can be driven the way its handlers drive it.</summary>
    private sealed class ChartProbe : CartesianChart
    {
        public void Track(Point at) => TrackPointer(at);

        public void Press(Point at, int clickCount = 1) => PressAt(at, clickCount);

        public void Release(Point at, KeyModifiers modifiers, bool clicked = true) =>
            ReleaseAt(at, modifiers, clicked);

        public bool Press(Key key, KeyModifiers modifiers) => Navigate(key, modifiers);
    }
}
