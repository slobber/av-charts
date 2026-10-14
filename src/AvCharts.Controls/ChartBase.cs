using System.Collections.Specialized;
using System.Windows.Input;
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
    private readonly AnnotationLayer _annotationLayer;
    private readonly SeriesLayer _seriesLayer;
    private readonly SelectionLayer _selectionLayer;
    private readonly CrosshairLayer _crosshairLayer;
    private readonly Pen?[] _paletteStrokes = new Pen?[ChartTokens.PaletteSize];
    private readonly IBrush?[] _paletteFills = new IBrush?[ChartTokens.PaletteSize];
    private readonly Dictionary<ChartDimension, AxisUnitRange> _windows = [];
    private readonly List<ChartItem> _selected = [];
    private DataTransform _transform = new();
    private ChartItem? _caret;
    private ChartItem? _anchor;
    private HitData? _pointerHit;
    private int _pressClickCount = 1;
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
    private string?[] _tipTexts = [];
    private Point _tipAnchor;
    private bool _pointerIsOnChart;

    /// <summary>Creates a chart.</summary>
    protected ChartBase()
    {
        ClipToBounds = true;

        // Under everything else: an annotation is context for the data rather than a lid on it.
        _annotationLayer = new AnnotationLayer { Transform = _transform };
        AddLayer(_annotationLayer);

        _seriesLayer = new SeriesLayer { Transform = _transform };
        AddLayer(_seriesLayer);

        // Above the series, so a mark reads over the ink it is about, and below the crosshair, so that the
        // readings a reader is following stay on top of everything.
        _selectionLayer = new SelectionLayer();
        AddLayer(_selectionLayer);

        // Above the series, so the crosshair reads over the ink rather than under it.
        _crosshairLayer = new CrosshairLayer();
        AddLayer(_crosshairLayer);

        // The tips are the one part of a chart that is made of controls rather than paint, so they live in a
        // layer of their own that nothing test-hits: a tip is something to read, not something to point at.
        VisualChildren.Add(_tips);
        LogicalChildren.Add(_tips);

        Series.CollectionChanged += OnSeriesChanged;
        Elements.CollectionChanged += OnElementsChanged;

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
        ItemHoverFillProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        ItemHoverStrokeProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        ItemSelectedFillProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
        ItemSelectedStrokeProperty.Changed.AddClassHandler<ChartBase>((chart, _) => chart.ApplyInks());
    }

    /// <summary>The transform every layer, series and element reads its positions from.</summary>
    /// <remarks>
    /// Settable, and that is the point of it: a coordinate system of one's own - the polar mapping a pie chart
    /// needs, the hexagonal one a capability chart needs - is a <see cref="DataTransform"/> subclass, and handing
    /// it to a chart is the only way to use it. Advanced: everything else in the library assumes values across
    /// and values up. Setting it moves the axes, the series and the elements across to it, which is what makes
    /// the chart work again afterwards rather than draw in the old frame.
    /// </remarks>
    public DataTransform Transform
    {
        get => _transform;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_transform, value))
            {
                return;
            }

            var previous = _transform;
            _transform = value;

            // The layers hold their own reference to a transform rather than asking the chart for one, and a
            // series is registered with a transform rather than looking one up, so all of that has to be said
            // again about the new one.
            foreach (var layer in _layers)
            {
                switch (layer)
                {
                    case AnnotationLayer annotations:
                        annotations.Transform = value;
                        break;

                    case AxisLayer axis:
                        axis.Transform = value;
                        break;

                    case SeriesLayer series:
                        series.Transform = value;
                        break;

                    default:
                        break;
                }
            }

            foreach (var (dimension, axis) in previous.Axes)
            {
                value.SetAxis(dimension, axis);
            }

            foreach (var series in Series)
            {
                previous.RemoveElement(series);
                value.AddElement(series);
            }

            foreach (var element in Elements)
            {
                previous.RemoveElement(element);
                value.AddElement(element);
            }

            // What the chart told the outside world about its visible ranges was about the old frame.
            _windows.Clear();

            InvalidateArrange();
            InvalidateLayers();
        }
    }

    /// <summary>
    /// The things drawn on the chart that are not series: threshold lines, reference bands, markers for events.
    /// </summary>
    /// <remarks>
    /// Adding one registers it with the transform and paints it in the layer under the series; an element that
    /// declares data moves the plot, which is why adding one asks for a layout pass. Experimental in v1, as the
    /// design brief has it - see <see cref="Charts.ChartElement"/>.
    /// </remarks>
    public AvaloniaList<ChartElement> Elements { get; } = [];

    /// <summary>The layer that paints the elements, under the series.</summary>
    public AnnotationLayer AnnotationLayer => _annotationLayer;

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

    /// <summary>The fill of the mark on the record the keyboard is on.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.MarkerHoverFill"/>.</remarks>
    public static readonly StyledProperty<IBrush?> ItemHoverFillProperty =
        AvaloniaProperty.Register<ChartBase, IBrush?>(nameof(ItemHoverFill));

    /// <summary>The pen of the mark on the record the keyboard is on.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.MarkerHoverStroke"/>.</remarks>
    public static readonly StyledProperty<Pen?> ItemHoverStrokeProperty =
        AvaloniaProperty.Register<ChartBase, Pen?>(nameof(ItemHoverStroke));

    /// <summary>The fill of the mark on a selected record.</summary>
    /// <remarks>The token behind it is <see cref="Themes.ChartTokens.MarkerSelectedFill"/>.</remarks>
    public static readonly StyledProperty<IBrush?> ItemSelectedFillProperty =
        AvaloniaProperty.Register<ChartBase, IBrush?>(nameof(ItemSelectedFill));

    /// <summary>The pen of the mark on a selected record.</summary>
    /// <remarks>
    /// The same accent as <see cref="ItemHoverStroke"/> and a heavier line, which is the one difference the
    /// design brief draws between the record a reader is on and the ones a reader has chosen.
    /// </remarks>
    public static readonly StyledProperty<Pen?> ItemSelectedStrokeProperty =
        AvaloniaProperty.Register<ChartBase, Pen?>(nameof(ItemSelectedStroke));

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

    /// <summary>The fill of the mark on the record the keyboard is on.</summary>
    public IBrush? ItemHoverFill
    {
        get => GetValue(ItemHoverFillProperty);
        set => SetValue(ItemHoverFillProperty, value);
    }

    /// <summary>The pen of the mark on the record the keyboard is on.</summary>
    public Pen? ItemHoverStroke
    {
        get => GetValue(ItemHoverStrokeProperty);
        set => SetValue(ItemHoverStrokeProperty, value);
    }

    /// <summary>The fill of the mark on a selected record.</summary>
    public IBrush? ItemSelectedFill
    {
        get => GetValue(ItemSelectedFillProperty);
        set => SetValue(ItemSelectedFillProperty, value);
    }

    /// <summary>The pen of the mark on a selected record.</summary>
    public Pen? ItemSelectedStroke
    {
        get => GetValue(ItemSelectedStrokeProperty);
        set => SetValue(ItemSelectedStrokeProperty, value);
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
    /// How many records a reader may pick out at once, with the pointer or the keyboard; none, until this says
    /// otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off by default, as it is in Flex, and for a reason that is not only tidiness: selection is what makes a
    /// chart want the keyboard, and a chart that takes the keyboard away from a host's text boxes without being
    /// asked is a chart in the way. Asking for selection is what makes the chart focusable, which is also what
    /// the focus ring is drawn for.
    /// </para>
    /// <para>
    /// Turning selection off lets go of whatever was selected.
    /// </para>
    /// </remarks>
    public SelectionMode SelectionMode
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Focusable = value != SelectionMode.None;

            if (value == SelectionMode.None)
            {
                var before = _selected.ToArray();

                LetGo();
                Announce(before);
            }
        }
    }

    /// <summary>The records that are selected, in the order they were picked; empty when none are.</summary>
    /// <remarks>
    /// The chart's own list rather than a copy - walking it costs nothing, and it is as long as the reader's
    /// selection rather than as long as the data. It is a view: what selects is a click or a key, not writing
    /// to this.
    /// </remarks>
    public IReadOnlyList<ChartItem> SelectedItems => _selected;

    /// <summary>Where the keyboard is, or null when it is nowhere in particular.</summary>
    /// <remarks>
    /// The record the arrows move about, which is not the same as a selected one: Control moves it without
    /// selecting anything, and Shift selects everything between it and the anchor. It is drawn in the lighter of
    /// the two accents, so a reader can see where the keyboard is even when nothing is selected.
    /// </remarks>
    public ChartItem? Caret => _caret;

    /// <summary>Raised when the pointer moves onto a record.</summary>
    /// <remarks>
    /// Made up by the chart: a record is not a visual, so there is nothing to subscribe to. The chart asks what
    /// is under the pointer - the same hit test a data tip uses - and says when the answer changed.
    /// </remarks>
    public event EventHandler<ChartItemEventArgs>? ItemPointerEntered;

    /// <summary>Raised when the pointer moves off a record, or off the chart.</summary>
    public event EventHandler<ChartItemEventArgs>? ItemPointerExited;

    /// <summary>Raised when the pointer moves while a record is under it.</summary>
    public event EventHandler<ChartItemEventArgs>? ItemPointerMoved;

    /// <summary>Raised when the pointer goes down on a record.</summary>
    /// <remarks>Raised whether or not the press goes on to be a click: the press did happen on that record.</remarks>
    public event EventHandler<ChartItemEventArgs>? ItemPointerPressed;

    /// <summary>Raised when the pointer comes up on a record.</summary>
    public event EventHandler<ChartItemEventArgs>? ItemPointerReleased;

    /// <summary>Raised when a record is clicked: the pointer went down and came up on it without dragging.</summary>
    public event EventHandler<ChartItemEventArgs>? ItemClick;

    /// <summary>Raised when a record is clicked twice in a row.</summary>
    /// <remarks>What counts as twice in a row is the platform's answer, not the chart's.</remarks>
    public event EventHandler<ChartItemEventArgs>? ItemDoubleClick;

    /// <summary>Raised when what is selected changes, including when it is let go.</summary>
    public event EventHandler<ChartItemEventArgs>? SelectionChanged;

    /// <summary>What to run when a record is clicked; the same thing as <see cref="ItemClick"/>, for MVVM.</summary>
    /// <remarks>
    /// A command instead of a handler, because a view model cannot subscribe to an event without a view to
    /// subscribe on. The parameter is the <see cref="ChartItemEventArgs"/> the event would have carried.
    /// </remarks>
    public ICommand? ItemClickCommand { get; set; }

    /// <summary>What to run when a record is clicked twice; the command form of <see cref="ItemDoubleClick"/>.</summary>
    public ICommand? ItemDoubleClickCommand { get; set; }

    /// <summary>What to run when what is selected changes; the command form of <see cref="SelectionChanged"/>.</summary>
    public ICommand? SelectionChangedCommand { get; set; }

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
            RefreshTips(new Rect(Bounds.Size));
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
            RefreshTips(new Rect(Bounds.Size));
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
            // A hidden series is not asked what it wants: what the axes cover, what the tips find and what a
            // legend says are all about what the chart is showing.
            if (!series.Visible)
            {
                continue;
            }

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
        RefreshTips(new Rect(Bounds.Size));
    }

    /// <summary>Takes every tip away.</summary>
    protected void HideDataTips()
    {
        _pointerIsOnChart = false;
        RefreshTips(new Rect(Bounds.Size));
    }

    /// <summary>Picks a record out where the pointer clicked, or lets the selection go on empty space.</summary>
    /// <param name="at">Where the click was, in the chart's own coordinates.</param>
    /// <param name="modifiers">The modifiers the click arrived with.</param>
    /// <remarks>
    /// The same three gestures the keyboard has, made with a mouse: Control adds a record to the selection or
    /// takes it out again, Shift takes the selection from the anchor to the record that was clicked, and a plain
    /// click replaces the selection. A click where no record is within reach lets the whole thing go, which is
    /// how a reader clears it.
    /// </remarks>
    protected void SelectAt(Point at, KeyModifiers modifiers)
    {
        if (SelectionMode == SelectionMode.None)
        {
            return;
        }

        var before = _selected.ToArray();
        var found = FindDataPoints(at);
        var hit = found.Count > 0 ? found[0] : (HitData?)null;

        if (hit is not { } picked)
        {
            LetGo();
            Announce(before);
            return;
        }

        // The keyboard carries on from wherever the pointer left off.
        _caret = picked.ChartItem;

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            Extend(picked.ChartItem);
        }
        else
        {
            Pick(picked.ChartItem, modifiers);
        }

        ShowSelection();
        Announce(before);
    }

    /// <summary>Says what the pointer is on, which is where the per-record events come from.</summary>
    /// <param name="at">Where the pointer is, in the chart's own coordinates.</param>
    /// <remarks>
    /// The chart works the answer out rather than asking anything that exists, because a record is not a visual:
    /// it is the same hit test a data tip uses, and the events say when the answer changed and when a pointer
    /// moved while it did not.
    /// </remarks>
    protected void TrackPointer(Point at)
    {
        var hit = HitAt(at);

        if (hit is { } now)
        {
            if (_pointerHit is not { } before || !SameRecord(before.ChartItem, now.ChartItem))
            {
                if (_pointerHit is { } left)
                {
                    Raise(ItemPointerExited, left);
                }

                Raise(ItemPointerEntered, now);
            }

            Raise(ItemPointerMoved, now);
        }
        else if (_pointerHit is { } left)
        {
            Raise(ItemPointerExited, left);
        }

        _pointerHit = hit;
    }

    /// <summary>Says the pointer went down on whatever is at a position.</summary>
    /// <param name="at">Where it went down, in the chart's own coordinates.</param>
    /// <param name="clickCount">How many clicks the platform says this is, which is what makes a double click.</param>
    /// <remarks>
    /// A host driving its own input - a touch pad, a remote, a test - says so through this, the way it does
    /// everything else about the pointer.
    /// </remarks>
    protected void PressAt(Point at, int clickCount = 1)
    {
        _pressClickCount = clickCount;

        if (HitAt(at) is { } hit)
        {
            Raise(ItemPointerPressed, hit);
        }
    }

    /// <summary>Answers the pointer coming up at a position: the record's events, and the selection.</summary>
    /// <param name="at">Where it came up, in the chart's own coordinates.</param>
    /// <param name="modifiers">The modifiers it arrived with.</param>
    /// <param name="clicked">Whether this was a click rather than the end of a drag.</param>
    protected void ReleaseAt(Point at, KeyModifiers modifiers, bool clicked)
    {
        if (HitAt(at) is { } hit)
        {
            Raise(ItemPointerReleased, hit);

            if (clicked)
            {
                Raise(ItemClick, hit, ItemClickCommand);

                if (_pressClickCount >= 2)
                {
                    Raise(ItemDoubleClick, hit, ItemDoubleClickCommand);
                }
            }
        }

        if (clicked)
        {
            SelectAt(at, modifiers);
        }
    }

    /// <summary>Moves the caret the way a key asks it to, and changes the selection the way the modifiers ask.</summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">The modifiers it was pressed with.</param>
    /// <returns><see langword="true"/> when the key did something.</returns>
    /// <remarks>
    /// The semantics the design brief fixes, measured off Flex: the arrows and Home and End move a caret, Space
    /// picks the record the caret is on, Control moves the caret without touching the selection, and Shift takes
    /// the selection from the anchor to wherever the caret went. Nothing wraps round - at the end of a series
    /// the caret stays where it is, which is what Flex's own comment calls "as per some customers' suggestion".
    /// </remarks>
    protected bool Navigate(Key key, KeyModifiers modifiers)
    {
        if (SelectionMode == SelectionMode.None)
        {
            return false;
        }

        var before = _selected.ToArray();

        if (key == Key.Space)
        {
            // Space is the one key that picks rather than moves: it takes the record the caret is already on.
            if (_caret is not { } caret)
            {
                return false;
            }

            Pick(caret, modifiers);
            Announce(before);
            return true;
        }

        if (Arrive(key) is not { } item)
        {
            return false;
        }

        _caret = item;

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            Extend(item);
        }
        else if (!modifiers.HasFlag(KeyModifiers.Control))
        {
            // Control is "move the caret and leave the selection alone"; anything else means the record the
            // caret arrived at is the one the reader is now on, so it is selected.
            Select(item);
        }

        ShowSelection();
        Announce(before);
        return true;
    }

    /// <summary>Where a key sends the caret, or null when it sends it nowhere.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The record the caret should be on.</returns>
    /// <remarks>
    /// Across and along are different things, as they are in Flex: Left and Right walk the records of the series
    /// the caret is in, and Up and Down change series, landing on the first record of the one next door. With no
    /// caret yet - nothing clicked, nothing pressed - every key starts at the first record the chart is showing.
    /// </remarks>
    private ChartItem? Arrive(Key key)
    {
        List<ChartItem> records = _caret is { } caret && caret.Series.Visible ? [.. caret.Series.Items] : [];
        var at = _caret is { } here && here.Series.Visible
            ? records.FindIndex(item => item.Index == here.Index)
            : -1;

        return key switch
        {
            Key.Left => at > 0 ? records[at - 1] : at < 0 ? FirstInView() : null,
            Key.Right => at < 0 ? FirstInView() : at < records.Count - 1 ? records[at + 1] : null,
            Key.Home => records.Count > 0 ? records[0] : FirstInView(),
            Key.End => records.Count > 0 ? records[^1] : LastInView(),
            Key.Up => NeighbouringSeries(-1),
            Key.Down => NeighbouringSeries(1),
            _ => null,
        };
    }

    /// <summary>The series that are part of the chart: the ones that are shown.</summary>
    /// <returns>The series a reader can see, in the order they are declared.</returns>
    private IEnumerable<Series> VisibleSeries() => Series.Where(series => series.Visible);

    /// <summary>The first record in view, walking the series in order.</summary>
    /// <returns>The record, or null when nothing is shown.</returns>
    private ChartItem? FirstInView()
    {
        foreach (var series in VisibleSeries())
        {
            foreach (var item in series.Items)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>The last record in view of the last series that has one.</summary>
    /// <returns>The record, or null when nothing is shown.</returns>
    private ChartItem? LastInView()
    {
        foreach (var series in VisibleSeries().Reverse())
        {
            ChartItem? last = null;

            foreach (var item in series.Items)
            {
                last = item;
            }

            if (last is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>The first record of the series next to the caret's, or of the one before it.</summary>
    /// <param name="step">Which way to look; negative for the series before.</param>
    /// <returns>The record, or null when there is no such series with anything in it.</returns>
    private ChartItem? NeighbouringSeries(int step)
    {
        var series = VisibleSeries().ToArray();
        var from = _caret is { } caret ? Array.IndexOf(series, caret.Series) : step > 0 ? -1 : series.Length;

        for (var index = from + step; index >= 0 && index < series.Length; index += step)
        {
            foreach (var item in series[index].Items)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Makes one record the whole of the selection, and the anchor a range would start from.</summary>
    /// <param name="item">The record.</param>
    private void Select(ChartItem item)
    {
        _selected.Clear();
        _selected.Add(item);
        _anchor = item;
        ShowSelection();
    }

    /// <summary>
    /// Picks a record: replaces the selection with it, or - with Control, where more than one may be selected -
    /// adds it, or takes it out again when it was already there.
    /// </summary>
    /// <param name="item">The record.</param>
    /// <param name="modifiers">The modifiers, which say whether this is a toggle.</param>
    /// <returns><see langword="true"/>, since a record was there to be picked.</returns>
    private bool Pick(ChartItem item, KeyModifiers modifiers)
    {
        if (SelectionMode == SelectionMode.Multiple && modifiers.HasFlag(KeyModifiers.Control))
        {
            if (!_selected.Remove(item))
            {
                _selected.Add(item);
            }

            _anchor = item;
        }
        else
        {
            Select(item);
            return true;
        }

        ShowSelection();
        return true;
    }

    /// <summary>
    /// Takes the selection from the anchor to a record, which is what Shift asks for.
    /// </summary>
    /// <param name="to">Where the caret went.</param>
    /// <remarks>
    /// A range only makes sense along one series, so where the anchor and the record are in different series the
    /// selection is the two of them - which is the same thing Flex settles for. The anchor does not move, so a
    /// reader can widen and narrow the range with the shift key still down.
    /// </remarks>
    private void Extend(ChartItem to)
    {
        if (SelectionMode == SelectionMode.Single || _anchor is not { } from)
        {
            Select(to);
            return;
        }

        _selected.Clear();

        if (!ReferenceEquals(from.Series, to.Series))
        {
            _selected.Add(from);
            _selected.Add(to);
            ShowSelection();
            return;
        }

        var records = from.Series.Items.ToArray();
        var first = Array.FindIndex(records, item => item.Index == from.Index);
        var last = Array.FindIndex(records, item => item.Index == to.Index);

        if (first < 0 || last < 0)
        {
            _selected.Add(to);
            ShowSelection();
            return;
        }

        if (first > last)
        {
            (first, last) = (last, first);
        }

        for (var index = first; index <= last; index++)
        {
            _selected.Add(records[index]);
        }

        ShowSelection();
    }

    /// <summary>Lets go of the selection, the caret and the anchor.</summary>
    private void LetGo()
    {
        _selected.Clear();
        _caret = null;
        _anchor = null;
        ShowSelection();
    }

    /// <summary>Hands what is picked out, and where the keyboard is, to the layer that draws them.</summary>
    private void ShowSelection()
    {
        _selectionLayer.Selected = [.. _selected];
        _selectionLayer.Caret = _caret;
    }

    /// <summary>Says that the selection changed, if it did, and runs whatever a host bound to it.</summary>
    /// <param name="before">What was selected before the change.</param>
    /// <remarks>
    /// Only when it did change: a reader clicking the record that is already the whole of the selection, or
    /// holding Control and moving the caret, has not changed what is selected, and a host that rebuilds a table
    /// for every such event would be answering the same question twice.
    /// </remarks>
    private void Announce(IReadOnlyList<ChartItem> before)
    {
        if (before.Count == _selected.Count && before.SequenceEqual(_selected))
        {
            return;
        }

        var args = new ChartItemEventArgs(_selected);

        SelectionChanged?.Invoke(this, args);
        Run(SelectionChangedCommand, args);
    }

    /// <summary>Raises one of the per-record events, and runs the command that stands for it.</summary>
    /// <param name="raised">The event.</param>
    /// <param name="hit">The record it is about.</param>
    /// <param name="command">The command a host may have bound instead, if there is one for this event.</param>
    private void Raise(EventHandler<ChartItemEventArgs>? raised, HitData hit, ICommand? command = null)
    {
        var args = new ChartItemEventArgs(_selected, hit);

        raised?.Invoke(this, args);
        Run(command, args);
    }

    /// <summary>Runs a command, when there is one and it says it can run.</summary>
    /// <param name="command">The command.</param>
    /// <param name="args">What it is given.</param>
    private static void Run(ICommand? command, ChartItemEventArgs args)
    {
        if (command?.CanExecute(args) == true)
        {
            command.Execute(args);
        }
    }

    /// <summary>The record under a point, when there is one within reach of the pointer.</summary>
    /// <param name="at">The position, in the chart's own coordinates.</param>
    /// <returns>The nearest hit, or null.</returns>
    private HitData? HitAt(Point at)
    {
        var found = FindDataPoints(at);
        return found.Count > 0 ? found[0] : null;
    }

    /// <summary>Whether two hits are about the same record.</summary>
    /// <param name="first">One record.</param>
    /// <param name="second">The other.</param>
    /// <returns>True when both are the same record of the same series.</returns>
    private static bool SameRecord(ChartItem first, ChartItem second) =>
        ReferenceEquals(first.Series, second.Series) && first.Index == second.Index;

    /// <summary>Drops whatever a series had selected, and the caret if it was in that series.</summary>
    /// <param name="series">The series that was hidden.</param>
    /// <remarks>
    /// A mark is drawn at a record's place on the chart, and a hidden series has no place: leaving its records
    /// selected would leave marks for records that are not there, or bring them back when the series is shown
    /// again long after the reader stopped thinking about them.
    /// </remarks>
    private void Forget(Series series)
    {
        _selected.RemoveAll(item => ReferenceEquals(item.Series, series));

        if (_caret is { } caret && ReferenceEquals(caret.Series, series))
        {
            _caret = null;
        }

        if (_anchor is { } anchor && ReferenceEquals(anchor.Series, series))
        {
            _anchor = null;
        }

        ShowSelection();
    }

    /// <summary>
    /// Works out which readings are being shown, walks the tips into place, and puts the rest of the pool away.
    /// </summary>
    /// <remarks>
    /// Called whenever anything the answer depends on moves: the pointer, the mode, or the plot itself, since a
    /// tip is placed against the chart's bounds and those move when the chart is laid out again.
    /// </remarks>
    private void RefreshTips(Rect chart)
    {
        _tipReadings = Readings();

        for (var index = 0; index < _tipReadings.Length; index++)
        {
            var tip = Tip(index);

            tip.Data = _tipReadings[index];
            // The text comes from the group when the pointer put the tip there - one tip, every reading near it -
            // and from the reading itself otherwise, which is the case when every reading in view has a tip.
            tip.Text = index < _tipTexts.Length && _tipTexts[index] is { } text
                ? text
                : Line(_tipReadings[index]);
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
        _tipTexts = [];

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

        // One tip carrying every reading the pointer is near, not only the nearest one. Two lines crossing are
        // two readings, and a reader who points at the crossing is asking about both of them: a tip that showed
        // one and hid the other would be answering a question nobody asked. The lines are the ones a tip of its
        // own would write - the series' name and the reading the axes wrote - so the tip reads the way the ones
        // already on the chart read.
        var found = FindDataPoints(_tipAnchor, MouseSensitivity);

        if (found.Count == 0)
        {
            return [];
        }

        _tipTexts = [string.Join('\n', found.Select(Line))];
        return [found[0]];
    }

    /// <summary>One line of a tip: the reading as the axes wrote it, or as the host asked for it.</summary>
    /// <param name="hit">The reading.</param>
    /// <returns>The line.</returns>
    private string Line(HitData hit) => DataTipFunction?.Invoke(hit) ?? hit.DisplayText;

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
            var left = Math.Clamp(slot.Left, chart.Left, Math.Max(chart.Left, chart.Right - slot.Width));
            var top = Math.Clamp(slot.Top, chart.Top, Math.Max(chart.Top, chart.Bottom - slot.Height));

            Canvas.SetLeft(slot.Tip, left);
            Canvas.SetTop(slot.Tip, top);

            // And put it there now rather than waiting for the layout pass that the two lines above ask for: a
            // tip worked out during a layout pass would otherwise be painted at the corner it was created in
            // until the next one, which is one frame of a tip in the wrong place.
            slot.Tip.Arrange(new Rect(left, top, slot.Width, slot.Height));
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
    public IEnumerable<ChartItem> GetAllDataPoints() =>
        VisibleSeries().SelectMany(series => series.Items);

    /// <summary>Every data point inside a rectangle, series by series.</summary>
    /// <param name="region">The rectangle, in the chart's own coordinates.</param>
    /// <returns>The records, series by series, in data order.</returns>
    public IReadOnlyList<ChartItem> GetItemsInRegion(Rect region)
    {
        var items = new List<ChartItem>();

        foreach (var series in Series)
        {
            if (!series.Visible)
            {
                continue;
            }

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

        // The tips are worked out and placed against the plot, so both happen after it. Working them out again
        // rather than only moving them is what makes asking for every tip work on a chart that has not been laid
        // out yet: at the moment the property is set there is no plot, so nothing is in view and there is nothing
        // to show - and this pass is the one that finds out what there is.
        _tips.Arrange(bounds);

        if (_pointerIsOnChart || ShowAllDataTips)
        {
            RefreshTips(bounds);
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
    /// <para>
    /// Announced from the layout pass rather than from the operations that usually move the window, because those
    /// are not the only things that do: a resize moves it, a series arriving moves it, a change of theme does.
    /// Everything that moves the window is worth hearing about, and comparing what the axes worked out covers
    /// all of them in one place.
    /// </para>
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

        // Dragging the content to the right, or up, shows earlier or lower data, so the window slides the other
        // way by as many units as the pixels are worth at this scale. The sign is not the same on both
        // dimensions: a screen's y grows downwards and a value grows upwards, so a drag upwards - a negative
        // distance - is the one that moves the window up the values.
        var units = (dimension == ChartDimension.X ? -pixels : pixels) * (window.Range.Length / window.Length);

        SetWindow(window, window.Range.Minimum + units, window.Range.Maximum + units);
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

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // A press is where a reader's attention is, whether or not anything comes of it: the chart takes the
        // keyboard so that the arrows work on what was just clicked. A chart with nothing selectable is not
        // focusable, so this does nothing there.
        Focus();
        PressAt(e.GetPosition(this), e.ClickCount);

        if (!PanOnDrag)
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

        TrackPointer(at);
        MoveCrosshair(at);
        ShowDataTipsAt(at);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        // Leaving the chart is leaving whatever record the pointer was on, which is the one way a reader who
        // moved the pointer off the side hears about it.
        if (_pointerHit is { } left)
        {
            Raise(ItemPointerExited, left);
            _pointerHit = null;
        }

        HideCrosshair();
        HideDataTips();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Navigate(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
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

        // From here the window is moving and the readings are not: a series may draw from the thinning it kept
        // rather than work one out again. What that draws is provisional, and letting go throws it away.
        SeriesLayer.IsDragging = true;

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

        var moved = _isPanning && _panHasStarted;

        if (_isPanning)
        {
            DragEnd();
            e.Pointer.Capture(null);
        }

        e.Handled = moved;

        // A press that never became a drag is a click, which is what the threshold is for: picking a record out
        // and panning the chart would otherwise be the same gesture. Asking about the press rather than about
        // what is captured keeps this working on a chart that does not pan at all.
        var clicked = !moved && e.InitialPressMouseButton == MouseButton.Left;

        ReleaseAt(e.GetPosition(this), e.KeyModifiers, clicked);
    }

    /// <summary>Ends a drag.</summary>
    protected void DragEnd()
    {
        _isPanning = false;
        Cursor = _cursorBeforePan;
        _panHasStarted = false;

        // The picture drawn while the window was moving was a provisional one; this puts the exact one back.
        SeriesLayer.IsDragging = false;
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
        Restack();

        // A layer that arrived after the theme did still has to be dressed.
        ApplyInks();
    }

    /// <summary>
    /// What a layer paints at: the furniture first, the data over it, and the readings over that.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <returns>The rank; layers of the same rank keep the order they arrived in.</returns>
    /// <remarks>
    /// Written down rather than left to the order the layers happen to be created in, which is what it used to
    /// be and what it got wrong: a chart type makes its axes in its own constructor, after the layers this class
    /// makes, so the grid was painted over the series and the axis labels over the crosshair's readouts. A layer
    /// of a kind that is not one of these goes with the furniture.
    /// </remarks>
    private static int Rank(ChartLayer layer) => layer switch
    {
        AnnotationLayer _ => 0,
        SeriesLayer _ => 2,
        SelectionLayer _ => 3,
        CrosshairLayer _ => 4,
        _ => 1,
    };

    /// <summary>Puts the layers in paint order, and keeps the tips on top of all of them.</summary>
    private void Restack()
    {
        for (var index = 0; index < _layers.Count; index++)
        {
            var wanted = _layers.OrderBy(Rank).ElementAt(index);
            var at = VisualChildren.IndexOf(wanted);

            if (at != index)
            {
                VisualChildren.Move(at, index);
            }
        }

        var top = VisualChildren.IndexOf(_tips);

        if (top >= 0 && top != VisualChildren.Count - 1)
        {
            VisualChildren.Move(top, VisualChildren.Count - 1);
        }
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

        // An axis' renderer is the caller's to tune whenever they like - a knob beside the chart, a preference -
        // and its settings change how the axis is drawn and how much room it takes rather than what it covers, so
        // nothing else notices. This is where the chart does: the plot is worked out again, because hiding the
        // labels gives the gutter's room back, and the axis is painted again.
        renderer.Changed += (_, _) =>
        {
            InvalidateArrange();
            layer.InvalidateVisual();
        };

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
            var layout = Transform.GetLayout(axis.Renderer.Axis);
            var gutter = axis.Renderer.MeasureGutter(layout);
            var overhang = axis.Renderer.MeasureOverhang(layout);
            var far = axis.Renderer.Placement == AxisPlacement.End;

            if (axis.Renderer.IsVertical)
            {
                // Its labels stand beside the plot and reach half a label past the top and the bottom of it, so
                // both ends of this dimension need room. The bottom usually has it already - the horizontal axis
                // is measured into the same corner - and the top usually does not.
                top = Math.Max(top, overhang);
                bottom = Math.Max(bottom, overhang);

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
                // Its labels run along the plot and reach half a label past its left and right edges, so both
                // ends of this dimension need room: the left usually has it from the vertical axis' gutter, the
                // right usually does not.
                right = Math.Max(right, overhang);
                left = Math.Max(left, overhang);

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
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // A list that was cleared or built again wholesale says nothing about what left it, so the answer is
            // not to work out the difference: everything this chart registered as a series goes, and whatever is
            // in the list now arrives. Working it out from the event would quietly leave a cleared chart holding
            // the series it used to have.
            foreach (var gone in _transform.Elements.OfType<Series>().ToArray())
            {
                Forget(gone);
                Leave(gone);
            }

            foreach (var series in Series)
            {
                Join(series);
            }
        }
        else
        {
            if (e.OldItems is not null)
            {
                foreach (Series series in e.OldItems)
                {
                    // A series that is gone from the chart cannot have anything selected on it any more.
                    Forget(series);
                    Leave(series);
                }
            }

            if (e.NewItems is not null)
            {
                foreach (Series series in e.NewItems)
                {
                    Join(series);
                }
            }
        }

        ApplyPalette();
        SeriesChanged();

        // A new series may want more of an axis than the old ones did, which can move the plot.
        InvalidateArrange();
        _seriesLayer.InvalidateVisual();
    }

    /// <summary>Takes a series into the chart: ranged, drawn, listened to.</summary>
    /// <param name="series">The series.</param>
    private void Join(Series series)
    {
        _transform.AddElement(series);
        _seriesLayer.Add(series);

        // A series that puts on a colour of its own later gives up its slot, so the ones after it move up; and a
        // series that is hidden, pushed to or invalidated says so through its own events.
        series.Invalidated += OnSeriesInvalidated;
        series.Pushed += OnSeriesPushed;
        series.VisibleChanged += OnSeriesVisibleChanged;
    }

    /// <summary>Lets a series out of the chart.</summary>
    /// <param name="series">The series.</param>
    private void Leave(Series series)
    {
        _transform.RemoveElement(series);
        _seriesLayer.Remove(series);
        series.Invalidated -= OnSeriesInvalidated;
        series.Pushed -= OnSeriesPushed;
        series.VisibleChanged -= OnSeriesVisibleChanged;
    }

    private void OnSeriesInvalidated(object? sender, EventArgs e)
    {
        ApplyPalette();
        SeriesChanged();

        // A series says it changed when its readings did, and what an axis covers and what its labels say are
        // read off those readings in a layout pass - so a chart that only repainted would draw the new line
        // against the old grid, and the grid would be the one the chart opened with. It is the same door the
        // pushed batch comes through, and it says the same thing.
        InvalidateArrange();

        // And every layer is asked to paint, not only the series: a layer is told where the plot is and not what
        // it shows, so a window that slid while the plot stood still would leave the axes drawing the ticks and
        // the labels of a window that is gone - a live chart with its clock frozen.
        InvalidateLayers();
    }

    /// <summary>Answers an element being added to or taken off the chart.</summary>
    /// <param name="sender">The element list.</param>
    /// <param name="e">What changed.</param>
    /// <remarks>
    /// The same shape as the series list, one step simpler: an element has no data of its own to read, so
    /// nothing has to be applied when it arrives - it is registered with the transform, painted under the series
    /// and left alone until it says it changed.
    /// </remarks>
    private void OnElementsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // The same wholesale answer as the series list: what was registered as an element goes, and what is
            // in the list now arrives.
            foreach (var gone in _transform.Elements.OfType<ChartElement>().ToArray())
            {
                Leave(gone);
            }

            foreach (var element in Elements)
            {
                Join(element);
            }
        }
        else
        {
            if (e.OldItems is not null)
            {
                foreach (ChartElement element in e.OldItems)
                {
                    Leave(element);
                }
            }

            if (e.NewItems is not null)
            {
                foreach (ChartElement element in e.NewItems)
                {
                    Join(element);
                }
            }
        }

        // An element that describes data can move the plot, so the chart is laid out again rather than only
        // repainted.
        InvalidateArrange();
    }

    /// <summary>Takes an element into the chart: ranged and painted.</summary>
    /// <param name="element">The element.</param>
    private void Join(ChartElement element)
    {
        _transform.AddElement(element);
        _annotationLayer.Add(element);
        element.Invalidated += OnElementInvalidated;
    }

    /// <summary>Lets an element out of the chart.</summary>
    /// <param name="element">The element.</param>
    private void Leave(ChartElement element)
    {
        _transform.RemoveElement(element);
        _annotationLayer.Remove(element);
        element.Invalidated -= OnElementInvalidated;
    }

    private void OnElementInvalidated(object? sender, EventArgs e) => _annotationLayer.InvalidateVisual();

    /// <summary>Answers a series being shown or hidden, which is a layout's business and not only a paint's.</summary>
    /// <param name="sender">The series.</param>
    /// <param name="e">Unused.</param>
    /// <remarks>
    /// A series that is hidden stops covering anything, so the axes have to be worked out again: the ranges
    /// change and so does the room the plot gives up for a second axis the series brought with it. That is what
    /// the design brief means by a hidden series taking no part in the layout - and it is why this is not the
    /// repaint <see cref="Charts.Series.Invalidated"/> asks for.
    /// </remarks>
    private void OnSeriesVisibleChanged(object? sender, EventArgs e)
    {
        var before = _selected.ToArray();

        if (sender is Series series && !series.Visible)
        {
            Forget(series);
        }

        Announce(before);
        SeriesChanged();
        InvalidateArrange();

        // Every layer, not just the series': hiding a series is a pass the chart has to re-lay-out, and the axes
        // are laid out from what is left - their labels say another range now. Repainting only the series layer
        // leaves the axis layer drawing the range the hidden series asked for, which a Linux machine showed and a
        // Windows one hid: the label of a wider number is a wider label, so the plot gave up a little more room
        // there and the axis layer re-recorded itself for a reason that had nothing to do with the labels.
        InvalidateLayers();
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

    /// <summary>
    /// Applies every series' pushed records, on the UI thread, and works the chart out again if any arrived.
    /// </summary>
    /// <remarks>
    /// Laid out again and not merely repainted: where the axes put their lines and what their labels say is
    /// worked out in a layout pass, so a chart that only repaints keeps the labels it had - which is a live chart
    /// whose axis never moves. A collection that is added to goes through the same door, and for the same reason.
    /// </remarks>
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
            InvalidateArrange();
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

                case SelectionLayer selection:
                    selection.CaretFill = ItemHoverFill;
                    selection.CaretStroke = ItemHoverStroke;
                    selection.SelectedFill = ItemSelectedFill;
                    selection.SelectedStroke = ItemSelectedStroke;
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
