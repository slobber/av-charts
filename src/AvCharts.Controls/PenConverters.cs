using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AvCharts.Controls;

/// <summary>Takes a pen apart into the two things a border is drawn with: its brush and how wide it is.</summary>
/// <remarks>
/// <para>
/// The outline of a tip, of a legend and of a chart is a token, and a token is a <see cref="Pen"/> - a brush and
/// a thickness - because that is what an outline is. A <see cref="Avalonia.Controls.Border"/> in a template wants
/// those two separately, so a control theme reads them out of the pen with these; a host writing a theme of its
/// own for a tip wants them for the same reason.
/// </para>
/// <para>
/// Each of them answers "nothing" for a pen that is null, which is what a control with no outline has. A binding
/// written the other way - walking into the pen, <c>BorderPen.Brush</c> - does not fail quietly: it writes an
/// error for every tip of every chart, and the width it hands a border is a number where a thickness is wanted,
/// so the outline is not drawn at all. That is the bug these two replace, and it is why they exist rather than
/// the templates reaching into the pen themselves.
/// </para>
/// </remarks>
public static class PenConverters
{
    /// <summary>A pen's brush; null for no pen at all, which is a border that is not there.</summary>
    public static IValueConverter Brush { get; } = new FuncValueConverter<Pen?, IBrush?>(pen => pen?.Brush);

    /// <summary>A pen's width, as the thickness a border takes; none at all for no pen.</summary>
    public static IValueConverter BorderThickness { get; } =
        new FuncValueConverter<Pen?, Thickness>(
            pen => pen is { Thickness: > 0 } wide ? new Thickness(wide.Thickness) : default);
}
