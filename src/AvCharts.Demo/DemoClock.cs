namespace AvCharts.Demo;

/// <summary>Where the demo reads the time from, so that a page's clock can be pinned by a test.</summary>
/// <remarks>
/// <para>
/// The live feed stamps every reading with the instant it was taken at, and a picture that says when it was taken
/// is a picture the build cannot compare: recorded at half past nine it comes out different at ten. So the one
/// place the demo asks what time it is, is here - and the tests pin it, which is the same thing they do with the
/// font the pictures are drawn in.
/// </para>
/// <para>
/// Pinned by the tests and by nothing else. The demo itself reads the real clock, because a live chart of a
/// made-up time is a picture of nothing.
/// </para>
/// </remarks>
public static class DemoClock
{
    /// <summary>The time now, as the demo understands it.</summary>
    public static Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.Now;
}
