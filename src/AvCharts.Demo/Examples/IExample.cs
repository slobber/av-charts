using Avalonia.Controls;

namespace AvCharts.Demo.Examples;

/// <summary>
/// One example of the gallery: a pair of files under <c>Examples/</c> - the markup a reader copies, and the code
/// the same chart is built by hand from.
/// </summary>
/// <remarks>
/// <para>
/// An example is two files with the same name: an <c>.axaml</c> holding the chart as markup, and a <c>.cs</c>
/// holding the readings and the same chart built in code. The page shows all three - the picture, the code and
/// the markup - and the picture is the markup half loaded from the file, so what a reader copies out is what they
/// were looking at.
/// </para>
/// <para>
/// What keeps the two halves from drifting apart is the check beside the gallery, which draws each of them and
/// compares the two pictures pixel by pixel. That is what this interface is for: an example is found by being
/// one, rather than by being listed in a place where it can be forgotten - a pair of files nobody checks is a
/// snippet that stops working quietly.
/// </para>
/// <para>
/// An example is named by where it stands: the type's namespace says which folder it is in and the type's own
/// name says what it is called, so <c>Examples/Candles/HighLowCloseBars.axaml</c> is drawn again by
/// <c>AvCharts.Demo.Examples.Candles.HighLowCloseBars</c>.
/// </para>
/// </remarks>
public interface IExample
{
    /// <summary>The chart the example's markup draws, drawn again in code.</summary>
    /// <returns>The chart.</returns>
    Control Build();
}
