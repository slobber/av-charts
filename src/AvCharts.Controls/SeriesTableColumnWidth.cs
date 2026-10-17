namespace AvCharts.Controls;

/// <summary>The width of one column, named by what the column shows.</summary>
/// <remarks>
/// A series column is keyed by its display name, a record column by its record index, and the leading
/// names column of a <see cref="SeriesTableOrientation.SeriesAsRows"/> table as <c>series-names</c>.
/// Extra display columns are keyed by their position after the data columns.
/// </remarks>
public sealed class SeriesTableColumnWidth
{
    /// <summary>Creates a column width.</summary>
    /// <param name="key">The stable name a host can keep with its saved width.</param>
    /// <param name="width">The width in device-independent pixels.</param>
    public SeriesTableColumnWidth(string key, double width)
    {
        Key = key;
        Width = width;
    }

    /// <summary>The stable name a host can keep with its saved width.</summary>
    public string Key { get; }

    /// <summary>The width in device-independent pixels.</summary>
    public double Width { get; }
}
