using Avalonia;
using Avalonia.Media;

namespace AvCharts.Controls;

/// <summary>A border along a table row or column.</summary>
/// <remarks>
/// A row uses the top and bottom thickness; a column uses the left and right. The same class is used for both so
/// that a host can style a rule once and put it on either edge without translating it into a second vocabulary.
/// </remarks>
public sealed class SeriesTableBorderStyle
{
    /// <summary>Creates a border style.</summary>
    /// <param name="Brush">The colour of the border, or null for none.</param>
    /// <param name="Thickness">The edges of the border to draw.</param>
    public SeriesTableBorderStyle(IBrush? Brush = null, Thickness Thickness = default)
    {
        this.Brush = Brush;
        this.Thickness = Thickness;
    }

    /// <summary>The colour of the border.</summary>
    public IBrush? Brush { get; }

    /// <summary>The edges and widths of the border.</summary>
    public Thickness Thickness { get; }
}
