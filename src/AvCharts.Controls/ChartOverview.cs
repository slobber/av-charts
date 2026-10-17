using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using AvCharts.Axes;
using AvCharts.Data;
using AvCharts.Series;
using AvCharts.Themes;

namespace AvCharts.Controls;

/// <summary>
/// All of the readings at once, with the window that is on screen drawn over them: a picture to pick a window from
/// rather than only a bar to move one with.
/// </summary>
/// <remarks>
/// <para>
/// The scroll bar's bigger sibling, and a control the host places itself in the same way. Where
/// <see cref="ChartScrollBar"/> says only how much of the readings is on screen and where, this draws the readings
/// themselves - each series as a line over the whole recording - and leaves the window as a rectangle lying on top
/// of them. So it answers the question a scroll bar cannot: not "how far along am I" but "what is there to go and
/// look at", which is what a reader picks a window by.
/// </para>
/// <para>
/// The window can be taken by its middle and dragged along the readings, or by either end and stretched - which is
/// the whole of zooming - and a press away from it puts it there. Moving it is <see cref="ChartBase.PanBy"/> and
/// stretching it is <see cref="ChartBase.ZoomBy(double, double, Point)"/>, the two calls a wheel and a drag on the
/// plot already make, so both scale modes are answered without a case of their own here.
/// </para>
/// <para>
/// What it draws is a line for every series, whatever shape that series is on the chart: an overview is the shape
/// of a recording rather than a smaller copy of the picture, and a hundred columns of candles in forty pixels
/// would be a grey smear. Each series is drawn in the colour the chart gave it, and a hidden series is not drawn.
/// </para>
/// </remarks>
public class ChartOverview : Control
{
    /// <summary>How thick the panel is when the host leaves it to the overview to say, in logical pixels.</summary>
    /// <remarks>
    /// Taller than a scroll bar, since it carries a picture rather than a handle: forty is enough for a shape to
    /// read as one, and a host that wants more says so with the layout.
    /// </remarks>
    private const double DefaultThickness = 40;

    /// <summary>How close to an end of the window a press has to be to be taking hold of that end.</summary>
    private const double EdgeGrab = 4;

    /// <summary>The chart whose window the overview moves.</summary>
    public static readonly StyledProperty<ChartBase?> ChartProperty =
        AvaloniaProperty.Register<ChartOverview, ChartBase?>(nameof(Chart));

    /// <summary>Which way the overview runs, and so which of the chart's two dimensions it is about.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<ChartOverview, Orientation>(nameof(Orientation), Orientation.Horizontal);

    /// <summary>The brush the panel behind the readings is filled with.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ChartOverview, IBrush?>(nameof(Background));

    /// <summary>The brush the window is filled with.</summary>
    public static readonly StyledProperty<IBrush?> WindowFillProperty =
        AvaloniaProperty.Register<ChartOverview, IBrush?>(nameof(WindowFill));

    /// <summary>The pen the window is drawn around with.</summary>
    public static readonly StyledProperty<Pen?> WindowPenProperty =
        AvaloniaProperty.Register<ChartOverview, Pen?>(nameof(WindowPen));

    /// <summary>Whether the window is narrower than the readings, so that there is anything to pick.</summary>
    public static readonly DirectProperty<ChartOverview, bool> CanScrollProperty =
        AvaloniaProperty.RegisterDirect<ChartOverview, bool>(nameof(CanScroll), overview => overview._canScroll);

    private ChartBase? _watched;
    private bool _canScroll;
    private bool _dragging;
    private Grab _grab;
    private double _taken;
    private Cursor? _cursorBeforeDrag;

    /// <summary>Which part of the window a drag has hold of.</summary>
    private enum Grab
    {
        /// <summary>Nothing.</summary>
        None,

        /// <summary>The middle: the window moves.</summary>
        Window,

        /// <summary>The low end: the window is stretched from its start.</summary>
        Start,

        /// <summary>The high end: the window is stretched from its end.</summary>
        End,
    }

