using System.Globalization;

namespace AvCharts.Charts;

// The series a markup file can name. Avalonia's markup has no type arguments, so the generic series - which is
// where all the behaviour lives - cannot be written in a page at all; these are the same series over `object`,
// one line each, so that a chart written in markup and a chart written in code are the same chart.
//
// The values are read by path rather than by lambda wherever a generic series takes a selector: see
// `PropertyPath`, and `XPath`/`YPath` and their relatives on the series themselves.

/// <summary>
/// A line of readings, for markup: the readings are any object and the two values are read by path.
/// </summary>
/// <remarks>The measured shape of a <see cref="LineSeries{T}"/>, named where a page can reach it.</remarks>
public class LineSeries : LineSeries<object>;

/// <summary>A line with the area under it filled, for markup.</summary>
/// <remarks>The measured shape of a <see cref="AreaSeries{T}"/>.</remarks>
public class AreaSeries : AreaSeries<object>;

/// <summary>A line that steps across and up, for markup.</summary>
/// <remarks>The measured shape of a <see cref="StepLineSeries{T}"/>.</remarks>
public class StepLineSeries : StepLineSeries<object>;

/// <summary>A stepped line with the area under it filled, for markup.</summary>
/// <remarks>The measured shape of a <see cref="StepAreaSeries{T}"/>.</remarks>
public class StepAreaSeries : StepAreaSeries<object>;

/// <summary>Readings as columns, for markup.</summary>
/// <remarks>The measured shape of a <see cref="ColumnSeries{T}"/>.</remarks>
public class ColumnSeries : ColumnSeries<object>;

/// <summary>Readings as bars lying down, for markup.</summary>
/// <remarks>The measured shape of a <see cref="BarSeries{T}"/>.</remarks>
public class BarSeries : BarSeries<object>;

/// <summary>Readings as marks, for markup.</summary>
/// <remarks>The measured shape of a <see cref="PlotSeries{T}"/>.</remarks>
public class PlotSeries : PlotSeries<object>;

/// <summary>Readings as bubbles sized by a third value, for markup.</summary>
/// <remarks>The measured shape of a <see cref="BubbleSeries{T}"/>.</remarks>
public class BubbleSeries : BubbleSeries<object>;

/// <summary>Prices as candles, for markup.</summary>
/// <remarks>The measured shape of a <see cref="CandlestickSeries{T}"/>.</remarks>
public class CandlestickSeries : CandlestickSeries<object>;

/// <summary>Readings as shares of a circle, for markup.</summary>
/// <remarks>The measured shape of a <see cref="PieSeries{T}"/>.</remarks>
public class PieSeries : PieSeries<object>;

/// <summary>The selectors a path turns into, in the three shapes the series take them in.</summary>
/// <remarks>
/// One place rather than a line in every series: a path is compiled the same way whether it is read as a chart
/// value, as a number, or as words, and the three readings of "nothing there" are the same three the rest of
/// the library uses - a missing value, a NaN, and no text.
/// </remarks>
internal static class Paths
{
    /// <summary>The chart-value selector a path makes; null when there is no path.</summary>
    /// <typeparam name="T">The type of one item in the source.</typeparam>
    /// <param name="path">The path.</param>
    /// <returns>The selector, or null.</returns>
    public static Func<T, ChartValue>? Value<T>(string? path) =>
        path is { Length: > 0 } ? PropertyPath.Read<T>(path) : null;

    /// <summary>The numeric selector a path makes; null when there is no path.</summary>
    /// <typeparam name="T">The type of one item in the source.</typeparam>
    /// <param name="path">The path.</param>
    /// <returns>The selector, or null.</returns>
    public static Func<T, double>? Number<T>(string? path) =>
        Value<T>(path) is { } read
            ? item => read(item) is { IsEmpty: false } value && value.TryGetNumber(out var number)
                ? number
                : double.NaN
            : null;

    /// <summary>The text selector a path makes; null when there is no path.</summary>
    /// <typeparam name="T">The type of one item in the source.</typeparam>
    /// <param name="path">The path.</param>
    /// <returns>The selector, or null.</returns>
    /// <remarks>A reading of any kind has words of its own: a category is its own name, a number is written out.</remarks>
    public static Func<T, string?>? Text<T>(string? path) =>
        Value<T>(path) is { } read
            ? item => read(item) switch
            {
                { IsEmpty: true } => null,
                { Kind: ChartValueKind.Category } value => value.Category,
                var value => value.Number.ToString(CultureInfo.InvariantCulture),
            }
            : null;
}
