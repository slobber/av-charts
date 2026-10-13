using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvCharts.Themes;

/// <summary>The default theme: where it lives, and how to get hold of it.</summary>
/// <remarks>
/// A host merges <see cref="Source"/> into its resources - in XAML with a <c>ResourceInclude</c>, in code with
/// <see cref="Create"/> - and its charts are dressed with it. The tokens, named in <see cref="ChartTokens"/>,
/// come along; overriding one is a matter of putting a value under the same key.
/// </remarks>
public static class ChartTheme
{
    /// <summary>The resource dictionary holding the palette, the inks and the charts' control theme.</summary>
    public static Uri Source { get; } = new("avares://AvCharts.Controls/Themes/ChartTheme.axaml");

    /// <summary>Loads a fresh copy of the default theme, for a host that builds its resources in code.</summary>
    /// <returns>The dictionary, ready to be merged into a resource dictionary of the host's own.</returns>
    public static ResourceDictionary Create() => (ResourceDictionary)AvaloniaXamlLoader.Load(Source);
}
