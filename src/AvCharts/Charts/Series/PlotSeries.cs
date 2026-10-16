using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// A series drawn as marks at its points, with nothing joined up: the shape Flex calls a plot.
/// </summary>
/// <remarks>
/// <para>
/// The base every series with marks is built on, which is where this differs from Flex: there each of a plot, a
/// line, an area and a bubble carries its own copy of the mark code. What they share is one thing - a mark where
/// a record is - so it is written once. A <see cref="LineSeries{T}"/> adds a line through the marks, an
/// <see cref="AreaSeries{T}"/> fills under it, and a <see cref="BubbleSeries{T}"/> says how big each mark is.
/// </para>
/// <para>
/// A plot is drawn as marks, so it draws them: a circle at each point, filled with the series' colour held back
/// to a tint and outlined in the colour itself, which is what makes a mark read as a shape rather than a smudge.
/// A series drawn as a line turns them off - a line is the shape, and the readings are where it passed rather
/// than what it is - and a host that wants no marks at all sets <see cref="Marker"/> to null.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class PlotSeries<T> : CartesianSeries<T>
{
    private IMarkerRenderer? _marker = Markers.Circle;
    private double _markerSize = 4;
    private IBrush? _markerFill;
    private Pen? _markerStroke;
    private Geometry? _marks;

    /// <summary>The mark drawn at each point: a circle by default, and nothing at all when set to null.</summary>
    /// <remarks>
    /// A plot with no marks draws nothing whatever - it is the marks it is made of - so one is drawn unless the
    /// host says otherwise. A series drawn as a line is the other way round and turns them off; see
    /// <see cref="LineSeries{T}"/>.
    /// </remarks>
    public IMarkerRenderer? Marker
    {
        get => _marker;
        set
        {
            if (ReferenceEquals(_marker, value))
            {
                return;
            }

            _marker = value;
            InvalidateGeometry();
        }
    }

    /// <summary>How far a mark reaches from its point, in logical pixels.</summary>
    public double MarkerSize
    {
        get => _markerSize;
        set
        {
            if (_markerSize.Equals(value))
            {
                return;
            }

            _markerSize = value;
            InvalidateGeometry();
        }
    }

    /// <summary>The colour inside the marks; without one, the chart's palette fills them.</summary>
    public IBrush? MarkerFill
    {
        get => _markerFill;
        set
        {
            if (ReferenceEquals(_markerFill, value))
            {
                return;
            }

            _markerFill = value;
            Invalidate();
        }
    }

    /// <summary>The outline of the marks; without one, a mark is outlined in its own colour.</summary>
    /// <remarks>
    /// A fill says what a mark is and an outline says where it ends: the palette's fill is the series' colour
    /// held back to a tint, so the outline of a mark is the colour that tint is a tint of, at full strength.
    /// Handing in a pen overrides both, for a shape that wants an edge of its own - a white ring around a mark
    /// on a busy plot, or no edge at all.
    /// </remarks>
    public Pen? MarkerStroke
    {
        get => _markerStroke;
        set
        {
            if (ReferenceEquals(_markerStroke, value))
            {
                return;
            }

            _markerStroke = value;
            Invalidate();
        }
    }

    /// <inheritdoc />
    public override bool HasOwnColours => MarkerFill is not null;

    /// <inheritdoc />
    public override IBrush? Color => MarkerFill ?? base.Color;

    /// <summary>How big the mark at one record is: the same for every record, unless a subclass says otherwise.</summary>
    /// <param name="index">The position of the record.</param>
    /// <param name="context">Where the plot is and how to map values into it.</param>
    /// <returns>The radius, in logical pixels.</returns>
    protected virtual double RadiusOf(int index, in SeriesRenderContext context) => MarkerSize;

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        // The marks come straight from the records rather than from the runs, so that a series whose marks differ
        // from one point to the next - a bubble - can say so. The runs are for whatever a subclass draws between
        // the points.
        var marker = Marker;

        if (marker is null)
        {
            _marks = null;
        }
        else
        {
            var batch = new GeometryBatch();

            foreach (var (index, _, _, point) in PlacedRecords(context))
            {
                marker.AddTo(batch, point, RadiusOf(index, in context));
            }

            _marks = batch.Build();
        }

        BuildInk(in context);
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        var fill = MarkerFill ?? PaletteFill;
        var outline = MarkerStroke ?? PaletteStroke ?? OutlineOf(fill);

        // Either of them is worth drawing on its own: a mark with no fill and an outline is a ring, and a mark
        // with a fill and no outline is a blob - both are things a host asks for by setting one and not the other.
        if (_marks is not null && (fill is not null || outline is not null))
        {
            context.DrawGeometry(fill, outline, _marks);
        }
    }

    /// <summary>Builds whatever this series draws between its points; nothing at all, for a plot.</summary>
    /// <param name="context">Where the plot is and how to map values into it.</param>
    protected virtual void BuildInk(in SeriesRenderContext context)
    {
    }
}
