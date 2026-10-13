namespace AvCharts.Controls;

/// <summary>How many records a reader may pick out at once.</summary>
public enum SelectionMode
{
    /// <summary>Nothing is selectable; what a chart does until it is asked for selection.</summary>
    None,

    /// <summary>One record at a time, which is what clicking a new one replaces.</summary>
    Single,

    /// <summary>As many as are picked, with Control and Shift.</summary>
    Multiple,
}
