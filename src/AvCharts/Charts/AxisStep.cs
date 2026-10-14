namespace AvCharts.Charts;

/// <summary>
/// How an axis spaces its grid lines: how far apart they are, and how to walk from one to the next.
/// </summary>
/// <remarks>
/// <para>
/// Most axes step evenly, and <see cref="EvenStep"/> is that case. A time axis cannot: a month is not a fixed
/// number of milliseconds, so a grid that lands on the first of every month is a different distance apart each
/// time. Making the step an object rather than a number is what lets the grid be walked calendar-wise without
/// the numeric base class knowing anything about calendars, and it is where an axis with its own idea of a
/// readable gap plugs in - see <see cref="AxisBase.ResolveStep"/>.
/// </para>
/// <para>
/// The step travels with the layout it produced, because it also decides how much of a value a label has to
/// say: a monthly axis names months, and only the step knows the lines are months apart.
/// <see cref="IAxis.FormatValue(double, AxisStep)"/> is where that is asked.
/// </para>
/// </remarks>
public abstract class AxisStep
{
    /// <summary>A step that draws nothing: what an axis with no data, or no room, has to grid.</summary>
    public static AxisStep None { get; } = new EvenStep(0);

    /// <summary>
    /// The distance between lines, in unit space; the average one when they are not evenly spaced. Zero when
    /// the step draws nothing.
    /// </summary>
    public abstract double Length { get; }

    /// <summary>The greatest grid position at or below a value.</summary>
    /// <param name="value">A position in unit space.</param>
    /// <returns>The position.</returns>
    public abstract double Floor(double value);

    /// <summary>The grid position just above a value.</summary>
    /// <param name="value">A position in unit space.</param>
    /// <returns>
    /// The next position, or a value that is not greater than the one given when the step cannot move on -
    /// which is how a caller walking the grid knows to stop.
    /// </returns>
    public abstract double Advance(double value);

    /// <summary>The lowest grid position at or above a value.</summary>
    /// <param name="value">A position in unit space.</param>
    /// <returns>The position.</returns>
    public double Ceiling(double value)
    {
        var floor = Floor(value);

        return floor >= value ? floor : Advance(floor);
    }
}

/// <summary>A step whose lines are all the same distance apart.</summary>
public sealed class EvenStep : AxisStep
{
    /// <summary>Creates a step.</summary>
    /// <param name="length">
    /// The distance between lines, in unit space. A length of zero or less draws nothing, which is how
    /// <see cref="AxisStep.None"/> is built.
    /// </param>
    public EvenStep(double length) => Length = length;

    /// <inheritdoc />
    public override double Length { get; }

    /// <inheritdoc />
    public override double Floor(double value) => Length > 0 ? Math.Floor(value / Length) * Length : value;

    /// <summary>The next line up from a line: the next whole multiple of the step.</summary>
    /// <param name="value">The line to move on from.</param>
    /// <returns>The next line.</returns>
    /// <remarks>
    /// The next whole multiple of the step, rather than a running total: adding a step over and over drifts, and a
    /// line that is meant to be read as zero comes out as -1.110223022E-16 - which a label then says out loud.
    /// Counting multiples keeps the round numbers exact, because zero of any step is zero.
    /// </remarks>
    public override double Advance(double value) =>
        Length > 0 ? (Math.Round(value / Length) + 1) * Length : double.NegativeInfinity;
}
