using Avalonia;
using Avalonia.Controls;

namespace AvCharts.Demo;

/// <summary>One example: a name, a line about it, and the live thing itself.</summary>
/// <remarks>
/// The house's demo blocks also show the markup and the code behind an example, read back out of the page's own
/// file. There is nothing to read back here: these examples are built in code rather than written as markup, so a
/// block is the example without its source. A page of markup per example, with the source under it, is worth
/// doing when the gallery is the documentation - and it is recorded as the thing to do then.
/// </remarks>
public class DemoBlock : ContentControl
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DemoBlock, string?>(nameof(Title));

    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<DemoBlock, string?>(nameof(Description));

    /// <summary>The name of the example, shown above it.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The line under the title, when the example needs one.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
