using Avalonia.Controls;
using Avalonia.Media;

namespace AvCharts.Controls;

/// <summary>The library's defaults: what a host sets once and every chart then starts from.</summary>
/// <remarks>
/// <para>
/// Two things live here, and both are things a host with a page of charts would otherwise repeat on every one
/// of them: the <see cref="Theme"/> its charts are dressed with, and the <see cref="Typeface"/> the axes write
/// their labels in. Both are also things a chart cannot take from the control tree by itself - a control theme
/// arrives as resources that somebody has to put <em>somewhere</em>, and an axis' labels are drawn by a renderer
/// that has no font above it to inherit.
/// </para>
/// <para>
/// Everything else is deliberately not here. How a series is sampled, which readings it takes, what a chart
/// covers and how it is laid out are the series' and the chart's own business - see §5 of the design brief for
/// why sampling belongs to the series rather than to the application.
/// </para>
/// <para>
/// The theme is read where a token is looked up, so a chart's own scope is read first and this is the last
/// thing consulted: a host that puts a theme on one window still gets that theme in that window. The face is
/// handed to a renderer that was not given one of its own.
/// </para>
/// <para>
/// Both are read as charts are built and dressed, so <see cref="Configure"/> is a startup call: it belongs in a
/// host's <c>Main</c> or <c>App</c>, before the first chart. A chart that is already on screen keeps what it was
/// dressed with until its own resources change - which is the same rule as any other theme, and the reason
/// there is no machinery here to reach into charts the library has already handed out.
/// </para>
/// </remarks>
public sealed class ChartDefaults
{
    /// <summary>The one set of defaults, which <see cref="Current"/> is.</summary>
    /// <remarks>
    /// Private, so that the only way to set the library's defaults is <see cref="Configure"/>: a second
    /// instance that nothing reads would be a trap rather than a feature.
    /// </remarks>
    private ChartDefaults()
    {
    }

    /// <summary>The defaults in force.</summary>
    /// <remarks>
    /// Read, not replaced: <see cref="Configure"/> sets the properties of this one rather than handing out
    /// another, so there is exactly one answer to "what are the defaults".
    /// </remarks>
    public static ChartDefaults Current { get; } = new();

    /// <summary>Sets the library's defaults.</summary>
    /// <param name="configure">What to set them to.</param>
    /// <remarks>
    /// Called once at startup, in the host's own <c>Main</c> or <c>App</c>: one line, and every chart built
    /// afterwards is dressed and lettered the way the host wants without any of them being told.
    /// </remarks>
    /// <example>
    /// <code>
    /// ChartDefaults.Configure(defaults =>
    /// {
    ///     defaults.Theme = ChartTheme.Create();
    ///     defaults.Typeface = new Typeface("Inter");
    /// });
    /// </code>
    /// </example>
    public static void Configure(Action<ChartDefaults> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(Current);
    }

    /// <summary>
    /// The theme every chart falls back to: the tokens of the design brief's §7, in a dictionary of the host's
    /// own making.
    /// </summary>
    /// <remarks>
    /// The last thing a chart consults when it looks a token up, and the first thing it is dressed with when
    /// nothing above it carries one. A host that prefers to hand each window its theme can leave this null and
    /// keep doing that; a host that has many windows can set it once here.
    /// </remarks>
    public ResourceDictionary? Theme { get; set; }

    /// <summary>The face an axis' labels are set in when the axis was not given one of its own.</summary>
    /// <remarks>
    /// Handed down to every axis renderer that still wears the library's own default face, so that a host with
    /// a font of its own does not have to visit each axis of each chart. An axis given a face of its own keeps
    /// it, which is how a single chart - a chart of Chinese, of numbers in another script - is lettered
    /// differently from the rest.
    /// </remarks>
    public Typeface? Typeface { get; set; }
}
