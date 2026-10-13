using Avalonia.Media;

namespace AvCharts.Charts;

/// <summary>
/// A series whose points are joined by a line, with an optional mark at each point.
/// </summary>
/// <remarks>
/// The line and the marks are two geometries, because they are two different fills: one cloud of circles in the
/// marker colour and one run through them in the line colour. Both are built once per change and kept, so a
/// still chart repaints from cached geometry.
/// </remarks>
/// <typeparam name="T">The type of one item in <see cref="CartesianSeries{T}.ItemsSource"/>.</typeparam>
public class LineSeries<T> : CartesianSeries<T>
{
    private IMarkerRenderer? _marker;
    private double _markerSize = 4;
    private IBrush? _markerFill;
    private IBrush? _stroke;
    private double _strokeThickness = 2;
    private Geometry? _lineGeometry;
    private Geometry? _markerGeometry;

    /// <summary>The mark drawn at each point; none by default, which draws a plain line.</summary>
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

    /// <summary>How far the mark reaches from the point, in logical pixels.</summary>
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

    /// <summary>The fill of the marks. Without one, marks are measured but not painted.</summary>
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

    /// <summary>The colour of the line. Without one, no line is painted.</summary>
    public IBrush? Stroke
    {
        get => _stroke;
        set
        {
            if (ReferenceEquals(_stroke, value))
            {
                return;
            }

            _stroke = value;
            Invalidate();
        }
    }

    /// <summary>The width of the line, in logical pixels.</summary>
    public double StrokeThickness
    {
        get => _strokeThickness;
        set
        {
            if (_strokeThickness.Equals(value))
            {
                return;
            }

            _strokeThickness = value;
            Invalidate();
        }
    }

    /// <inheritdoc />
    public override bool HasOwnColours => Stroke is not null;

    /// <inheritdoc />
    public override IBrush? Color => Stroke ?? base.Color;

    /// <inheritdoc />
    protected override void BuildGeometry(in SeriesRenderContext context)
    {
        var runs = BuildRuns(context);

        var line = new GeometryBatch();
        var marker = Marker is null ? null : new GeometryBatch();

        foreach (var run in runs)
        {
            if (run.Count > 1)
            {
                line.AddPolyline(run);
            }

            if (marker is null)
            {
                continue;
            }

            foreach (var point in run)
            {
                Marker!.AddTo(marker, point, MarkerSize);
            }
        }

        _lineGeometry = line.Build();
        _markerGeometry = marker?.Build();
    }

    /// <inheritdoc />
    protected override void Draw(DrawingContext context, in SeriesRenderContext renderContext)
    {
        if (_markerGeometry is not null && MarkerFill is not null)
        {
            context.DrawGeometry(MarkerFill, null, _markerGeometry);
        }

        // The line is drawn in the series' own colour when it has one, and in the colour the chart took out of
        // its palette for this series otherwise.
        var stroke = Stroke ?? PaletteStroke?.Brush;

        if (_lineGeometry is not null && stroke is not null)
        {
            context.DrawGeometry(null, new Pen(stroke, StrokeThickness), _lineGeometry);
        }
    }
}
