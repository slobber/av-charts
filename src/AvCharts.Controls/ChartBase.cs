using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvCharts.Charts;
using AvCharts.Themes;

namespace AvCharts.Controls;

/// <summary>
/// The base of every chart control: it owns the transform, the layers, and the layout that turns the two into a
/// rectangle of plot.
/// </summary>
/// <remarks>
/// <para>
/// The control paints almost nothing itself. It arranges a small fixed set of layers - axes, series,
/// annotations - and gets out of the way, so a repaint of one series costs one layer rather than the chart.
/// </para>
/// <para>
/// Layout is where the axis labels are measured, because the plot rectangle depends on how much room they
/// need, and how many there are depends on the plot rectangle. It is worked out in passes until it stops
/// moving, with a hard limit so that a pathological axis cannot lock the layout up.
/// </para>
/// <para>
/// The inks are properties rather than resource lookups: the theme sets them (the control theme shipped in
/// <c>AvCharts.Themes</c>, or whatever a host puts under the same token), and the chart hands the axis ones
/// down to its axis layers. That way a chart paints in the colours of whatever it is put into, and a host that
/// overrides a token reskins every chart at once.
/// </para>
/// </remarks>
public abstract class ChartBase : Control
{
    /// <summary>How many times the plot is worked out before the answer is taken as it stands.</summary>
    private const int MaximumLayoutPasses = 4;

    private readonly List<ChartLayer> _layers = [];
    private readonly SeriesLayer _seriesLayer;
    private readonly Pen?[] _paletteStrokes = new Pen?[ChartTokens.PaletteSize];
    private readonly IBrush?[] _paletteFills = new IBrush?[ChartTokens.PaletteSize];
    private readonly Dictionary<ChartDimension, AxisUnitRange> _windows = [];
    private int _pumpIsPosted;
    private bool _isPanning;
    private bool _panHasStarted;
    private Point _panFrom;
    private Cursor? _cursorBeforePan;

    /// <summary>How far the pointer may move before a press counts as a drag, in logical pixels.</summary>
    /// <remarks>
    /// A hand is not steady, and without a threshold a click that moves by a pixel pans the chart - and the click
    /// itself never arrives at whatever wanted it.
    /// </remarks>
    private const double DragThreshold = 3;

    /// <summary>How much one notch of the wheel zooms.</summary>
    private const double WheelZoomPerNotch = 1.2;

    /// <summary>Creates a chart.</summary>
    protected ChartBase()
    {
        ClipToBounds = true;
        _seriesLayer = new SeriesLayer { Transform = Transform };
        AddLayer(_seriesLayer);
        Series.CollectionChanged += OnSeriesChanged;

        // A theme change - the variant, or a host swapping a token - is a repaint, not a rebuild: the same
        // layers and the same geometry, read again for the colours.
        ActualThemeVariantChanged += (_, _) => Dress();
        ResourcesChanged += (_, _) => Dress();
    }