    /// <summary>The chart whose window the overview moves; null, and it draws nothing at all.</summary>
    public ChartBase? Chart
    {
        get => GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    /// <summary>
    /// Which way the overview runs - and so which dimension of the chart it is about: across for the horizontal
    /// one, up for the vertical one, as the two axes are laid out.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>The brush the panel behind the readings is filled with.</summary>
    /// <remarks>The token behind it is <see cref="ChartTokens.OverviewBackground"/>.</remarks>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>The brush the window is filled with.</summary>
    /// <remarks>The token behind it is <see cref="ChartTokens.OverviewWindowFill"/>.</remarks>
    public IBrush? WindowFill
    {
        get => GetValue(WindowFillProperty);
        set => SetValue(WindowFillProperty, value);
    }

    /// <summary>The pen the window is drawn around with.</summary>
    /// <remarks>The token behind it is <see cref="ChartTokens.OverviewWindowStroke"/>.</remarks>
    public Pen? WindowPen
    {
        get => GetValue(WindowPenProperty);
        set => SetValue(WindowPenProperty, value);
    }

    /// <summary>
    /// Whether the window is narrower than the readings, so that there is anything to pick; false when there is no
    /// chart, no axis on that dimension, or nothing on it.
    /// </summary>
    public bool CanScroll => _canScroll;

    /// <summary>Starts a drag at a point, in the overview's own coordinates.</summary>
    /// <param name="at">Where the press landed.</param>
    /// <remarks>
    /// <para>
    /// Within a few pixels of an end, the press has taken hold of that end and the drag stretches the window from
    /// there; inside the window it has taken hold of the middle, so the window does not jump under the hand;
    /// anywhere else it is a reader asking to go where they pressed, and the window is put there with its middle on
    /// the pointer.
    /// </para>
    /// <para>
    /// This and the two below are what the pointer handlers call, and they are the rules rather than the routing: a
    /// host driving an overview of its own - from a keyboard, a remote, a test - calls these.
    /// </para>
    /// </remarks>
    protected void DragStart(Point at)
    {
        var (start, length, has) = Window();

        if (!has)
        {
            return;
        }

        var along = Along(at);

        _dragging = true;
        _cursorBeforeDrag = Cursor;
        Cursor = new Cursor(StandardCursorType.SizeAll);

        if (Math.Abs(along - start) <= EdgeGrab)
        {
            _grab = Grab.Start;
            _taken = 0;
            return;
        }

        if (Math.Abs(along - (start + length)) <= EdgeGrab)
        {
            _grab = Grab.End;
            _taken = 0;
            return;
        }

        _grab = Grab.Window;
        _taken = along >= start && along <= start + length ? along - start : length / 2;

        // A press away from the window is a reader asking to go there, and the drag carries on from there.
        DragMove(at);
    }

    /// <summary>Carries a drag on to a point, moving the window or stretching the end it has hold of.</summary>
    /// <param name="at">Where the pointer is now, in the overview's own coordinates.</param>
    protected void DragMove(Point at)
    {
        if (!_dragging || Chart is not { } chart || !Ranges(out var readings, out var window) || window.IsEmpty)
        {
            return;
        }

        var (_, track) = Track();

        if (!(track > 0) || !(readings.Length > 0))
        {
            return;
        }

        // The track is the whole of the readings, so a distance along it is a fraction of them - and the window is
        // moved in the chart's own pixels, which is the one thing PanBy takes.
        var place = readings.Minimum + (Along(at) / track * readings.Length);

        if (_grab == Grab.Window)
        {
            var wanted = Math.Clamp(
                place - (_taken / track * readings.Length),
                readings.Minimum,
                Math.Max(readings.Minimum, readings.Maximum - window.Length));
            var moved = wanted - window.Minimum;

            if (!(moved != 0))
            {
                return;
            }

            var pixels = moved * (Span(chart) / window.Length);
            var horizontal = Orientation == Orientation.Horizontal;

            chart.PanBy(horizontal ? -pixels : 0, horizontal ? 0 : pixels);
            return;
        }

        // Stretching an end: the other end stays where it is, which is what makes this a zoom about that end. Two
        // pixels of the track is as short as the window may be dragged, so that it cannot be closed to nothing.
        var shortest = 2 / track * readings.Length;
        var start = _grab == Grab.Start ? Math.Min(place, window.Maximum - shortest) : window.Minimum;
        var end = _grab == Grab.End ? Math.Max(place, window.Minimum + shortest) : window.Maximum;
        // Zooming says how much bigger the readings are drawn, so a window being stretched is a *smaller* factor:
        // this is the reciprocal of how much longer the window is to become.
        var factor = window.Length / (end - start);

        if (!(factor > 0) || double.IsInfinity(factor) || OnChart(chart, window, _grab == Grab.Start ? end : start)
            is not { } anchor)
        {
            return;
        }

        if (Orientation == Orientation.Horizontal)
        {
            chart.ZoomBy(factor, 1, anchor);
        }
        else
        {
            chart.ZoomBy(1, factor, anchor);
        }
    }

    /// <summary>Ends a drag.</summary>
    protected void DragEnd()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        _grab = Grab.None;
        Cursor = _cursorBeforeDrag;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var thickness = Math.Min(DefaultThickness, Cross(availableSize));

        return Orientation == Orientation.Horizontal ? new Size(0, thickness) : new Size(thickness, 0);
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // The tokens land on the overview's brushes and pen the way they land on a chart's inks.
        ResourceThemes.Apply(this, typeof(ChartOverview));
        Watch(Chart);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        Watch(null);
        base.OnDetachedFromLogicalTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ChartProperty)
        {
            Watch(Chart);
        }
        else if (change.Property == OrientationProperty
            || change.Property == BoundsProperty
            || change.Property == BackgroundProperty
            || change.Property == WindowFillProperty
            || change.Property == WindowPenProperty)
        {
            Refresh();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);

