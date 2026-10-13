using Avalonia;
using Avalonia.Controls;

namespace AvCharts.Demo;

/// <summary>
/// A page of the demo: what it is called, one line about what it shows, and the examples under it.
/// </summary>
/// <remarks>
/// The look comes from the demo's own styles rather than from the library: a page is a heading over its
/// examples, which is the demo's business and not something a charting library should have an opinion about.
/// It is the same shape as the rest of the house's demos, so a reader moving between them sees the same page.
/// </remarks>
public class DemoPage : ContentControl
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DemoPage, string?>(nameof(Title));

    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<DemoPage, string?>(nameof(Description));

    /// <summary>The page's name.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The line under the name: what the page is about.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
