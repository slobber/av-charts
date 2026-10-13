using System.Globalization;

namespace AvCharts.Charts;

/// <summary>Identifies what a <see cref="ChartValue"/> holds.</summary>
public enum ChartValueKind
{
    /// <summary>
    /// The data item exists but this dimension has no value: a gap. A null reference and a NaN both land here.
    /// </summary>
    Empty,

    /// <summary>A number, carried in <see cref="ChartValue.Number"/>.</summary>
    Number,

    /// <summary>A point in time, carried in <see cref="ChartValue.DateTime"/>; the axis decides how to measure it.</summary>
    DateTime,

    /// <summary>A category, carried in <see cref="ChartValue.Category"/>, as used by a category axis.</summary>
    Category,
}

/// <summary>
/// One dimension of one data item: the unit that flows from a series' selector into an axis and back out of a
/// hit test.
/// </summary>
/// <remarks>
/// <para>
/// A small discriminated union rather than an <see cref="object"/>, so a selector of type
/// <c>Func&lt;T, ChartValue&gt;</c> boxes nothing on the way in and needs no cast on the way out - which is the
/// whole reason the data pipeline is generic.
/// </para>
/// <para>
/// <see cref="double.NaN"/> converts to <see cref="ChartValueKind.Empty"/>: C# has no nullable <see cref="double"/>,
/// and NaN is what a missing measurement usually looks like by the time it reaches a chart.
/// </para>
/// <para>
/// A <see langword="null"/> or empty string also converts to <see cref="ChartValueKind.Empty"/>, because an
/// unlabelled category cannot be told apart from a missing one - and the empty cell of a CSV is the common case.
/// </para>
/// <para>
/// Converting between kinds is the axis' job, not this type's: a linear axis has nothing to say about a
/// <see cref="ChartValueKind.DateTime"/> value, and says so by treating the point as missing on that axis.
/// </para>
/// </remarks>
public readonly struct ChartValue : IEquatable<ChartValue>
{
    private ChartValue(ChartValueKind kind, double number, DateTime dateTime, string? category)
    {
        Kind = kind;
        Number = number;
        DateTime = dateTime;
        Category = category;
    }

    /// <summary>An empty value: the item is there, the dimension is not.</summary>
    public static ChartValue Empty => default;

    /// <summary>What this value holds; empty when the dimension has no value.</summary>
    public ChartValueKind Kind { get; }

    /// <summary>True when the dimension has no value. Every consumer of a <see cref="ChartValue"/> checks this first.</summary>
    public bool IsEmpty => Kind == ChartValueKind.Empty;

    /// <summary>The number, when <see cref="Kind"/> is <see cref="ChartValueKind.Number"/>; otherwise 0.</summary>
    public double Number { get; }

    /// <summary>The instant, when <see cref="Kind"/> is <see cref="ChartValueKind.DateTime"/>; otherwise default.</summary>
    public DateTime DateTime { get; }

    /// <summary>The category, when <see cref="Kind"/> is <see cref="ChartValueKind.Category"/>; otherwise null.</summary>
    public string? Category { get; }

    /// <summary>Builds a numeric value; <see cref="double.NaN"/> becomes <see cref="Empty"/>.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The value.</returns>
    public static ChartValue FromNumber(double value) =>
        double.IsNaN(value) ? Empty : new ChartValue(ChartValueKind.Number, value, default, null);

    /// <summary>Builds an instant.</summary>
    /// <param name="value">The instant.</param>
    /// <returns>The value.</returns>
    public static ChartValue FromDateTime(DateTime value) =>
        new(ChartValueKind.DateTime, 0, value, null);

    /// <summary>Builds a category; a null or empty label becomes <see cref="Empty"/>.</summary>
    /// <param name="value">The category label.</param>
    /// <returns>The value.</returns>
    public static ChartValue FromCategory(string? value) =>
        string.IsNullOrEmpty(value) ? Empty : new ChartValue(ChartValueKind.Category, 0, default, value);

    /// <summary>Reads the number, if this value is one.</summary>
    /// <param name="value">The number, when this returns <see langword="true"/>; otherwise 0.</param>
    /// <returns><see langword="true"/> when this value holds a number.</returns>
    public bool TryGetNumber(out double value)
    {
        value = Number;
        return Kind == ChartValueKind.Number;
    }

    /// <summary>Converts a number to a value.</summary>
    /// <param name="value">The number.</param>
    public static implicit operator ChartValue(double value) => FromNumber(value);

    /// <summary>Converts a nullable number to a value; null becomes <see cref="Empty"/>.</summary>
    /// <param name="value">The number.</param>
    public static implicit operator ChartValue(double? value) =>
        value.HasValue ? FromNumber(value.Value) : Empty;

    /// <summary>Converts an instant to a value.</summary>
    /// <param name="value">The instant.</param>
    public static implicit operator ChartValue(DateTime value) => FromDateTime(value);

    /// <summary>Converts a nullable instant to a value; null becomes <see cref="Empty"/>.</summary>
    /// <param name="value">The instant.</param>
    public static implicit operator ChartValue(DateTime? value) =>
        value.HasValue ? FromDateTime(value.Value) : Empty;

    /// <summary>Converts a category label to a value; null or empty becomes <see cref="Empty"/>.</summary>
    /// <param name="value">The category label.</param>
    public static implicit operator ChartValue(string? value) => FromCategory(value);

    /// <inheritdoc />
    public bool Equals(ChartValue other) =>
        Kind == other.Kind
        && Number.Equals(other.Number)
        && DateTime.Equals(other.DateTime)
        && string.Equals(Category, other.Category, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ChartValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Number, DateTime, Category);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they hold the same kind and payload.</returns>
    public static bool operator ==(ChartValue left, ChartValue right) => left.Equals(right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(ChartValue left, ChartValue right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        ChartValueKind.Number => Number.ToString(CultureInfo.InvariantCulture),
        ChartValueKind.DateTime => DateTime.ToString("o", CultureInfo.InvariantCulture),
        ChartValueKind.Category => Category ?? string.Empty,
        _ => string.Empty,
    };
}
