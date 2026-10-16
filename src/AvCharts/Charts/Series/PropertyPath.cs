using System.Collections;
using System.Globalization;
using System.Reflection;


using AvCharts.Data;



namespace AvCharts.Series;

/// <summary>
/// Reads a value out of a record by the name of the way in: <c>Value</c>, or <c>Region.Name</c> for a step
/// inside a step.
/// </summary>
/// <remarks>
/// <para>
/// What a markup file needs and a lambda cannot be: a selector written as a string. A page sets
/// <c>YPath="Value"</c> instead of <c>YSelector="{Binding ...}"</c>, which is the difference between a chart a
/// host can draw in markup and one they can only draw in code.
/// </para>
/// <para>
/// A path is compiled once, when it is set, into the same <c>Func</c> a host writing code would have written by
/// hand - so everything above the selector is identical for the two, which is what lets a chart written in
/// markup and one written in code be the same chart rather than two that look alike. Reflection is the price of
/// writing a name rather than a method, and it is paid once per read rather than once per frame: the series
/// keeps the values it reads, the way it does for any other selector.
/// </para>
/// <para>
/// A step reads a property, a field, or an entry of a dictionary. A path nobody has, or a step into something
/// that is missing, is a missing value rather than a fault: the same rule the rest of the library follows for a
/// reading that is not there.
/// </para>
/// </remarks>
internal static class PropertyPath
{
    private const BindingFlags Anything = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

    /// <summary>Makes a selector that reads one value out of a record by path.</summary>
    /// <typeparam name="T">The type of one item in the source.</typeparam>
    /// <param name="path">The path: names joined by full stops.</param>
    /// <returns>The selector.</returns>
    public static Func<T, ChartValue> Read<T>(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var steps = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return item => Walk(item, steps);
    }

    /// <summary>Walks a path from a record, one step at a time.</summary>
    /// <param name="item">The record.</param>
    /// <param name="steps">The names to walk through.</param>
    /// <returns>The value the path reaches, or an empty value when it reaches nothing.</returns>
    private static ChartValue Walk(object? item, string[] steps)
    {
        foreach (var step in steps)
        {
            item = Step(item, step);
        }

        return Value(item);
    }

    /// <summary>One step of a path: a property, a field, or a dictionary's entry.</summary>
    /// <param name="item">Where the step starts.</param>
    /// <param name="step">What to read.</param>
    /// <returns>Where the step ends, or null.</returns>
    private static object? Step(object? item, string step)
    {
        switch (item)
        {
            case null:
                return null;

            case IDictionary dictionary:
                return dictionary.Contains(step) ? dictionary[step] : null;
        }

        var type = item.GetType();

        if (type.GetProperty(step, Anything) is { CanRead: true } property)
        {
            return property.GetValue(item);
        }

        return type.GetField(step, Anything) is { } field ? field.GetValue(item) : null;
    }

    /// <summary>What a value that a path reached is worth to a chart.</summary>
    /// <param name="item">The value.</param>
    /// <returns>The chart value; empty when this is not something a chart can place.</returns>
    private static ChartValue Value(object? item) => item switch
    {
        null => ChartValue.Empty,
        ChartValue value => value,
        double number => number,
        float number => number,
        decimal number => (double)number,
        int number => number,
        long number => number,
        short number => number,
        byte number => number,
        sbyte number => number,
        uint number => number,
        ulong number => number,
        ushort number => number,
        string text => text,
        DateTimeOffset instant => instant,
        DateTime moment => moment,
        IFormattable other when double.TryParse(other.ToString(null, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            => parsed,
        _ => ChartValue.Empty,
    };
}