        if (Background is { } panel)
        {
            context.FillRectangle(panel, bounds);
        }

        if (Chart is not { } chart
            || !Ranges(out var readings, out var window)
            || chart.Transform.GetAxis(ChartDimension.X) is not { } across
            || chart.Transform.GetAxis(ChartDimension.Y) is not { } up
            || chart.Transform.DataRange(across) is not { } xRange
            || chart.Transform.DataRange(up) is not { } yRange
            || xRange.IsEmpty
            || yRange.IsEmpty)
        {
            return;
        }

        var (start, length) = Track();

        if (!(length > 0))
        {
            return;
        }

        Read(context, chart, bounds, start, length, xRange, yRange);

        var box = WindowAt(start, length, readings, window);

        if (WindowFill is { } fill)
        {
            context.FillRectangle(fill, box);
        }

        if (WindowPen is { Thickness: > 0 } pen)
        {
            context.DrawRectangle(null, pen, box.Deflate(pen.Thickness / 2));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        DragStart(e.GetPosition(this));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragging)
        {
            DragMove(e.GetPosition(this));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragging)
        {
            DragEnd();
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    /// <summary>Paints every visible series as a line over the whole of the readings.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="chart">The chart being described.</param>
    /// <param name="bounds">The box the overview is drawn in.</param>
    /// <param name="start">Where the track starts along the overview.</param>
    /// <param name="length">How long the track is.</param>
    /// <param name="xRange">What the readings cover across.</param>
    /// <param name="yRange">What they cover up.</param>
    private void Read(
        DrawingContext context,
        ChartBase chart,
        Rect bounds,
        double start,
        double length,
        AxisUnitRange xRange,
        AxisUnitRange yRange)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(Orientation == Orientation.Horizontal ? length : bounds.Height));

