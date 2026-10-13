using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using AvCharts.Charts;
using AvCharts.Themes;

namespace AvCharts.Controls;

/// <summary>
/// The small box a chart shows beside a record: the reading, written out.
/// </summary>
/// <remarks>
/// <para>
/// A control rather than something a layer paints, because a tip is not part of the chart's picture: it is a
/// piece of interface that a host reskins and retemplates like any other. That also makes it the one thing on a
/// chart with text that a host can get at - the design brief puts it and the legend on that side of the line.
/// </para>
/// <para>
/// A chart makes these itself, one per reading it is showing, and pools them: see the data tip properties on
/// <see cref="ChartBase"/>. What it is given is the hit it stands for and the line to write; the box, the
/// border and the shadow come from the theme, which is looked up by type the way the chart's own is.
/// </para>
/// </remarks>
public class DataTip : TemplatedControl
{
    /// <summary>The pen around the box.</summary>
    /// <remarks>
    /// A pen rather than a brush and a thickness, because that is what the token is: the theme hands over
    /// <c>DataTip.Border</c> as it stands, and the default template reads its brush and its thickness out of it.
    /// </remarks>
    public static readonly StyledProperty<Pen?> BorderPenProperty =
        AvaloniaProperty.Register<DataTip, Pen?>(nameof(BorderPen));

    /// <summary>The hit this tip stands for, which is what a host's own template reads.</summary>
    public static readonly StyledProperty<HitData?> DataProperty =
        AvaloniaProperty.Register<DataTip, HitData?>(nameof(Data));

    /// <summary>The line the tip writes.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DataTip, string?>(nameof(Text));

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ResourceThemes.Apply(this, typeof(DataTip));
    }

    /// <summary>The pen around the box.</summary>
    /// <remarks>The token behind it is <see cref="ChartTokens.DataTipBorder"/>.</remarks>
    public Pen? BorderPen
    {
        get => GetValue(BorderPenProperty);
        set => SetValue(BorderPenProperty, value);
    }

    /// <summary>The hit this tip stands for, or null when it is standing for nothing.</summary>
    public HitData? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>The line the tip writes.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
