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
    private int _pumpIsPosted;

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

        var bounds = new Rect(finalSize);
        PlotBounds = LayoutPlot(bounds);

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

        foreach (var layer in _layers)
        {
            layer.Configure(PlotBounds, scaling);
            layer.Arrange(bounds);
        }

        return finalSize;
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

            plot = next;
        }
    }

    private Rect Deflate(Rect bounds, Rect plot)
    {
        var left = 0d;
        var bottom = 0d;

        foreach (var layer in _layers)
        {
            if (layer is not AxisLayer axis)
            {
                continue;
            }

            var gutter = axis.Renderer.MeasureGutter(Transform.GetLayout(axis.Renderer.Dimension));

            if (axis.Renderer.IsVertical)
            {
                left = Math.Max(left, gutter);
            }
            else
            {
                bottom = Math.Max(bottom, gutter);
            }
        }

        return new Rect(
            bounds.X + left,
            bounds.Y,
            Math.Max(0, bounds.Width - left),
            Math.Max(0, bounds.Height - bottom));
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

        // A new series may want more of an axis than the old ones did, which can move the plot.
        InvalidateArrange();
        _seriesLayer.InvalidateVisual();
    }

    private void OnSeriesInvalidated(object? sender, EventArgs e) => ApplyPalette();

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
