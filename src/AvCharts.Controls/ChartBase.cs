using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
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
    private readonly CrosshairLayer _crosshairLayer;
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

    /// <summary>How far a tip stands off the reading it is about, in logical pixels.</summary>
    private const double TipOffset = 6;

    private readonly Canvas _tips = new() { IsHitTestVisible = false };
    private readonly List<DataTip> _tipPool = [];
    private HitData[] _tipReadings = [];
    private Point _tipAnchor;
    private bool _pointerIsOnChart;

    /// <summary>Creates a chart.</summary>
    protected ChartBase()
    {
        ClipToBounds = true;
        _seriesLayer = new SeriesLayer { Transform = Transform };
        AddLayer(_seriesLayer);

        // Above the series, so the crosshair reads over the ink rather than under it.
        _crosshairLayer = new CrosshairLayer();
        AddLayer(_crosshairLayer);

        // The tips are the one part of a chart that is made of controls rather than paint, so they live in a
        // layer of their own that nothing test-hits: a tip is something to read, not something to point at.
        VisualChildren.Add(_tips);
        LogicalChildren.Add(_tips);

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
        AxisLinePenProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        TickPenProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        GridLinePenProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        MinorGridLinePenProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        LabelBrushProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        CrosshairLinePenProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        CrosshairLabelBackgroundProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        CrosshairLabelForegroundProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
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

    /// <summary>The pen the crosshair's lines are drawn with.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.CrosshairLine"/>.</remarks>
    public static readonly StyledProperty<Pen?> CrosshairLinePenProperty =
        AvaloniaProperty.Register<ChartBase, Pen?>(nameof(CrosshairLinePen));

    /// <summary>The brush behind the crosshair's readouts.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.CrosshairLabelBackground"/>.</remarks>
    public static readonly StyledProperty<IBrush?> CrosshairLabelBackgroundProperty =
        AvaloniaProperty.Register<ChartBase, IBrush?>(nameof(CrosshairLabelBackground));

    /// <summary>The brush the crosshair's readouts are written in.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.CrosshairLabelForeground"/>.</remarks>
    public static readonly StyledProperty<IBrush?> CrosshairLabelForegroundProperty =
        AvaloniaProperty.Register<ChartBase, IBrush?>(nameof(CrosshairLabelForeground));

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

    /// <summary>The pen the crosshair's lines are drawn with.</summary>
    public Pen? CrosshairLinePen
    {
        get => GetValue(CrosshairLinePenProperty);
        set => SetValue(CrosshairLinePenProperty, value);
    }

    /// <summary>The brush behind the crosshair's readouts.</summary>
    public IBrush? CrosshairLabelBackground
    {
        get => GetValue(CrosshairLabelBackgroundProperty);
        set => SetValue(CrosshairLabelBackgroundProperty, value);
    }

    /// <summary>The brush the crosshair's readouts are written in.</summary>
    public IBrush? CrosshairLabelForeground
    {
        get => GetValue(CrosshairLabelForegroundProperty);
        set => SetValue(CrosshairLabelForegroundProperty, value);
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

    /// <summary>Whether a crosshair follows the pointer; off by default, as the design brief has it.</summary>
    public bool ShowCrosshair
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;

            if (!value)
            {
                HideCrosshair();
            }
        }
    }

    /// <summary>The nearest valid point the crosshair is on, or null when it is not showing.</summary>
    public HitData? CrosshairHit => _crosshairLayer.Hit;

    /// <summary>
    /// What the chart shows when the pointer finds a reading: a tip beside it, a tip that follows the pointer,
    /// or nothing.
    /// </summary>
    /// <remarks>
    /// Beside the record by default, which is what the design brief means by data tips being on in v1. Changing
    /// it rearranges whatever is already showing rather than waiting for the pointer to move again.
    /// </remarks>
    public DataTipMode DataTipMode
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            RefreshTips();
        }
    }

    /// <summary>
    /// Whether every reading in view gets a tip at once, rather than only the one the pointer is near; off by
    /// default.
    /// </summary>
    /// <remarks>
    /// The tips are stacked so that they do not cover one another, which is Flex's <c>TipStack</c> and the reason
    /// it is worth having. It is meant for charts with a handful of records on them: every record in view is a
    /// tip, so a series of a hundred thousand points would ask for a hundred thousand of them.
    /// </remarks>
    public bool ShowAllDataTips
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            RefreshTips();
        }
    }

    /// <summary>How a tip writes itself, or null to write what the axes wrote for the hit.</summary>
    /// <remarks>
    /// Flex's <c>dataTipFunction</c>, as a delegate rather than a function object. The hit is the whole of what
    /// is known - the record, the caller's own object, the values and the line the axes wrote - so a host that
    /// wants a different line has everything it needs in hand.
    /// </remarks>
    public Func<HitData, string>? DataTipFunction { get; set; }

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
        => FindDataPoints(point, MouseSensitivity);

    /// <summary>
    /// The nearest record of every series within a distance of a point, nearest first.
    /// </summary>
    /// <param name="point">The position, in the chart's own coordinates.</param>
    /// <param name="sensitivity">How far from the point a record may be, in logical pixels.</param>
    /// <returns>One hit per series that has one, ordered by how close it is.</returns>
    /// <remarks>
    /// The crosshair uses this with a distance no point can be further than, because a crosshair wants the
    /// nearest point there is rather than the nearest within a hand's reach - whereas a pointer wants what it is
    /// actually pointing at, which is the rule the brief gives for a hit test.
    /// </remarks>
    public IReadOnlyList<HitData> FindDataPoints(Point point, double sensitivity)
    {
        var hits = new List<HitData>();

        foreach (var series in Series)
        {
            if (series.HitTestItem(point, sensitivity) is { } hit)
            {
                hits.Add(hit);
            }
        }

        hits.Sort((first, second) => first.Distance.CompareTo(second.Distance));
        return hits;
    }

    /// <summary>
    /// Puts the crosshair on the nearest valid point to a position, and takes it away when there is none.
    /// </summary>
    /// <param name="at">Where the pointer is, in the chart's own coordinates.</param>
    /// <remarks>
    /// <para>
    /// The crosshair is not the pointer: it stands on the nearest record there is rather than wherever the
    /// pointer happens to be, so it never points at a reading that does not exist. The distance it is willing to
    /// travel is therefore unbounded - unlike <see cref="FindDataPoints(Point)"/>, where a pointer should only
    /// find what it is actually pointing at.
    /// </para>
    /// <para>
    /// A host driving its own input - a touch pad, a remote, a test - puts the crosshair where it wants by
    /// calling this, rather than by imitating a pointer. Nothing shows while <see cref="ShowCrosshair"/> is off.
    /// </para>
    /// </remarks>
    protected void MoveCrosshair(Point at)
    {
        if (!ShowCrosshair)
        {
            HideCrosshair();
            return;
        }

        var hits = FindDataPoints(at, double.PositiveInfinity);

        // Told to look everywhere, so the first one is the nearest there is; a chart with nothing to point at
        // has no crosshair at all.
        _crosshairLayer.Hit = hits.Count > 0 ? hits[0] : null;
    }

    /// <summary>Takes the crosshair away.</summary>
    protected void HideCrosshair() => _crosshairLayer.Hit = null;

    /// <summary>Shows the tips for a position: the reading the pointer is near, or every reading in view.</summary>
    /// <param name="at">Where the pointer is, in the chart's own coordinates.</param>
    /// <remarks>
    /// A host driving its own input - a touch pad, a remote, a test - shows tips by calling this, rather than by
    /// imitating a pointer. Nothing shows while <see cref="DataTipMode"/> is <see cref="Controls.DataTipMode.None"/>,
    /// and a position that is near no reading shows nothing either: a tip is a reading, so there has to be one.
    /// </remarks>
    protected void ShowDataTipsAt(Point at)
    {
        _tipAnchor = at;
        _pointerIsOnChart = true;
        RefreshTips();
    }

    /// <summary>Takes every tip away.</summary>
    protected void HideDataTips()
    {
        _pointerIsOnChart = false;
        RefreshTips();
    }

    /// <summary>
    /// Works out which readings are being shown, walks the tips into place, and puts the rest of the pool away.
    /// </summary>
    /// <remarks>
    /// Called whenever anything the answer depends on moves: the pointer, the mode, or the plot itself, since a
    /// tip is placed against the chart's bounds and those move when the chart is laid out again.
    /// </remarks>
    private void RefreshTips()
    {
        _tipReadings = Readings();

        var chart = new Rect(Bounds.Size);

        for (var index = 0; index < _tipReadings.Length; index++)
        {
            var tip = Tip(index);

            tip.Data = _tipReadings[index];
            tip.Text = DataTipFunction?.Invoke(_tipReadings[index]) ?? _tipReadings[index].DisplayText;
            tip.IsVisible = true;
            tip.Measure(Size.Infinity);
        }

        for (var index = _tipReadings.Length; index < _tipPool.Count; index++)
        {
            _tipPool[index].IsVisible = false;
        }

        Place(chart);
    }

    /// <summary>The readings the tips stand for, in the order they are laid out.</summary>
    /// <returns>The hits, or none when there is nothing to show.</returns>
    private HitData[] Readings()
    {
        if (DataTipMode == DataTipMode.None)
        {
            return [];
        }

        // Asking for all of them is asking whether the pointer is there or not: that is what the property is
        // for. Otherwise a tip is about where the pointer is, so with no pointer there is nothing to say.
        if (ShowAllDataTips)
        {
            return EveryReadingInView();
        }

        if (!_pointerIsOnChart)
        {
            return [];
        }

        // As close as a pointer reaches, which is the same reach as a hit test: a tip is about what is under the
        // pointer, and one reading rather than every reading within reach of it.
        var found = FindDataPoints(_tipAnchor, MouseSensitivity);
        return found.Count == 0 ? [] : [found[0]];
    }

    /// <summary>One hit for every reading in view, so that every one of them can be written.</summary>
    /// <returns>The hits.</returns>
    /// <remarks>
    /// The same hit test a pointer would do decides which record each of these is, so a series that answers for
    /// its own hits answers for its own tips as well - which is what keeps a custom series from needing a second
    /// way of naming its records. A record with no value has no place on the chart and gets no tip.
    /// </remarks>
    private HitData[] EveryReadingInView()
    {
        var hits = new List<HitData>();

        foreach (var item in GetAllDataPoints())
        {
            if (item.Position is { } at &&
                item.Series.HitTestItem(at, 0.5) is { } hit &&
                hit.ChartItem.Index == item.Index)
            {
                hits.Add(hit);
            }
        }

        return [.. hits];
    }

    /// <summary>Puts every tip where it belongs, and leaves the rest of the pool where it is.</summary>
    /// <param name="chart">The rectangle the tips have to stay inside.</param>
    private void Place(Rect chart)
    {
        if (_tipReadings.Length == 0)
        {
            return;
        }

        var slots = new List<TipSlot>(_tipReadings.Length);

        for (var index = 0; index < _tipReadings.Length; index++)
        {
            var reading = _tipReadings[index];
            var tip = _tipPool[index];
            var at = reading.Position ?? chart.Center;

            // A tip hangs to the right of what it is about, above it when there is room above it. What it is
            // about is the reading, except when the mode says to follow the pointer - then it is the pointer.
            var anchor = DataTipMode == DataTipMode.Mouse ? _tipAnchor : at;
            var x = Math.Clamp(anchor.X, chart.Left, chart.Right);
            var y = Math.Clamp(anchor.Y, chart.Top, chart.Bottom);
            var size = tip.DesiredSize;
            var top = y - TipOffset - size.Height >= chart.Top ? y - TipOffset - size.Height : y + TipOffset;

            slots.Add(new TipSlot(tip, x, y, top, size.Width, size.Height));
        }

        // Half of them to the left of what they are about and half to the right, so that two neighbouring
        // readings do not both want the same side; within a side, tips that would overlap are stacked.
        slots.Sort((first, second) => first.X.CompareTo(second.X));

        var half = slots.Count / 2;

        Stack(slots.GetRange(half, slots.Count - half), chart, toTheRight: true);
        Stack(slots.GetRange(0, half), chart, toTheRight: false);

        foreach (var slot in slots)
        {
            Canvas.SetLeft(slot.Tip, Math.Clamp(slot.Left, chart.Left, Math.Max(chart.Left, chart.Right - slot.Width)));
            Canvas.SetTop(slot.Tip, Math.Clamp(slot.Top, chart.Top, Math.Max(chart.Top, chart.Bottom - slot.Height)));
        }
    }

    /// <summary>Lays one side of the chart out: the tips that would overlap become a column.</summary>
    /// <param name="slots">The tips of that side, which this sorts by how high they are.</param>
    /// <param name="chart">The rectangle the tips have to stay inside.</param>
    /// <param name="toTheRight">Whether this is the side that hangs to the right of the readings.</param>
    /// <remarks>
    /// Flex's <c>TipStack</c>, ported: a run of tips whose boxes would meet is laid out one under the other,
    /// centred on where they wanted to be between them, and the run is kept inside the chart. What is not
    /// carried over is the pass that then nudges tips about so that they cover no readings - Flex itself calls
    /// the layout it produces unattractive, and the brief asks for the stacking.
    /// </remarks>
    private static void Stack(List<TipSlot> slots, Rect chart, bool toTheRight)
    {
        slots.Sort((first, second) => first.Y.CompareTo(second.Y));

        var stacks = new List<TipStack>();
        var current = new TipStack();

        foreach (var slot in slots)
        {
            slot.Left = toTheRight ? slot.X + TipOffset : slot.X - slot.Width - TipOffset;

            if (current.WouldCover(slot.Top))
            {
                current.Add(slot, chart.Height);
                continue;
            }

            if (current.Count > 0)
            {
                stacks.Add(current);
            }

            current = new TipStack();
            current.Add(slot, chart.Height);
        }

        if (current.Count > 0)
        {
            stacks.Add(current);
        }

        foreach (var stack in stacks)
        {
            stack.Lay();
        }
    }

    /// <summary>The tip at a position in the pool, making one when the pool is not that long yet.</summary>
    /// <param name="index">The position in the pool.</param>
    /// <returns>The tip.</returns>
    private DataTip Tip(int index)
    {
        while (_tipPool.Count <= index)
        {
            var tip = new DataTip { IsVisible = false };
            _tipPool.Add(tip);
            _tips.Children.Add(tip);
        }

        return _tipPool[index];
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

        _tips.Measure(size);

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

        // The tips are placed against the plot, so they are placed after it: a second pointer move is not
        // something a resize or a pan should need before they read right.
        _tips.Arrange(bounds);

        if (_tipReadings.Length > 0)
        {
            Place(new Rect(finalSize));
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

        var at = e.GetPosition(this);

        // Only a drag that happened is consumed: a press that went nowhere is left for whoever wanted the click.
        e.Handled = DragMove(at, e.KeyModifiers);

        MoveCrosshair(at);
        ShowDataTipsAt(at);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        HideCrosshair();
        HideDataTips();
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
        ResourceThemes.Apply(this, typeof(ChartBase));
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
        ApplyInks();
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

    /// <summary>Looks a token up in the resources this chart can see, in the variant it is showing.</summary>
    /// <param name="key">The token's name.</param>
    /// <returns>The value, or null when nothing in scope carries that key.</returns>
    private object? FindInScope(object key) => ResourceThemes.Find(this, key);

    /// <summary>
    /// Hands the chart's inks down to the layers that paint them, and asks those layers to repaint.
    /// </summary>
    /// <remarks>
    /// A control theme can set a chart's own properties but not the properties of the layers inside it, so the
    /// chart passes them along - whenever one of them changes, and once for a layer that arrives later. A layer
    /// no longer keeps an ink of its own against the theme: to paint one axis differently, a host overrides the
    /// token for that chart or sets the layer's property after the chart is dressed.
    /// </remarks>
    private void ApplyInks()
    {
        foreach (var layer in _layers)
        {
            switch (layer)
            {
                case AxisLayer axis:
                    axis.AxisLinePen = AxisLinePen;
                    axis.TickPen = TickPen;
                    axis.GridLinePen = GridLinePen;
                    axis.MinorGridLinePen = MinorGridLinePen;
                    axis.LabelBrush = LabelBrush;
                    break;

                case CrosshairLayer crosshair:
                    crosshair.LinePen = CrosshairLinePen;
                    crosshair.LabelBackground = CrosshairLabelBackground;
                    crosshair.LabelForeground = CrosshairLabelForeground;
                    break;

                default:
                    continue;
            }

            layer.InvalidateVisual();
        }
    }

    /// <summary>Where one tip wants to be, and how much room it takes.</summary>
    private sealed class TipSlot
    {
        /// <summary>Remembers what a tip is standing for and where it would like to stand.</summary>
        /// <param name="tip">The tip.</param>
        /// <param name="x">Where the reading is, across the chart.</param>
        /// <param name="y">Where the reading is, up the chart.</param>
        /// <param name="top">Where the tip would rather be, up the chart.</param>
        /// <param name="width">How wide the tip is.</param>
        /// <param name="height">How tall the tip is.</param>
        public TipSlot(DataTip tip, double x, double y, double top, double width, double height)
        {
            Tip = tip;
            X = x;
            Y = y;
            Top = top;
            Width = width;
            Height = height;
            Left = x;
        }

        /// <summary>The tip itself.</summary>
        public DataTip Tip { get; }

        /// <summary>Where the reading it is about is, across the chart.</summary>
        public double X { get; }

        /// <summary>Where the reading it is about is, up the chart.</summary>
        public double Y { get; }

        /// <summary>How wide the tip is.</summary>
        public double Width { get; }

        /// <summary>How tall the tip is.</summary>
        public double Height { get; }

        /// <summary>Where the tip is being put, up the chart; where it wanted to be until a stack moves it.</summary>
        public double Top { get; set; }

        /// <summary>Where the tip is being put, across the chart.</summary>
        public double Left { get; set; }
    }

    /// <summary>
    /// A run of tips whose boxes would cover one another, laid out as a column instead.
    /// </summary>
    /// <remarks>
    /// Flex's <c>TipStack</c>: a tip that would land on the stack joins it, the stack grows around where its
    /// tips wanted to be between them, and the run is kept inside the room it has. It is what makes showing all
    /// the tips at once readable rather than a pile.
    /// </remarks>
    private sealed class TipStack
    {
        private readonly List<TipSlot> _tips = [];
        private double _top;
        private double _height;

        /// <summary>How many tips have joined.</summary>
        public int Count => _tips.Count;

        /// <summary>Whether a tip wanting to be at a height would land on this stack.</summary>
        /// <param name="top">Where that tip would rather be, up the chart.</param>
        /// <returns>True when it would cover the stack.</returns>
        public bool WouldCover(double top) => _tips.Count > 0 && top < _top + _height;

        /// <summary>Folds a tip into the stack and grows the stack around it.</summary>
        /// <param name="tip">The tip joining.</param>
        /// <param name="room">How much height there is to lay the stack out in.</param>
        public void Add(TipSlot tip, double room)
        {
            _tips.Add(tip);

            if (_tips.Count == 1)
            {
                _top = tip.Top;
                _height = tip.Height;
                return;
            }

            _height += tip.Height;

            // Where the stack wants to be: the middle of where its tips wanted to be between them, which is the
            // average of two and a moving average after that - Flex's arithmetic, kept because it reads well.
            var wanted = _tips.Count == 2
                ? (_tips[0].Y + _tips[1].Y) / 2
                : (((_top + (_height / 2)) * (_tips.Count - 1)) + tip.Y) / _tips.Count;

            _top = Math.Min(room - _height, Math.Max(0, wanted - (_height / 2)));
        }

        /// <summary>Puts the stack's tips one under the other, from where the stack ended up.</summary>
        public void Lay()
        {
            var top = _top;

            foreach (var tip in _tips)
            {
                tip.Top = top;
                top += tip.Height;
            }
        }
    }
}