    static ChartBase()
    {
        // The surface is the chart's own ink, and nothing else paints it, so changing any of it is a repaint.
        AffectsRender<ChartBase>(BackgroundProperty, BorderPenProperty, FocusRingProperty);

        // The axis inks are handed to the layers instead, which are visuals in their own right and repaint on
        // their own account.
        AxisLinePenProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyAxisInks());
        TickPenProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyAxisInks());
        GridLinePenProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyAxisInks());
        MinorGridLinePenProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyAxisInks());
        LabelBrushProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyAxisInks());
    }

    /// <summary>The transform every layer and series reads its positions from.</summary>
    public DataTransform Transform { get; } = new();

    /// <summary>The layer that paints the series.</summary>
    public SeriesLayer SeriesLayer => _seriesLayer;

    /// <summary>The series on the chart, in drawing order. Adding one wires it up; removing one lets it go.</summary>
    public AvaloniaList<Series> Series { get; } = [];

    /// <summary>The brush the surface of the chart is filled with; unset, the surface is left clear.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.SurfaceBackground"/>.</remarks>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ChartBase, IBrush?>(nameof(Background));

    /// <summary>The pen drawn around the surface; unset, there is no edge.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.SurfaceBorder"/>.</remarks>
    public static readonly StyledProperty<Pen?> BorderPenProperty =
        AvaloniaProperty.Register<ChartBase, Pen?>(nameof(BorderPen));

    /// <summary>The pen drawn around the surface while the chart holds the keyboard focus.</summary>
    /// <remarks>
    /// <para>The token behind it is <see cref="Themes.ChartTokens.SurfaceFocusRing"/>.</para>
    /// <para>
    /// Nothing draws it yet in practice, because a chart cannot take the focus until the selection step makes
    /// it focusable. The ring is already here so that the token has a home and the drawing is one line.
    /// </para>
    /// </remarks>
    public static readonly StyledProperty<Pen?> FocusRingProperty =
        AvaloniaProperty.Register<ChartBase, Pen?>(nameof(FocusRing));

    /// <summary>The pen the axis lines are drawn with, for the axes that do not set one of their own.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.AxisLine"/>.</remarks>
    public static readonly StyledProperty<Pen?> AxisLinePenProperty =
        AvaloniaProperty.Register<ChartBase, Pen?>(nameof(AxisLinePen));

    /// <summary>The pen the tick marks are drawn with; unset, they take the axis line's pen.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.AxisTick"/>.</remarks>
    public static readonly StyledProperty<Pen?> TickPenProperty =
        AvaloniaProperty.Register<ChartBase, Pen?>(nameof(TickPen));

    /// <summary>The pen the grid lines at the labelled intervals are drawn with.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.AxisGridLineMajor"/>.</remarks>
    public static readonly StyledProperty<Pen?> GridLinePenProperty =
        AvaloniaProperty.Register<ChartBase, Pen?>(nameof(GridLinePen));

    /// <summary>The pen the finer grid lines between them are drawn with.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.AxisGridLineMinor"/>.</remarks>
    public static readonly StyledProperty<Pen?> MinorGridLinePenProperty =
        AvaloniaProperty.Register<ChartBase, Pen?>(nameof(MinorGridLinePen));

    /// <summary>The brush the tick labels are written in.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.AxisLabelForeground"/>.</remarks>
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<ChartBase, IBrush?>(nameof(LabelBrush));

    /// <summary>The brush the surface of the chart is filled with; unset, the surface is left clear.</summary>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>The pen drawn around the surface; unset, there is no edge.</summary>
    public Pen? BorderPen
    {
        get => GetValue(BorderPenProperty);
        set => SetValue(BorderPenProperty, value);
    }

    /// <summary>The pen drawn around the surface while the chart holds the keyboard focus.</summary>
    public Pen? FocusRing
    {
        get => GetValue(FocusRingProperty);
        set => SetValue(FocusRingProperty, value);
    }

    /// <summary>The pen the axis lines are drawn with, for the axes that do not set one of their own.</summary>
    public Pen? AxisLinePen
    {
        get => GetValue(AxisLinePenProperty);
        set => SetValue(AxisLinePenProperty, value);
    }

    /// <summary>The pen the tick marks are drawn with; unset, they take the axis line's pen.</summary>
    public Pen? TickPen
    {
        get => GetValue(TickPenProperty);
        set => SetValue(TickPenProperty, value);
    }

    /// <summary>The pen the grid lines at the labelled intervals are drawn with.</summary>
    public Pen? GridLinePen
    {
        get => GetValue(GridLinePenProperty);
        set => SetValue(GridLinePenProperty, value);
    }

    /// <summary>The pen the finer grid lines between them are drawn with.</summary>
    public Pen? MinorGridLinePen
    {
        get => GetValue(MinorGridLinePenProperty);
        set => SetValue(MinorGridLinePenProperty, value);
    }

    /// <summary>The brush the tick labels are written in.</summary>
    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    /// <summary>The rectangle the data is mapped into: the control's bounds less the room the axes need.</summary>
    public Rect PlotBounds { get; private set; }

    /// <summary>Raised when what a dimension shows changes: what a host links two charts with.</summary>
    public event EventHandler<VisibleRangeChangedEventArgs>? VisibleRangeChanged;

    /// <summary>Whether the wheel zooms the chart about the pointer; on by default.</summary>
    /// <remarks>
    /// A chart inside a scrolling page usually wants this off, or the first notch of the wheel is spent on the
    /// chart rather than on the page.
    /// </remarks>
    public bool ZoomOnWheel { get; set; } = true;

    /// <summary>Whether dragging with the left button pans the chart; on by default.</summary>
    public bool PanOnDrag { get; set; } = true;

    /// <summary>Where a pair of values sits on the chart, or null when either cannot be placed.</summary>
    /// <param name="x">The value on the horizontal dimension.</param>
    /// <param name="y">The value on the vertical dimension.</param>
    /// <returns>The position, in the chart's own coordinates.</returns>
    public Point? DataToPoint(ChartValue x, ChartValue y) =>
        Transform.TryValueToPoint(x, y, out var point) ? point : null;

    /// <summary>The pair of values under a point, or null when the chart cannot say.</summary>
    /// <param name="point">The position, in the chart's own coordinates.</param>
    /// <returns>The values, in data space.</returns>
    public (ChartValue X, ChartValue Y)? PointToData(Point point) =>
        Transform.TryPointToValue(point, out var x, out var y) ? (x, y) : null;

    /// <summary>
    /// Moves the window by a number of logical pixels, in the direction the content is dragged.
    /// </summary>
    /// <param name="dx">How far the content moves right, in logical pixels; negative moves it left.</param>
    /// <param name="dy">How far the content moves down, in logical pixels; negative moves it up.</param>
    /// <remarks>
    /// The one path a drag, a wheel, a keyboard or a scrollbar all end up in: pixels, because that is what a
    /// pointer produces, and because a scrollbar has nothing else to say either. A dimension that is not to move
    /// is passed zero.
    /// </remarks>
    public void PanBy(double dx, double dy)
    {
        PanDimension(ChartDimension.X, dx);
        PanDimension(ChartDimension.Y, dy);

        // The plot itself may need re-measuring - the widest label can have changed with the window - and the
        // layers certainly need painting, since neither the plot rectangle nor the scaling moved.
        InvalidateArrange();
        InvalidateLayers();
    }

    /// <summary>Zooms both dimensions about a point, keeping whatever is under it where it is.</summary>
    /// <param name="factor">Above one zooms in, below one zooms out.</param>
    /// <param name="anchor">The point to zoom about, in the chart's own coordinates.</param>
    public void ZoomBy(double factor, Point anchor) => ZoomBy(factor, factor, anchor);

    /// <summary>Zooms each dimension about a point, keeping whatever is under it where it is.</summary>
    /// <param name="xFactor">Above one zooms in horizontally, below one zooms out.</param>
    /// <param name="yFactor">The same vertically; one to leave the dimension alone.</param>
    /// <param name="anchor">The point to zoom about, in the chart's own coordinates.</param>
    /// <remarks>Two factors because that is what panning with Shift or Ctrl pressed needs: one dimension still.</remarks>
    public void ZoomBy(double xFactor, double yFactor, Point anchor)
    {
        ZoomDimension(ChartDimension.X, xFactor, anchor);
        ZoomDimension(ChartDimension.Y, yFactor, anchor);
        InvalidateArrange();
        InvalidateLayers();
    }

    /// <summary>
    /// How far from a point the pointer may be and still count as pointing at it, in logical pixels; five.
    /// </summary>
    /// <remarks>Flex's own default, kept because a hand is as wide on either.</remarks>
    public double MouseSensitivity { get; set; } = 5;

    /// <summary>
    /// The nearest record of every series within <see cref="MouseSensitivity"/> of a point, nearest first.
    /// </summary>
    /// <param name="point">The position, in the chart's own coordinates.</param>
    /// <returns>One hit per series that has one, ordered by how close it is.</returns>
    /// <remarks>
    /// A data point is not a thing on screen, so this is the chart working out what the pointer is near rather
    /// than asking anything that exists. Records with no value are never hits.
    /// </remarks>
    public IReadOnlyList<HitData> FindDataPoints(Point point)
    {
        var hits = new List<HitData>();

        foreach (var series in Series)
        {
            if (series.HitTestItem(point, MouseSensitivity) is { } hit)
            {
                hits.Add(hit);
            }
        }

        hits.Sort((first, second) => first.Distance.CompareTo(second.Distance));
        return hits;
    }

    /// <summary>Every data point the chart is showing, lazily: the records in view of every series.</summary>
    /// <returns>The records, series by series, in data order.</returns>
    /// <remarks>
    /// A view rather than a list: nothing is built for a point that is not asked about, so a chart showing a
    /// million of them does not make a million of anything.
    /// </remarks>
    public IEnumerable<ChartItem> GetAllDataPoints() => Series.SelectMany(series => series.Items);

    /// <summary>Every data point inside a rectangle, series by series.</summary>
    /// <param name="region">The rectangle, in the chart's own coordinates.</param>
    /// <returns>The records, series by series, in data order.</returns>
    public IReadOnlyList<ChartItem> GetItemsInRegion(Rect region)
    {
        var items = new List<ChartItem>();

        foreach (var series in Series)
        {
            items.AddRange(series.GetItemsInRegion(region));
        }

        return items;
    }

    /// <summary>The stroke of a palette slot, for whatever draws by the point rather than by the series.</summary>
    /// <param name="index">
    /// The position in the palette, counted from zero. The palette cycles, so any index gives a colour; a pie
    /// chart handing its point index straight in gets the sequence it expects.
    /// </param>
    /// <returns>The pen, or null when the chart has not been dressed yet.</returns>
    public Pen? PaletteStroke(int index) => _paletteStrokes[Slot(index)];

    /// <summary>The fill of a palette slot, paired with <see cref="PaletteStroke"/>'s colour.</summary>
    /// <param name="index">The position in the palette, counted from zero.</param>
    /// <returns>The brush, or null when the chart has not been dressed yet.</returns>
    public IBrush? PaletteFill(int index) => _paletteFills[Slot(index)];

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // A chart has no natural size of its own, so it fills what it is given and picks a sensible size when
        // it is given nothing to fill.
        var size = new Size(
            double.IsInfinity(availableSize.Width) || double.IsNaN(availableSize.Width) ? 400 : availableSize.Width,
            double.IsInfinity(availableSize.Height) || double.IsNaN(availableSize.Height) ? 300 : availableSize.Height);

        foreach (var layer in _layers)
        {
            layer.Measure(size);
        }

        return size;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Whatever was pushed since the last pass is applied before the plot is worked out, so the axes cover it
        // in this frame rather than the next one.
        foreach (var series in Series)
        {
            series.ApplyPushed();
        }

        // The chart's own chance to work something out before the plot is measured: where the series that sit in
        // bands stand, what a stack adds up to.
        PrepareLayout();

        var bounds = new Rect(finalSize);
        PlotBounds = LayoutPlot(bounds);

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

        foreach (var layer in _layers)
        {
            layer.Configure(PlotBounds, scaling);
            layer.Arrange(bounds);
        }

        AnnounceWindows();

        return finalSize;
    }

    /// <summary>Asks every layer to repaint, which a move of the window needs and a move of the plot does not.</summary>
    /// <remarks>
    /// A layer repaints itself when the plot rectangle or the scaling changes, and neither has when the window
    /// slides: the lines are in different places, and only the layer knows it.
    /// </remarks>
    protected void InvalidateLayers()
    {
        foreach (var layer in _layers)
        {
            layer.InvalidateVisual();
        }
    }

    /// <summary>Says what each dimension shows, when that is not what it showed after the last pass.</summary>
    /// <remarks>
    /// Announced from the layout pass rather than from the operations that usually move the window, because those
    /// are not the only things that do: a resize moves it, a series arriving moves it, a change of theme does.
    /// Everything that moves the window is worth hearing about, and comparing what the axes worked out covers
    /// all of them in one place.
    /// </remarks>
    private void AnnounceWindows()
    {
        foreach (var dimension in new[] { ChartDimension.X, ChartDimension.Y })
        {
            var range = Transform.GetLayout(dimension).UnitRange;

            if (_windows.TryGetValue(dimension, out var last) && last == range)
            {
                continue;
            }

            _windows[dimension] = range;
            VisibleRangeChanged?.Invoke(this, new VisibleRangeChangedEventArgs(dimension, range));
        }
    }

    /// <summary>Moves one dimension's window by a number of logical pixels.</summary>
    private void PanDimension(ChartDimension dimension, double pixels)
    {
        if (!(pixels != 0) || !TryWindow(dimension, out var window))
        {
            return;
        }

        // Dragging the content to the right shows earlier data, so the window slides the other way by as many
        // units as the pixels are worth at this scale.
        var delta = -pixels * (window.Range.Length / window.Length);

        SetWindow(window, window.Range.Minimum + delta, window.Range.Maximum + delta);
    }

    /// <summary>Zooms one dimension about a point.</summary>
    private void ZoomDimension(ChartDimension dimension, double factor, Point anchor)
    {
        if (!(factor > 0) || double.IsInfinity(factor) || !TryWindow(dimension, out var window))
        {
            return;
        }

        // The value under the pointer is the thing that must not move; everything else follows from that.
        var fraction = dimension == ChartDimension.X
            ? (anchor.X - PlotBounds.X) / PlotBounds.Width
            : (PlotBounds.Bottom - anchor.Y) / PlotBounds.Height;
        var value = window.Range.Denormalize(fraction);
        var offset = dimension == ChartDimension.X ? anchor.X - PlotBounds.X : PlotBounds.Bottom - anchor.Y;

        if (window.Axis.ScaleMode == AxisScaleMode.Fixed)
        {
            // A fixed scale keeps one unit the same size, so zooming changes that size and the anchor decides
            // where the window has to start for the value under the pointer to stay where it was.
            var pixelsPerUnit = window.Axis.PixelsPerUnit * factor;

            if (!(pixelsPerUnit > 0) || double.IsInfinity(pixelsPerUnit))
            {
                return;
            }

            window.Axis.PixelsPerUnit = pixelsPerUnit;
            SetWindow(window, value - (offset / pixelsPerUnit), window.Range.Maximum);
            return;
        }

        // Fitting the data, the window shrinks about the anchor and follows it exactly.
        var span = window.Range.Length / factor;

        if (!(span > 0) || double.IsInfinity(span))
        {
            return;
        }

        var minimum = value - ((value - window.Range.Minimum) / factor);
        SetWindow(window, minimum, minimum + span);
    }

    /// <summary>
    /// The window a dimension shows and the axis that holds it: only an axis that keeps its range as settings can
    /// be moved, which is every axis here except a custom one that keeps it somewhere of its own.
    /// </summary>
    private bool TryWindow(ChartDimension dimension, out (AxisBase Axis, AxisUnitRange Range, double Length) window)
    {
        window = default;

        var range = Transform.GetLayout(dimension).UnitRange;
        var length = dimension == ChartDimension.X ? PlotBounds.Width
            : dimension == ChartDimension.Y ? PlotBounds.Height
            : 0;

        if (Transform.GetAxis(dimension) is not AxisBase axis ||
            range.IsEmpty || !(length > 0) || double.IsInfinity(range.Length))
        {
            return false;
        }

        window = (axis, range, length);
        return true;
    }

    /// <summary>Writes a window onto its axis, in the way its scale mode needs.</summary>
    private static void SetWindow(
        (AxisBase Axis, AxisUnitRange Range, double Length) window,
        double minimum,
        double maximum)
    {
        if (double.IsNaN(minimum) || double.IsInfinity(minimum) ||
            double.IsNaN(maximum) || double.IsInfinity(maximum) ||
            !(maximum > minimum))
        {
            return;
        }

        window.Axis.Minimum = minimum;

        // A fixed scale says where the window ends, so writing the far end would be a claim the axis then ignores;
        // fitting the data, both ends have to be written or the data would pull the window straight back.
        if (window.Axis.ScaleMode != AxisScaleMode.Fixed)
        {
            window.Axis.Maximum = maximum;
        }
    }

    /// <summary>
    /// Called at the start of every layout pass, before the plot is measured.
    /// </summary>
    /// <remarks>
    /// For the things a chart has to work out across its series - which is the only place they can be worked
    /// out, since a series knows nothing about the others. A chart that has no such business does nothing here,
    /// which is most of them.
    /// </remarks>
    protected virtual void PrepareLayout()
    {
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);

        if (Background is { } background)
        {
            context.FillRectangle(background, bounds);
        }

        // Half of a stroke lands inside the line and half outside it, so insetting by half puts the whole edge
        // on the surface rather than half of it off the control.
        if (BorderPen is { Thickness: > 0 } border)
        {
            context.DrawRectangle(null, border, bounds.Deflate(border.Thickness / 2));
        }

        if (IsFocused && FocusRing is { Thickness: > 0 } ring)
        {
            context.DrawRectangle(null, ring, bounds.Deflate(ring.Thickness / 2));
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var notches = e.Delta.Y;

        if (!ZoomOnWheel || !(notches != 0))
        {
            return;
        }

        WheelZoom(notches, e.GetPosition(this), e.KeyModifiers);
        e.Handled = true;
    }

    /// <summary>Zooms the window about a point, by a number of wheel notches.</summary>
    /// <param name="notches">How many notches the wheel turned; the sign says which way.</param>
    /// <param name="at">The point to zoom about, in the chart's own coordinates.</param>
    /// <param name="modifiers">The modifiers the input arrived with, which single out a dimension.</param>
    /// <remarks>
    /// The whole of what the wheel does, as a method: a host driving its own input - a touch pad, a remote - gets
    /// the same behaviour by calling this rather than by imitating a pointer.
    /// </remarks>
    protected void WheelZoom(double notches, Point at, KeyModifiers modifiers)
    {
        // The wheel says how many notches, not how much: one notch is a step of the ladder the eye is used to.
        var factor = Math.Pow(WheelZoomPerNotch, notches);
        var (horizontal, vertical) = SingledOut(modifiers);

        ZoomBy(horizontal ? factor : 1, vertical ? factor : 1, at);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!PanOnDrag || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        DragStart(e.GetPosition(this));

        // Captured, so that a drag that leaves the chart keeps panning it rather than stopping at the edge.
        e.Pointer.Capture(this);
    }

    /// <summary>Begins a drag at a point.</summary>
    /// <param name="at">Where the pointer went down, in the chart's own coordinates.</param>
    protected void DragStart(Point at)
    {
        _isPanning = true;
        _panHasStarted = false;
        _panFrom = at;
        _cursorBeforePan = Cursor;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        // Only a drag that happened is consumed: a press that went nowhere is left for whoever wanted the click.
        e.Handled = DragMove(e.GetPosition(this), e.KeyModifiers);
    }

    /// <summary>Carries a drag on to a point, panning the window by however far it moved.</summary>
    /// <param name="at">Where the pointer is now, in the chart's own coordinates.</param>
    /// <param name="modifiers">The modifiers the input arrived with, which single out a dimension.</param>
    /// <returns><see langword="true"/> when the drag actually panned the chart.</returns>
    protected bool DragMove(Point at, KeyModifiers modifiers)
    {
        if (!_isPanning)
        {
            return false;
        }

        var movedX = at.X - _panFrom.X;
        var movedY = at.Y - _panFrom.Y;

        if (!_panHasStarted && Math.Sqrt((movedX * movedX) + (movedY * movedY)) <= DragThreshold)
        {
            return false;
        }

        if (!_panHasStarted)
        {
            _panHasStarted = true;
            Cursor = new Cursor(StandardCursorType.SizeAll);
        }

        // A drag is a pan: the content follows the pointer, and PanBy takes the distance in the direction the
        // content was dragged, which is what this is.
        var (horizontal, vertical) = SingledOut(modifiers);

        PanBy(horizontal ? movedX : 0, vertical ? movedY : 0);
        _panFrom = at;

        return true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isPanning)
        {
            return;
        }

        var moved = _panHasStarted;

        DragEnd();
        e.Pointer.Capture(null);
        e.Handled = moved;
    }

    /// <summary>Ends a drag.</summary>
    protected void DragEnd()
    {
        _isPanning = false;
        Cursor = _cursorBeforePan;
        _panHasStarted = false;
    }

    /// <summary>
    /// Which dimensions a gesture touches, read from the modifier keys: Shift singles out the horizontal one,
    /// Control the vertical one, and neither - or both - singles out nothing.
    /// </summary>
    /// <param name="modifiers">The modifiers the input arrived with.</param>
    /// <returns>Which of the two dimensions the gesture is about.</returns>
    private static (bool Horizontal, bool Vertical) SingledOut(KeyModifiers modifiers)
    {
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var control = modifiers.HasFlag(KeyModifiers.Control);

        if (shift == control)
        {
            return (true, true);
        }

        return shift ? (true, false) : (false, true);
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        TakeControlTheme();
        Dress();
    }

    /// <summary>Registers a layer with the chart and adds it to the visual tree.</summary>
    /// <param name="layer">The layer.</param>
    protected void AddLayer(ChartLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        _layers.Add(layer);
        VisualChildren.Add(layer);

        // A layer that arrived after the theme did still has to be dressed.
        ApplyAxisInks();
    }

    /// <summary>Takes a layer out of the chart and out of the visual tree.</summary>
    /// <param name="layer">The layer.</param>
    protected void RemoveLayer(ChartLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        if (_layers.Remove(layer))
        {
            VisualChildren.Remove(layer);
        }
    }

    /// <summary>Creates the layer for one axis and registers it.</summary>
    /// <param name="renderer">The axis' renderer.</param>
    /// <returns>The layer, so the caller can configure how it paints.</returns>
    protected AxisLayer AddAxis(AxisRenderer renderer)
    {
        var layer = new AxisLayer(renderer) { Transform = Transform };
        AddLayer(layer);
        return layer;
    }

    /// <summary>
    /// Works out the plot rectangle: what is left of the control once every axis has taken the room its labels
    /// need.
    /// </summary>
    /// <param name="bounds">The control's bounds.</param>
    /// <returns>The rectangle the data is mapped into.</returns>
    protected Rect LayoutPlot(Rect bounds)
    {
        var plot = bounds;

        for (var pass = 0; ; pass++)
        {
            Transform.PlotBounds = plot;
            Transform.Update();

            var next = Deflate(bounds, plot);

            if (next == plot || pass >= MaximumLayoutPasses)
            {
                return plot;
            }

            // The plot moved, so what a fixed scale covers moved with it: the ranges are worked out again against
            // the room that is actually there rather than the room the pass started with. Without this, a fixed
            // scale is out by the share of the control the gutter took - which is exactly the kind of thing that
            // looks fine until someone measures it.
            plot = next;
            Transform.PlotBounds = plot;
            Transform.InvalidateRange();
        }
    }

    private Rect Deflate(Rect bounds, Rect plot)
    {
        var left = 0d;
        var bottom = 0d;
        var right = 0d;
        var top = 0d;

        foreach (var layer in _layers)
        {
            if (layer is not AxisLayer axis)
            {
                continue;
            }

            // The axis' own layout: a chart with a second axis on a dimension has two of them, and each was
            // resolved against the values of the series that are measured against it.
            var gutter = axis.Renderer.MeasureGutter(Transform.GetLayout(axis.Renderer.Axis));
            var far = axis.Renderer.Placement == AxisPlacement.End;

            if (axis.Renderer.IsVertical)
            {
                if (far)
                {
                    right = Math.Max(right, gutter);
                }
                else
                {
                    left = Math.Max(left, gutter);
                }
            }
            else
            {
                if (far)
                {
                    top = Math.Max(top, gutter);
                }
                else
                {
                    bottom = Math.Max(bottom, gutter);
                }
            }
        }

        return new Rect(
            bounds.X + left,
            bounds.Y + top,
            Math.Max(0, bounds.Width - left - right),
            Math.Max(0, bounds.Height - top - bottom));
    }

    private void OnSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (Series series in e.OldItems)
            {
                Transform.RemoveElement(series);
                _seriesLayer.Remove(series);
                series.Invalidated -= OnSeriesInvalidated;
                series.Pushed -= OnSeriesPushed;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (Series series in e.NewItems)
            {
                Transform.AddElement(series);
                _seriesLayer.Add(series);

                // A series that puts on a colour of its own later gives up its slot, so the ones after it move
                // up; the series itself says when that happened.
                series.Invalidated += OnSeriesInvalidated;
                series.Pushed += OnSeriesPushed;
            }
        }

        ApplyPalette();
        SeriesChanged();

        // A new series may want more of an axis than the old ones did, which can move the plot.
        InvalidateArrange();
        _seriesLayer.InvalidateVisual();
    }

    private void OnSeriesInvalidated(object? sender, EventArgs e)
    {
        ApplyPalette();
        SeriesChanged();
    }

    /// <summary>
    /// Called when a series was added, removed, or changed something the chart as a whole has to answer for.
    /// </summary>
    /// <remarks>
    /// The place for the work that belongs to no one series - which is exactly the work a series cannot do:
    /// an axis of its own is a series' own business, but drawing that axis is the chart's.
    /// </remarks>
    protected virtual void SeriesChanged()
    {
    }

    /// <summary>
    /// Asks for a pass that applies whatever was pushed, from whatever thread the push arrived on.
    /// </summary>
    /// <remarks>
    /// This runs on the pushing thread, where nothing may be drawn, so all it does is ask for a pass on the UI
    /// thread - once, however many pushes arrive before that pass runs. That single pass is the whole point of
    /// the arrangement: a frame's worth of points costs one repaint rather than one each.
    /// </remarks>
    private void OnSeriesPushed(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _pumpIsPosted, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(PumpPushes);
    }

    /// <summary>Applies every series' pushed records, on the UI thread, and repaints once if any arrived.</summary>
    private void PumpPushes()
    {
        Interlocked.Exchange(ref _pumpIsPosted, 0);

        var applied = 0;

        foreach (var series in Series)
        {
            applied += series.ApplyPushed();
        }

        if (applied > 0)
        {
            _seriesLayer.InvalidateVisual();
        }
    }

    /// <summary>Reads the palette out of the theme and gives it to the series.</summary>
    private void Dress()
    {
        for (var slot = 0; slot < ChartTokens.PaletteSize; slot++)
        {
            _paletteStrokes[slot] = FindInScope(ChartTokens.SeriesStroke(slot + 1)) as Pen;
            _paletteFills[slot] = FindInScope(ChartTokens.SeriesFill(slot + 1)) as IBrush;
        }

        ApplyPalette();
    }

    /// <summary>
    /// Gives every series that has no colours of its own the next slot of the palette, in the order the series
    /// were added.
    /// </summary>
    /// <remarks>
    /// A series that brings its own colour takes no slot, so the rest of the chart stays where it was. Slots
    /// cycle when there are more series than the palette holds.
    /// </remarks>
    private void ApplyPalette()
    {
        var slot = 0;

        foreach (var series in Series)
        {
            if (series.HasOwnColours)
            {
                series.PaletteStroke = null;
                series.PaletteFill = null;
                continue;
            }

            series.PaletteStroke = PaletteStroke(slot);
            series.PaletteFill = PaletteFill(slot);
            slot++;
        }
    }

    private static int Slot(int index) =>
        (((index % ChartTokens.PaletteSize) + ChartTokens.PaletteSize) % ChartTokens.PaletteSize);

    /// <summary>
    /// Takes the control theme keyed by this chart's type, if there is one in scope and nobody set one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Avalonia gives a control its theme from a style host. A colour theme, though, arrives as resource
    /// dictionaries - that is how a host overrides a token - so the chart looks for its own theme in the
    /// resources it can see, from its concrete type up to this one, and takes the first one it finds.
    /// </para>
    /// <para>
    /// Setting the theme is what makes the properties below settle: the theme's setters read the tokens, and
    /// the ones for the axes are then passed down by <see cref="ApplyAxisInks"/>.
    /// </para>
    /// </remarks>
    private void TakeControlTheme()
    {
        if (Theme is not null)
        {
            return;
        }

        for (var type = GetType(); type is not null && typeof(ChartBase).IsAssignableFrom(type); type = type.BaseType)
        {
            if (FindControlTheme(type) is { } theme)
            {
                Theme = theme;
                return;
            }
        }
    }

    /// <summary>Looks for a control theme keyed by a type in the resources this chart can see.</summary>
    /// <param name="type">The type to look up.</param>
    /// <returns>The theme, or null when there is none.</returns>
    private ControlTheme? FindControlTheme(Type type) => FindInScope(type) as ControlTheme;

    /// <summary>Looks a resource up in the resources this chart can see, in the variant it is showing.</summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The value, or null when nothing in scope carries that key.</returns>
    private object? FindInScope(object key)
    {
        var variant = ActualThemeVariant;

        // Everything the chart is inside of, and the application at the end of it: the application is not part
        // of the logical tree, but it is where a theme usually arrives.
        for (ILogical? node = this; node is not null; node = node.LogicalParent)
        {
            if (node is StyledElement element && element.Resources is { } resources
                && resources.TryGetResource(key, variant, out var value))
            {
                return value;
            }
        }

        return Application.Current is { } application
            && application.Resources is { } applicationResources
            && applicationResources.TryGetResource(key, variant, out var fromApplication)
                ? fromApplication
                : null;
    }

    /// <summary>
    /// Hands the chart's axis inks down to the layers that paint the axes, and asks them to repaint.
    /// </summary>
    /// <remarks>
    /// A control theme can set a chart's own properties but not the properties of the layers inside it, so the
    /// chart passes them along - whenever one of them changes, and once for a layer that arrives later. A layer
    /// no longer keeps an ink of its own against the theme: to paint one axis differently, a host overrides the
    /// token for that chart or sets the layer's property after the chart is dressed.
    /// </remarks>
    private void ApplyAxisInks()
    {
        foreach (var layer in _layers)
        {
            if (layer is not AxisLayer axis)
            {
                continue;
            }

            axis.AxisLinePen = AxisLinePen;
            axis.TickPen = TickPen;
            axis.GridLinePen = GridLinePen;
            axis.MinorGridLinePen = MinorGridLinePen;
            axis.LabelBrush = LabelBrush;
            axis.InvalidateVisual();
        }
    }
}