        foreach (var series in chart.Series)
        {
            if (!series.Visible || series.Color is not { } colour)
            {
                continue;
            }

            var xs = series.Units(ChartDimension.X);
            var ys = series.Units(ChartDimension.Y);
            var count = Math.Min(xs.Length, ys.Length);

            if (count < Sampler.MinimumPoints)
            {
                continue;
            }

            var geometry = new StreamGeometry();

            using (var figures = geometry.Open())
            {
                foreach (var run in Runs(xs, ys, count, bounds, start, length, xRange, yRange))
                {
                    var drawn = Sampler.Reduce(run, columns, SamplingMode.MinMax);

                    if (drawn.Count < Sampler.MinimumPoints)
                    {
                        continue;
                    }

                    figures.BeginFigure(drawn[0], false);

                    for (var index = 1; index < drawn.Count; index++)
                    {
                        figures.LineTo(drawn[index]);
                    }

                    figures.EndFigure(false);
                }
            }

            context.DrawGeometry(null, new Pen(colour, 1), geometry);
        }
    }

    /// <summary>Maps the readings onto the overview, in runs that a gap in them breaks.</summary>
    /// <param name="xs">The values across, in unit space.</param>
    /// <param name="ys">The values up, in unit space.</param>
    /// <param name="count">How many of them there are.</param>
    /// <param name="bounds">The box the overview is drawn in.</param>
    /// <param name="start">Where the track starts along the overview.</param>
    /// <param name="length">How long the track is.</param>
    /// <param name="xRange">What the readings cover across.</param>
    /// <param name="yRange">What they cover up.</param>
    /// <returns>The runs, in data order.</returns>
    /// <remarks>
    /// A record with no value on either dimension ends the run it was in, as it does on the chart: a gap in the
    /// readings is a gap in the shape.
    /// </remarks>
    private List<List<Point>> Runs(
        ReadOnlySpan<double> xs,
        ReadOnlySpan<double> ys,
        int count,
        Rect bounds,
        double start,
        double length,
        AxisUnitRange xRange,
        AxisUnitRange yRange)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var runs = new List<List<Point>>();
        var run = new List<Point>();

        for (var index = 0; index < count; index++)
        {
            if (double.IsNaN(xs[index]) || double.IsNaN(ys[index]))
            {
                if (run.Count > 0)
                {
                    runs.Add(run);
                    run = [];
                }

                continue;
            }

            var across = bounds.Height - (yRange.Normalize(ys[index]) * bounds.Height);
            var along = start + (xRange.Normalize(xs[index]) * length);

            run.Add(horizontal ? new Point(along, across) : new Point(across, bounds.Height - along));
        }

        if (run.Count > 0)
        {
            runs.Add(run);
        }

        return runs;
    }

    /// <summary>Where the window is drawn, in the overview's own coordinates.</summary>
    /// <param name="start">Where the track starts along the overview.</param>
    /// <param name="length">How long the track is.</param>
    /// <param name="readings">What the readings cover.</param>
    /// <param name="window">What is on screen.</param>
    /// <returns>The rectangle.</returns>
    private Rect WindowAt(double start, double length, AxisUnitRange readings, AxisUnitRange window)
    {
        var low = readings.Normalize(window.Minimum) * length;
        var high = readings.Normalize(window.Maximum) * length;

        return Orientation == Orientation.Horizontal
            ? new Rect(start + low, 0, Math.Max(1, high - low), Bounds.Height)
            : new Rect(0, Bounds.Height - start - high, Bounds.Width, Math.Max(1, high - low));
    }

    /// <summary>Where the window is along the track and how long it is, in the overview's own coordinates.</summary>
    /// <returns>Its start along the track, its length, and whether there is anything to draw.</returns>
    /// <remarks>
    /// Measured from the start of the track rather than from the end of the panel, which is the same way round as
    /// <see cref="Along"/> - the two are compared by every press, so a pixel measured from one end and a rule
    /// measured from the other is a press that never lands where it looks as though it does.
    /// </remarks>
    private (double Start, double Length, bool Has) Window()
    {
        if (!Ranges(out var readings, out var window) || readings.IsEmpty)
        {
            return (0, 0, false);
        }

        var (_, length) = Track();
        var low = readings.Normalize(window.Minimum) * length;
        var high = readings.Normalize(window.Maximum) * length;

        return (low, Math.Max(1, high - low), true);
    }

    /// <summary>Where a value on the dimension falls on the chart itself, as the dimension is laid out there.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="window">What is on screen on that dimension.</param>
    /// <param name="value">The value, in unit space.</param>
    /// <returns>The point, or null when the chart has no room to draw in yet.</returns>
    /// <remarks>
    /// The chart's window covers its plot exactly, so a value's place among the readings is its place along the
    /// plot - which is all a zoom about it needs: where it is, not what it is worth in pixels.
    /// </remarks>
    private Point? OnChart(ChartBase chart, AxisUnitRange window, double value)
    {
        var plot = chart.PlotBounds;

        if (!(plot.Width > 0) || !(plot.Height > 0))
        {
            return null;
        }

        return Orientation == Orientation.Horizontal
            ? new Point(plot.Left + (window.Normalize(value) * plot.Width), plot.Center.Y)
            : new Point(plot.Center.X, plot.Bottom - (window.Normalize(value) * plot.Height));
    }

    /// <summary>Starts listening to a chart instead of the one being listened to, if it is a different one.</summary>
    /// <param name="chart">The chart, or null to listen to none.</param>
    private void Watch(ChartBase? chart)
    {
        if (ReferenceEquals(_watched, chart))
        {
            return;
        }

        if (_watched is { } previous)
        {
            previous.VisibleRangeChanged -= OnWindowChanged;
            previous.LayoutUpdated -= OnChartLaidOut;
        }

        _watched = chart;

        if (chart is not null)
        {
            chart.VisibleRangeChanged += OnWindowChanged;
            chart.LayoutUpdated += OnChartLaidOut;
        }

        Refresh();
    }

    /// <summary>Answers a chart's window moving.</summary>
    /// <param name="sender">The chart.</param>
    /// <param name="e">Which dimension moved, and to what.</param>
    private void OnWindowChanged(object? sender, VisibleRangeChangedEventArgs e)
    {
        if (e.Dimension == Dimension)
        {
            Refresh();
        }
    }

    /// <summary>Answers a chart laying itself out, which is when the readings or the room may have changed.</summary>
    /// <param name="sender">The chart.</param>
    /// <param name="e">Unused.</param>
    private void OnChartLaidOut(object? sender, EventArgs e) => Refresh();

    /// <summary>Works out whether there is anything to pick, and asks for a repaint.</summary>
    private void Refresh()
    {
        var can = Ranges(out var readings, out var window)
            && !readings.IsEmpty
            && !window.IsEmpty
            && window.Length < readings.Length;

        if (can != _canScroll)
        {
            SetAndRaise(CanScrollProperty, ref _canScroll, can);
        }

        InvalidateVisual();
    }

    /// <summary>What the readings cover and what the window covers, both in unit space.</summary>
    /// <param name="readings">What the readings cover.</param>
    /// <param name="window">What is on screen.</param>
    /// <returns><see langword="false"/> when there is nothing to measure.</returns>
    /// <remarks>
    /// The readings are stretched by the window when the window reaches past them, which panning off the end of a
    /// recording does, so that the window never hangs out of the picture: part of what is on screen then has no
    /// readings in it, and the overview says so.
    /// </remarks>
    private bool Ranges(out AxisUnitRange readings, out AxisUnitRange window)
    {
        readings = AxisUnitRange.Empty;
        window = AxisUnitRange.Empty;

        if (Chart is not { } chart
            || chart.Transform.GetAxis(Dimension) is not { } axis
            || chart.Transform.DataRange(axis) is not { } covered)
        {
            return false;
        }

        window = chart.Transform.GetLayout(axis).UnitRange;
        readings = window.IsEmpty
            ? covered
            : new AxisUnitRange(
                Math.Min(covered.Minimum, window.Minimum),
                Math.Max(covered.Maximum, window.Maximum));

        return true;
    }

    /// <summary>The dimension the overview is about: across for a horizontal one, up for a vertical one.</summary>
    private ChartDimension Dimension =>
        Orientation == Orientation.Horizontal ? ChartDimension.X : ChartDimension.Y;

    /// <summary>How long the overview is, along the way it runs, in pixels.</summary>
    private double TrackLength() => Orientation == Orientation.Horizontal ? Bounds.Width : Bounds.Height;

    /// <summary>How long the plot is along that dimension, in pixels - what a pan is measured in.</summary>
    /// <param name="chart">The chart.</param>
    private double Span(ChartBase chart) =>
        Orientation == Orientation.Horizontal ? chart.PlotBounds.Width : chart.PlotBounds.Height;

    /// <summary>
    /// The stretch of the overview that stands for the readings: what is left of it once the same room is taken at
    /// each end as the chart leaves around its plot.
    /// </summary>
    /// <returns>Where the track starts, and how long it is.</returns>
    /// <remarks>
    /// The same rule the scroll bar keeps, and kept for the same reason: an overview runs the width of the page it
    /// is on, and a chart spends the first fifty-odd pixels of its own width on the labels down the side of the
    /// plot. Sharing the rule is also what makes the two line up when a host stacks them - the window a reader picks
    /// in one is the stretch of the chart drawn under the other.
    /// </remarks>
    private (double Start, double Length) Track()
    {
        var full = TrackLength();

        if (Chart is not { } chart)
        {
            return (0, full);
        }

        var plot = chart.PlotBounds;
        var before = Orientation == Orientation.Horizontal ? plot.Left : chart.Bounds.Height - plot.Bottom;
        var after = Orientation == Orientation.Horizontal ? chart.Bounds.Width - plot.Right : plot.Top;
        var start = Math.Clamp(before, 0, full / 2);

        return (start, Math.Max(0, full - start - Math.Clamp(after, 0, full / 2)));
    }

    /// <summary>How long a size is across the way the overview runs.</summary>
    /// <param name="size">The size.</param>
    private double Cross(Size size) => Orientation == Orientation.Horizontal ? size.Height : size.Width;

    /// <summary>Where a point is along the overview, counted from the low end of the readings.</summary>
    /// <param name="at">The point, in the overview's own coordinates.</param>
    /// <returns>The distance from the low end, in pixels.</returns>
    private double Along(Point at) =>
        (Orientation == Orientation.Horizontal ? at.X : Bounds.Height - at.Y) - Track().Start;
}
