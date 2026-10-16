using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using AvCharts.Axes;
using AvCharts.Data;
using AvCharts.Themes;

namespace AvCharts.Controls;

/// <summary>
/// A bar along one side of a chart that says where the window is inside the readings, and drags it along them.
/// </summary>
/// <remarks>
/// <para>
/// A control the host places itself, as a legend is, and for the same reason: how much of the chart is on screen
/// is the page's business, so the bar goes in the cell the page leaves for it - under the plot for the readings
/// that run across it, beside it for the ones that run up it - and pointing it at a chart is the whole of the
/// wiring.
/// </para>
/// <para>
/// The bar says two things and does one. It says how much of the readings fits on screen, which is the length of
/// its thumb against the length of its track, and where that stretch sits among them, which is where the thumb
/// is. What it does is move that stretch: the thumb is dragged, and the window follows it. It does not read the
/// readings itself and it does not redraw the chart - it turns a drag into logical pixels and hands them to
/// <see cref="ChartBase.PanBy"/>, the one path the wheel, a drag on the plot and a host's own code already
/// share, so a chart panned by its bar is panned the way everything else pans it and the two scale modes need no
/// case of their own here.
/// </para>
/// <para>
/// The readings it measures the window against are the ones declared to the axis, not the whole of the host's
/// data: a series that is hidden declares nothing, which is exactly right - there is no bar to drag past the end
/// of what is drawn.
/// </para>
/// <para>
/// A track and a thumb, and nothing else on it: no arrow at either end, by request. The ends of a chart's bar
/// are the ends of the readings rather than a page to step to, so a pair of buttons there would be two more
/// things to click and no way to get anywhere the drag does not already go. A press on the track is what a
/// reader who wants a long move uses.
/// </para>
/// </remarks>
public class ChartScrollBar : Control
{
    /// <summary>How thick the bar is when the host leaves it to the bar to say, in logical pixels.</summary>
    /// <remarks>
    /// A bar dropped into a row of a grid with no height of its own would otherwise take none: this is the room
    /// it asks for, and a host that wants a fatter bar says so with the layout, which is where a size belongs.
    /// Eight, which is thinner than a desktop scrollbar: this sits under a chart rather than down the side of a
    /// window, and a thin rule is what a chart's own furniture is drawn at.
    /// </remarks>
    private const double DefaultThickness = 8;

    /// <summary>The chart whose window the bar moves.</summary>
    public static readonly StyledProperty<ChartBase?> ChartProperty =
        AvaloniaProperty.Register<ChartScrollBar, ChartBase?>(nameof(Chart));

    /// <summary>Which way the bar runs, and so which of the chart's two dimensions it is about.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<ChartScrollBar, Orientation>(nameof(Orientation), Orientation.Horizontal);

    /// <summary>The brush the track is painted with.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ChartScrollBar, IBrush?>(nameof(Background));

    /// <summary>The brush the thumb is painted with.</summary>
    public static readonly StyledProperty<IBrush?> ThumbBrushProperty =
        AvaloniaProperty.Register<ChartScrollBar, IBrush?>(nameof(ThumbBrush));

    /// <summary>The brush the thumb is painted with while the pointer is over it.</summary>
    /// <remarks>Nothing means the thumb keeps <see cref="ThumbBrush"/>: a host that does not care need not say.</remarks>
    public static readonly StyledProperty<IBrush?> ThumbHoverBrushProperty =
        AvaloniaProperty.Register<ChartScrollBar, IBrush?>(nameof(ThumbHoverBrush));

    /// <summary>
    /// How short the thumb may get, in logical pixels; sixteen.
    /// </summary>
    /// <remarks>
    /// A window a thousandth of the readings is a thumb a thousandth of the track, which is a hairline nothing can
    /// be dragged by. Clamping the thumb keeps it takeable, and the bar still moves the window by the whole of
    /// what it is dragged - the thumb's own travel is what the drag is measured against, so a thumb that has been
    /// clamped is a coarser handle rather than a lying one.
    /// </remarks>
    public static readonly StyledProperty<double> MinThumbLengthProperty =
        AvaloniaProperty.Register<ChartScrollBar, double>(nameof(MinThumbLength), 16);

