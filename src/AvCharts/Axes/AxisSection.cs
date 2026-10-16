using Avalonia.Media;


using AvCharts.Data;

using AvCharts.Elements;

namespace AvCharts.Axes;

/// <summary>
/// A stretch of an axis marked out and, if it is named, named: the shaded slab in the gutter between two readings,
/// behind the labels.
/// </summary>
/// <remarks>
/// <para>
/// The same idea as <see cref="Section"/>, in the other place: where that one shades a band across the plot, this
/// one shades the axis itself - "the night shift", "the release window", "out of tolerance" - so that a reader
/// looking at the scale under the readings is told what that stretch of it means. It stands behind the labels
/// rather than beside them, so an axis section takes no room of its own.
/// </para>
/// <para>
/// A section that is <em>named</em> takes over what is written there: the labels inside its stretch are dropped, so
/// that the name is not written over them. That is the whole reason to name one - an axis stretch whose name is
/// buried under three tick labels is a stretch nobody reads - and it is why the labels come back where the section
/// is not.
/// </para>
/// <para>
/// Either end may be left out, and then the slab runs to the end of the axis; a value the axis cannot place draws
/// nothing at all, the same rule as <see cref="Section"/>.
/// </para>
/// </remarks>
public class AxisSection
{
    private ChartValue? _from;
    private ChartValue? _to;
    private string? _name;

    /// <summary>Raised when a setting changed, so that the axis that holds it can be drawn again.</summary>
    public event EventHandler? Changed;

    /// <summary>The reading the slab starts at; unset, it starts at the end of the axis.</summary>
    public ChartValue? From
    {
        get => _from;
        set
        {
            if (Nullable.Equals(_from, value))
            {
                return;
            }

            _from = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The reading it ends at; unset, it ends at the other end of the axis.</summary>
    public ChartValue? To
    {
        get => _to;
        set
        {
            if (Nullable.Equals(_to, value))
            {
                return;
            }

            _to = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>What the slab is filled with; unset, it is not filled and only its ends are drawn.</summary>
    public IBrush? Fill { get; set; }

    /// <summary>The pen the two ends of the slab are drawn with; unset, the ends are not drawn.</summary>
    public Pen? Border { get; set; }

    /// <summary>What the stretch is called; unset, the labels inside it stay where they are.</summary>
    public string? Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The brush the name is written in; unset, the border's brush, and then the fill's own colour.</summary>
    public IBrush? NameBrush { get; set; }

    /// <summary>The size the name is written at, in points.</summary>
    public double FontSize { get; set; } = 11;
}
