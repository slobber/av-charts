using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using AvCharts.Charts;
using AvCharts.Themes;

namespace AvCharts.Controls;

/// <summary>
/// One line of a legend: the colour a series is drawn in, its name, and whether it is being shown.
/// </summary>
/// <remarks>
/// <para>
/// A control of its own rather than something the legend paints, so that a host can reskin it - and so that a
/// legend of any shape at all is a template away. What it writes and what colour it shows are read from the
/// series it stands for; the legend is what keeps them up to date.
/// </para>
/// <para>
/// Clicking it shows or hides the series, which is the one thing a legend does in v1. A legend item is not a
/// button: it has no press state and nothing to focus, because the chart beside it is what has the keyboard.
/// </para>
/// </remarks>
public class LegendItem : TemplatedControl
{
    /// <summary>The series this line is about.</summary>
    public static readonly StyledProperty<Series?> SeriesProperty =
        AvaloniaProperty.Register<LegendItem, Series?>(nameof(Series));

    /// <summary>The name written beside the swatch.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<LegendItem, string?>(nameof(Text));

    /// <summary>The colour of the swatch, which is the colour the series is drawn in.</summary>
    public static readonly StyledProperty<IBrush?> ColourProperty =
        AvaloniaProperty.Register<LegendItem, IBrush?>(nameof(Colour));

    /// <summary>Whether the series is hidden, which is what dims the line.</summary>
    public static readonly StyledProperty<bool> IsOffProperty =
        AvaloniaProperty.Register<LegendItem, bool>(nameof(IsOff));

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ResourceThemes.Apply(this, typeof(LegendItem));
    }

    /// <summary>The series this line is about.</summary>
    public Series? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>The name written beside the swatch.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The colour of the swatch.</summary>
    public IBrush? Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <summary>Whether the series is hidden, which is what dims the line.</summary>
    /// <remarks>
    /// The token behind it is <see cref="ChartTokens.LegendItemDisabled"/>, and it is what the brief asks a
    /// legend to have for a series that is not being shown.
    /// </remarks>
    public bool IsOff
    {
        get => GetValue(IsOffProperty);
        set => SetValue(IsOffProperty, value);
    }

    /// <summary>Shows or hides the series this line is about, which is what clicking it does.</summary>
    public void Toggle()
    {
        if (Series is { } series)
        {
            series.Visible = !series.Visible;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOffProperty)
        {
            // A pseudo class rather than a property the theme has to bind: it is the theme's job to say what
            // being hidden looks like, and this is the one word it needs to select on.
            if (IsOff)
            {
                PseudoClasses.Add(":off");
            }
            else
            {
                PseudoClasses.Remove(":off");
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // Released rather than pressed, so that a reader who changed their mind and dragged away does not end up
        // toggling a series by accident.
        Toggle();
        e.Handled = true;
    }

    /// <summary>Reads the series again: its name, its colour and whether it is being shown.</summary>
    /// <remarks>
    /// Called by the legend, which is the only thing that knows when a series may have changed.
    /// </remarks>
    internal void Refresh()
    {
        if (Series is not { } series)
        {
            return;
        }

        Text = series.DisplayName;
        Colour = series.Color;
        IsOff = !series.Visible;
    }
}