    /// <summary>Whether there is anything to scroll through.</summary>
    public static readonly DirectProperty<ChartScrollBar, bool> CanScrollProperty =
        AvaloniaProperty.RegisterDirect<ChartScrollBar, bool>(nameof(CanScroll), bar => bar._canScroll);

    private ChartBase? _watched;
    private bool _canScroll;
    private bool _dragging;
    private bool _overThumb;
    private double _grab;
    private Cursor? _cursorBeforeDrag;

    /// <summary>The chart whose window the bar moves; null, and the bar draws its track and nothing else.</summary>
    public ChartBase? Chart
    {
        get => GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    /// <summary>
    /// Which way the bar runs - and so which dimension of the chart it moves: across for the horizontal one, up
    /// for the vertical one, as the two axes are laid out.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>The brush the track is painted with.</summary>
    /// <remarks>The token behind it is <see cref="ChartTokens.ScrollbarTrack"/>.</remarks>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>The brush the thumb is painted with.</summary>
    /// <remarks>The token behind it is <see cref="ChartTokens.ScrollbarThumb"/>.</remarks>
    public IBrush? ThumbBrush
    {
        get => GetValue(ThumbBrushProperty);
        set => SetValue(ThumbBrushProperty, value);
    }

    /// <summary>The brush the thumb is painted with while the pointer is over it.</summary>
    /// <remarks>The token behind it is <see cref="ChartTokens.ScrollbarThumbHover"/>.</remarks>
    public IBrush? ThumbHoverBrush
    {
        get => GetValue(ThumbHoverBrushProperty);
        set => SetValue(ThumbHoverBrushProperty, value);
    }

    /// <summary>How short the thumb may get, in logical pixels.</summary>
    public double MinThumbLength
    {
        get => GetValue(MinThumbLengthProperty);
        set => SetValue(MinThumbLengthProperty, value);
    }

    /// <summary>
    /// Whether the window is narrower than the readings, so that there is anything to scroll through; false when
    /// there is no chart, no axis on that dimension, or no data.
    /// </summary>
    /// <remarks>For a host that would rather hide the bar than show one that cannot move: there is nothing to drag.</remarks>
    public bool CanScroll => _canScroll;

    /// <summary>Starts a drag at a point, in the bar's own coordinates.</summary>
    /// <param name="at">Where the press landed.</param>
    /// <remarks>
    /// <para>
    /// A press on the thumb takes hold of it where it was pressed, so that the thumb does not jump under the hand.
    /// A press on the track anywhere else is a jump: the window is put where the pointer is, which is what a
    /// reader asks for by pressing a navigator away from its handle - they are not asking to move by a page, they
    /// are asking to go there - and the drag then carries on from there.
    /// </para>
    /// <para>
    /// This and the two below are what the pointer handlers call, and they are the rules rather than the routing:
    /// a host driving a bar of its own - from a keyboard, a remote, a test - calls these.
    /// </para>
    /// </remarks>
    protected void DragStart(Point at)
    {
        var thumb = Thumb();

        if (!thumb.Has)
        {
            return;
        }

        var along = Along(at);

        _dragging = true;
        _grab = along >= thumb.Start && along <= thumb.Start + thumb.Length ? along - thumb.Start : thumb.Length / 2;
        _cursorBeforeDrag = Cursor;
        Cursor = new Cursor(StandardCursorType.SizeAll);

        DragMove(at);
    }

    /// <summary>Carries a drag on to a point, moving the chart's window to where the thumb now is.</summary>
    /// <param name="at">Where the pointer is now, in the bar's own coordinates.</param>
    /// <remarks>
    /// The window is moved in the chart's own pixels by the difference between where the thumb is and where the
    /// pointer wants it, rather than by how far the pointer moved: a drag that runs past the end of the track
    /// stops at the end rather than accumulating a distance it cannot spend, and a drag that comes back lands
    /// where it is, not where the sum of its movements says.
    /// </remarks>
    protected void DragMove(Point at)
    {
        var thumb = Thumb();

        if (!_dragging || Chart is not { } chart || !Ranges(out var readings, out var window) || window.IsEmpty)
        {
            return;
        }

        // Against the track rather than against the bar: the thumb travels the plot's own stretch of it, which is
        // where a drag can go and therefore what a drag is measured by.
        var travel = Track().Length - thumb.Length;
        var span = readings.Length - window.Length;

        if (!(travel > 0) || !(span > 0))
        {
            return;
        }

        var wanted = Math.Clamp(Along(at) - _grab, 0, travel);
        var moved = readings.Minimum + (wanted / travel * span) - window.Minimum;

        if (!(moved != 0))
        {
            return;
        }

        // A value is worth as many pixels on the plot as the window is long in units; the sign is the one PanBy
        // documents - dragging the content to the right shows earlier readings - and it is not the same sign on
        // both dimensions, because a screen's y grows downwards while a value grows upwards.
        var pixels = moved * (PlotLength(chart) / window.Length);
        var horizontal = Orientation == Orientation.Horizontal;

        chart.PanBy(horizontal ? -pixels : 0, horizontal ? 0 : pixels);
    }

    /// <summary>Ends a drag.</summary>
    protected void DragEnd()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Cursor = _cursorBeforeDrag;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // Across its run the bar takes whatever the page gives it; the other way it asks for a bar's thickness,
        // which is all a bar needs to be usable in a panel that has no opinion about the size of its children.
        var thickness = Math.Min(DefaultThickness, CrossLength(availableSize));

        return Orientation == Orientation.Horizontal ? new Size(0, thickness) : new Size(thickness, 0);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);

