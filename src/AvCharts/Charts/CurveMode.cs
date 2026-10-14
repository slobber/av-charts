namespace AvCharts.Charts;

/// <summary>How the points of a line are joined to each other.</summary>
public enum CurveMode
{
    /// <summary>
    /// Straight from one reading to the next, which is what a reading is: the value between two of them was
    /// never measured. The default, and what every series did before there was a choice.
    /// </summary>
    Linear,

    /// <summary>
    /// A curve through every reading, rounded off between them. It passes through each reading and stays
    /// between neighbouring ones - it never invents a peak the data does not have.
    /// </summary>
    Smooth,
}
