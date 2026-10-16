





namespace AvCharts.Series;

/// <summary>Where a pie's wedges are labelled.</summary>
/// <remarks>
/// Flex's <c>labelPosition</c>, with the two a reader meets kept apart from the two that only differ in how the
/// label is reached: a label is either in the wedge or beside it, and a label beside a wedge always has the line
/// that says which wedge it belongs to.
/// </remarks>
public enum PieLabelPosition
{
    /// <summary>No labels at all, which is what a pie has unless a host asks for them.</summary>
    None,

    /// <summary>In the wedge, when the wedge has room for the text.</summary>
    Inside,

    /// <summary>Beside the circle, with a line reaching back to the wedge.</summary>
    Outside,
}