        // Laid out, the bar reads the chart again: this is also the moment a page re-arranges its children, which
        // is how the bar beside a chart hears about readings that were replaced, hidden or added - the chart's own
        // layout says so too, and this is the half that works when the two are arranged by hand.
        Refresh(Orientation == Orientation.Horizontal ? finalSize.Width : finalSize.Height);

        return arranged;
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // The tokens land on the bar's brushes the way they land on a chart's inks: one control theme keyed by the
        // type, found in whatever resources the bar is put among.
        ResourceThemes.Apply(this, typeof(ChartScrollBar));
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
            || change.Property == MinThumbLengthProperty
            || change.Property == BackgroundProperty
            || change.Property == ThumbBrushProperty
            || change.Property == ThumbHoverBrushProperty)
        {
            Refresh();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);

        if (Background is { } track)
        {
            context.FillRectangle(track, bounds);
        }

        // Worked out now rather than kept from the last time something moved: what the bar draws is a question
        // about the readings and the window, and both belong to the chart - so a bar that worked it out once and
        // held on to the answer would be a bar that is wrong the moment the chart is laid out, redrawn, or handed
        // a longer recording, without anything the bar listens for having happened.
        var thumb = Thumb();

        if (!thumb.Has)
        {
            return;
        }

        var (start, _) = Track();
        var box = Orientation == Orientation.Horizontal
            ? new Rect(start + thumb.Start, 0, thumb.Length, bounds.Height)
            : new Rect(0, bounds.Height - start - thumb.Start - thumb.Length, bounds.Width, thumb.Length);

        // A window that covers the whole of the readings has a thumb as long as its track, which is a bar drawn
        // solid: "all of it is on screen" said by the same shape that says how much is.
        if (((_overThumb ? ThumbHoverBrush : null) ?? ThumbBrush) is { } brush)
        {
            context.FillRectangle(brush, box);
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

        var at = e.GetPosition(this);

        if (_dragging)
        {
            DragMove(at);
            return;
        }

        var thumb = Thumb();
        var over = thumb.Has && Along(at) >= thumb.Start && Along(at) <= thumb.Start + thumb.Length;

        if (over != _overThumb)
        {
            _overThumb = over;
            InvalidateVisual();
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

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_overThumb)
        {
            _overThumb = false;
            InvalidateVisual();
        }
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

            // Everything that moves the window goes through the event above, and everything else that changes the
            // answer here - readings added, readings taken away, the chart resized - ends in a layout pass. Reading
            // a handful of numbers on one is cheaper than watching for all of them one at a time.
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

    /// <summary>Works out whether the bar can scroll at all, and asks for a repaint.</summary>
    private void Refresh() => Refresh(TrackLength());

    /// <summary>Works out whether the bar can scroll at all, and asks for a repaint.</summary>
    /// <param name="barLength">How long the bar is along the way it runs, in pixels.</param>
    /// <remarks>
    /// Called when something that could change the answer has happened: the window moved, the chart was laid out,
    /// or the bar itself changed. What it draws is worked out again when it draws, so this is about the one thing
    /// a host reads rather than draws - <see cref="CanScroll"/> - and about asking for the paint.
    /// </remarks>
    private void Refresh(double barLength)
    {
        var can = Thumb(barLength).CanScroll;

        if (can != _canScroll)
        {
            SetAndRaise(CanScrollProperty, ref _canScroll, can);
        }

        InvalidateVisual();
    }

    /// <summary>Where the thumb is along the track, and whether there is anywhere for it to go.</summary>
    /// <returns>
    /// Whether there is anything to draw, where the thumb starts from the low end of the readings, how long it is,
    /// and whether the window is narrower than they are.
    /// </returns>
    /// <remarks>
    /// Worked out from what the chart says each time it is asked rather than remembered, for the reason the thumb
    /// deserves the caution: the readings are the chart's, the window is the chart's, and a bar holding a stale
    /// copy of either draws a picture of a chart that no longer exists.
    /// </remarks>
    private (bool Has, double Start, double Length, bool CanScroll) Thumb()
        => Thumb(TrackLength());

    /// <summary>Where the thumb is on a bar of a given length, and whether there is anywhere for it to go.</summary>
    /// <param name="barLength">How long the bar is along the way it runs, in pixels.</param>
    /// <returns>
    /// Whether there is anything to draw, where the thumb starts from the low end of the readings, how long it is,
    /// and whether the window is narrower than they are.
    /// </returns>
    private (bool Has, double Start, double Length, bool CanScroll) Thumb(double barLength)
    {
        if (!Ranges(out var readings, out var window) || readings.IsEmpty)
        {
            return (false, 0, 0, false);
        }

        var track = Track(barLength).Length;

        // How much of the readings is on screen - the whole of them when there is nothing to scale into, which is
        // a window that says the readings are one value wide.
        var fraction = window.IsEmpty ? 1 : Math.Clamp(window.Length / readings.Length, 0, 1);
        var shortest = Math.Min(Math.Max(MinThumbLength, 0), track);
        var length = Math.Clamp(fraction * track, shortest, track);
        var travel = track - length;
        var span = readings.Length - (window.IsEmpty ? 0 : window.Length);
        var start = travel > 0 && span > 0
            ? Math.Clamp((window.Minimum - readings.Minimum) / span, 0, 1) * travel
            : 0;

        return (true, start, length, travel > 0 && span > 0);
    }

    /// <summary>What the readings cover and what the window covers, both in unit space.</summary>
    /// <param name="readings">What the readings cover.</param>
    /// <param name="window">What is on screen.</param>
    /// <returns><see langword="false"/> when there is nothing to measure.</returns>
    /// <remarks>
    /// The track is the readings, stretched by the window when the window reaches past them - which panning off
    /// the end of the readings does - so that the thumb never leaves its track and the bar says what is true: part
    /// of what is on screen has no readings in it.
    /// </remarks>
    private bool Ranges(out AxisUnitRange readings, out AxisUnitRange window)
    {
        readings = AxisUnitRange.Empty;
        window = AxisUnitRange.Empty;

        if (Chart is not { } chart || chart.Transform.GetAxis(Dimension) is not { } axis)
        {
            return false;
        }

        if (chart.Transform.DataRange(axis) is not { } covered)
        {
            return false;
        }

        window = chart.Transform.GetLayout(axis).UnitRange;

        if (window.IsEmpty)
        {
            readings = covered;
            return true;
        }

        readings = new AxisUnitRange(
            Math.Min(covered.Minimum, window.Minimum),
            Math.Max(covered.Maximum, window.Maximum));

        return true;
    }

    /// <summary>The dimension the bar is about: across for a horizontal bar, up for a vertical one.</summary>
    private ChartDimension Dimension =>
        Orientation == Orientation.Horizontal ? ChartDimension.X : ChartDimension.Y;

    /// <summary>How long the bar is, along the way it runs, in pixels.</summary>
    private double TrackLength() => Orientation == Orientation.Horizontal ? Bounds.Width : Bounds.Height;

    /// <summary>
    /// The stretch of the bar that stands for the readings: what is left of the bar once the same room is taken at
    /// each end of it as the chart leaves around its plot.
    /// </summary>
    /// <returns>Where the track starts along the bar, and how long it is.</returns>
    /// <remarks>
    /// A bar is as wide as the host makes it, and a chart spends the first fifty-odd pixels of its own width on the
    /// labels down the side of the plot. A track that ran the whole width of the bar would therefore say that the
    /// readings begin under the axis labels - so a reader who dragged the thumb to its left end would find the plot
    /// had not finished moving. The bar keeps the plot's own room at its ends instead, and what it draws runs from
    /// where the plot starts to where it ends.
    /// </remarks>
    private (double Start, double Length) Track() => Track(TrackLength());

    /// <summary>The same track, measured on a bar of a given length.</summary>
    /// <param name="full">How long the bar is, in the direction it runs.</param>
    /// <returns>Where the track starts along the bar, and how long it is.</returns>
    private (double Start, double Length) Track(double full)
    {
        if (Chart is not { } chart)
        {
            return (0, full);
        }

        var plot = chart.PlotBounds;
        var before = Orientation == Orientation.Horizontal ? plot.Left : chart.Bounds.Height - plot.Bottom;
        var after = Orientation == Orientation.Horizontal ? chart.Bounds.Width - plot.Right : plot.Top;

        // Half the bar at each end is as much room as the ends may take: a chart whose gutter is wider than the bar
        // is a page arranging two things that do not belong together, and the bar still has to be a bar.
        var start = Math.Clamp(before, 0, full / 2);

        return (start, Math.Max(0, full - start - Math.Clamp(after, 0, full / 2)));
    }

    /// <summary>How long one dimension of the plot is, in pixels - what a pan is measured in.</summary>
    /// <param name="chart">The chart.</param>
    private double PlotLength(ChartBase chart) =>
        Orientation == Orientation.Horizontal ? chart.PlotBounds.Width : chart.PlotBounds.Height;

    /// <summary>How long a size is across the way the bar runs.</summary>
    /// <param name="size">The size.</param>
    private double CrossLength(Size size) => Orientation == Orientation.Horizontal ? size.Height : size.Width;

    /// <summary>
    /// Where a point is along the bar, counted from the low end of the readings.
    /// </summary>
    /// <param name="at">The point, in the bar's own coordinates.</param>
    /// <returns>The distance from the low end, in pixels.</returns>
    /// <remarks>
    /// From the low end rather than from the top or the left, because the track is the readings and readings run
    /// upwards: a vertical bar is the value axis' own order laid on its side, so the bottom of the bar is the
    /// lowest reading and dragging the thumb down means looking at lower ones.
    /// </remarks>
    private double Along(Point at) =>
        (Orientation == Orientation.Horizontal ? at.X : Bounds.Height - at.Y) - Track().Start;
}
