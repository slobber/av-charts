using Avalonia;
using Avalonia.Media;


using AvCharts.Data;



namespace AvCharts.Drawing;

/// <summary>
/// The layer that draws a mark on the records a reader has picked out, and on the one the keyboard is on.
/// </summary>
/// <remarks>
/// <para>
/// A layer of its own rather than something each series draws, because picking a record out is the chart's
/// business rather than any series': a line, a column and a bubble are all selected the same way, and a series
/// that had to draw its own selection would need a second way of drawing for every shape there is. It also
/// keeps the repaint where the layering says it belongs - a change of selection re-records this layer and
/// nothing else.
/// </para>
/// <para>
/// What is drawn is a ring at the record's own position, in the two accents the theme hands over: the lighter
/// one for the record the keyboard is on, the heavier for the ones that are selected. A record that is both is
/// drawn once, as selected - the two states differ only in how heavy the ring is, which is what the tokens say.
/// </para>
/// </remarks>
public class SelectionLayer : ChartLayer
{
    private IReadOnlyList<ChartItem> _selected = [];
    private ChartItem? _caret;
    private Rect? _band;

    /// <summary>The records that are selected; assign a new list, which is what asks for the repaint.</summary>
    public IReadOnlyList<ChartItem> Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
            {
                return;
            }

            _selected = value ?? [];
            InvalidateVisual();
        }
    }

    /// <summary>The record the keyboard is on, or null when it is on none.</summary>
    public ChartItem? Caret
    {
        get => _caret;
        set
        {
            if (Nullable.Equals(_caret, value))
            {
                return;
            }

            _caret = value;
            InvalidateVisual();
        }
    }

    /// <summary>The fill inside the ring around the record the keyboard is on.</summary>
    public IBrush? CaretFill { get; set; }

    /// <summary>The pen of the ring around the record the keyboard is on.</summary>
    public Pen? CaretStroke { get; set; }

    /// <summary>The fill inside the ring around a selected record.</summary>
    public IBrush? SelectedFill { get; set; }

    /// <summary>The pen of the ring around a selected record.</summary>
    public Pen? SelectedStroke { get; set; }

    /// <summary>
    /// The rectangle being dragged over the readings, or null when none is: the band a reader picks a stretch out
    /// with, drawn while the pointer is down.
    /// </summary>
    /// <remarks>
    /// Assigning a new one asks for the repaint, the way <see cref="Selected"/> does - this is the one thing here
    /// that moves many times while a gesture is running, and it moves alone.
    /// </remarks>
    public Rect? Band
    {
        get => _band;
        set
        {
            if (Nullable.Equals(_band, value))
            {
                return;
            }

            _band = value;
            InvalidateVisual();
        }
    }

    /// <summary>The fill inside the band.</summary>
    public IBrush? BandFill { get; set; }

    /// <summary>The pen around the band.</summary>
    public Pen? BandStroke { get; set; }

    /// <summary>How far a mark reaches from the record, in logical pixels; five.</summary>
    public double Radius { get; set; } = 5;

    /// <inheritdoc />
    protected override void DrawLayer(DrawingContext context)
    {
        // The band first, so that the rings are read over it rather than under it: a reader dragging a stretch out
        // is looking at the records it is taking, and those are what the rings are on.
        if (_band is { } band && (BandFill is not null || BandStroke is not null))
        {
            context.DrawRectangle(BandFill, BandStroke, band);
        }

        foreach (var item in _selected)
        {
            Draw(context, item, SelectedFill, SelectedStroke);
        }

        if (_caret is { } caret && !_selected.Contains(caret))
        {
            Draw(context, caret, CaretFill, CaretStroke);
        }
    }

    /// <summary>Draws one mark, if the record it is for can be placed at all.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="item">The record.</param>
    /// <param name="fill">The fill inside the ring.</param>
    /// <param name="stroke">The pen of the ring.</param>
    private void Draw(DrawingContext context, ChartItem item, IBrush? fill, Pen? stroke)
    {
        if (item.Position is not { } at || fill is null && stroke is null)
        {
            return;
        }

        context.DrawEllipse(fill, stroke, at, Radius, Radius);
    }
}
