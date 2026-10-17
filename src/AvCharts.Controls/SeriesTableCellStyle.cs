using Avalonia;
using Avalonia.Media;

namespace AvCharts.Controls;

/// <summary>The ink and the box one table cell draws for itself.</summary>
/// <remarks>
/// A cell's style is separate from its text: a formatter or <see cref="SeriesTable.SetCellText"/> decides what a
/// cell says, while this decides the paper behind it, the colour of the words and the border around it. Null
/// means "use the table's own": an unset background or border leaves the cell transparent.
/// </remarks>
public sealed class SeriesTableCellStyle
{
    /// <summary>Creates a style for one cell.</summary>
    /// <param name="Background">The fill behind the words, or null for no fill.</param>
    /// <param name="Foreground">The colour of the words, or null for the table's.</param>
    /// <param name="BorderBrush">The colour of the border, or null for no border.</param>
    /// <param name="BorderThickness">The width of the border.</param>
    public SeriesTableCellStyle(
        IBrush? Background = null,
        IBrush? Foreground = null,
        IBrush? BorderBrush = null,
        Thickness BorderThickness = default)
    {
        this.Background = Background;
        this.Foreground = Foreground;
        this.BorderBrush = BorderBrush;
        this.BorderThickness = BorderThickness;
    }

    /// <summary>The fill behind the words.</summary>
    public IBrush? Background { get; }

    /// <summary>The colour of the words.</summary>
    public IBrush? Foreground { get; }

    /// <summary>The colour of the border.</summary>
    public IBrush? BorderBrush { get; }

    /// <summary>The width of the border.</summary>
    public Thickness BorderThickness { get; }
}
