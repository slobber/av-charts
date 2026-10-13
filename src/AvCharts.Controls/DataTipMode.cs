namespace AvCharts.Controls;

/// <summary>How a chart shows the readings a pointer finds.</summary>
public enum DataTipMode
{
    /// <summary>A tip beside the nearest record, which is what a chart does by default.</summary>
    Item,

    /// <summary>A tip that follows the pointer, showing the reading nearest to it.</summary>
    Mouse,

    /// <summary>No tips at all.</summary>
    None,
}
