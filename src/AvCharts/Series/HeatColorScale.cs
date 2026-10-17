using Avalonia.Media;

namespace AvCharts.Series;

/// <summary>
/// The colours a heat map moves through as its values rise.
/// </summary>
/// <remarks>
/// The scale is a list of evenly spaced colours rather than a fixed palette: a heat map has one continuous
/// quantity, so its colour has to change continuously too. Values below or above the range take the first or the
/// last colour instead of wrapping around, which keeps a clipped reading visibly clipped.
/// </remarks>
public sealed class HeatColorScale
{
    private readonly Color[] _colors;

    /// <summary>Creates a scale from colours in increasing-value order.</summary>
    /// <param name="colors">The colours; at least one.</param>
    public HeatColorScale(params Color[] colors)
    {
        ArgumentNullException.ThrowIfNull(colors);

        if (colors.Length == 0)
        {
            throw new ArgumentException("A heat scale needs at least one colour.", nameof(colors));
        }

        _colors = [.. colors];
    }

    /// <summary>The colours, low to high.</summary>
    public IReadOnlyList<Color> Colors => _colors;

    /// <summary>The colour for one value in a range.</summary>
    /// <param name="value">The value.</param>
    /// <param name="minimum">The value that takes the first colour.</param>
    /// <param name="maximum">The value that takes the last colour.</param>
    /// <returns>The interpolated colour; the middle colour when the range has no length.</returns>
    public Color ColorAt(double value, double minimum, double maximum)
    {
        if (_colors.Length == 1 || double.IsNaN(value) || double.IsNaN(minimum) || double.IsNaN(maximum))
        {
            return _colors[0];
        }

        var position = maximum > minimum
            ? Math.Clamp((value - minimum) / (maximum - minimum), 0, 1)
            : 0.5;
        var across = position * (_colors.Length - 1);
        var left = Math.Min((int)Math.Floor(across), _colors.Length - 1);
        var right = Math.Min(left + 1, _colors.Length - 1);
        var fraction = across - left;
        var from = _colors[left];
        var to = _colors[right];

        return Color.FromArgb(
            Mix(from.A, to.A, fraction),
            Mix(from.R, to.R, fraction),
            Mix(from.G, to.G, fraction),
            Mix(from.B, to.B, fraction));
    }

    private static byte Mix(byte from, byte to, double fraction) =>
        (byte)Math.Clamp(Math.Round(from + ((to - from) * fraction)), byte.MinValue, byte.MaxValue);
}
