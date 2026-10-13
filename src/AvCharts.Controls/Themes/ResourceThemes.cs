using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace AvCharts.Themes;

/// <summary>
/// Hands a control in this library its own control theme, the way these themes are delivered: as resources.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia gives a control its theme from a style host. A colour theme, though, arrives as resource
/// dictionaries - that is how a host overrides a token - so a control here looks for its own theme in the
/// resources it can see, from its concrete type up to the family it belongs to, and takes the first one it
/// finds. That way one <c>ResourceInclude</c> in a host is enough, and Avalonia's own styling still wins: a
/// theme that was set some other way is left alone.
/// </para>
/// <para>
/// It is a static helper rather than a base class because the two things that wear a theme - the chart and the
/// data tip - are both controls with a family of their own, and the one thing they have in common is this.
/// </para>
/// </remarks>
internal static class ResourceThemes
{
    /// <summary>Gives an element its own control theme, if the resources it can see carry one.</summary>
    /// <param name="element">The element to dress.</param>
    /// <param name="family">The base type of the family to look through, from the element's own type to it.</param>
    public static void Apply(StyledElement element, Type family)
    {
        if (element.Theme is not null)
        {
            return;
        }

        for (var type = element.GetType(); type is not null && family.IsAssignableFrom(type); type = type.BaseType)
        {
            if (Find(element, type) is ControlTheme theme)
            {
                element.Theme = theme;
                return;
            }
        }
    }

    /// <summary>Looks a resource up in the resources an element can see, in the variant it is showing.</summary>
    /// <param name="element">The element to look from.</param>
    /// <param name="key">The resource key.</param>
    /// <returns>The value, or null when nothing in scope carries that key.</returns>
    public static object? Find(StyledElement element, object key)
    {
        var variant = element.ActualThemeVariant;

        // Everything the element is inside of, and the application at the end of it: the application is not part
        // of the logical tree, but it is where a theme usually arrives.
        for (ILogical? node = element; node is not null; node = node.LogicalParent)
        {
            if (node is StyledElement scope && scope.Resources is { } resources
                && resources.TryGetResource(key, variant, out var value))
            {
                return value;
            }
        }

        return Application.Current is { } application
            && application.Resources is { } applicationResources
            && applicationResources.TryGetResource(key, variant, out var fromApplication)
                ? fromApplication
                : null;
    }
}
