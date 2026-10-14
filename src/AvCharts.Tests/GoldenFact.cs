using Xunit;

namespace AvCharts.Tests;

/// <summary>A test that draws a picture and compares it with the one checked in beside the tests.</summary>
/// <remarks>
/// <para>
/// Only where the pictures were recorded. A golden image is the rasterizer's output, and the same drawing
/// through the same font does not come out the same everywhere: measured on a Linux runner, a page whose
/// layout and geometry match to the pixel differs in a quarter of its pixels, because the bold weight of the
/// embedded Inter family resolves to another face there - the headings come out wider, the axis labels and the
/// curves are identical. Sixteen of these tests fail there for that reason.
/// </para>
/// <para>
/// Skipped rather than re-recorded, so that there is one set of pictures and it stays the acceptance: a second
/// set recorded on Linux would be a different picture of the same thing, and neither would be the truth about
/// the other. The workflow runs these on a Windows runner instead, which is where they can mean something.
/// </para>
/// </remarks>
public sealed class GoldenFactAttribute : FactAttribute
{
    public GoldenFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = Why;
        }
    }

    /// <summary>What the runner is told, in one sentence.</summary>
    internal const string Why =
        "golden images are recorded on Windows: the bold weight of the embedded Inter family resolves to " +
        "another face elsewhere, so the same drawing comes out different (measured: a quarter of the pixels " +
        "of one page). The pictures are compared on a Windows runner in CI.";
}

/// <summary>The same, for a picture drawn once per set of data.</summary>
public sealed class GoldenTheoryAttribute : TheoryAttribute
{
    public GoldenTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = GoldenFactAttribute.Why;
        }
    }
}
