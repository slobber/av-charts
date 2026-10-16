using Avalonia;
using Avalonia.Media;

namespace AvCharts.Demo;

/// <summary>
/// The two fonts the gallery is set in: one for the letters it writes in English, and one for the pages it says
/// in Chinese.
/// </summary>
/// <remarks>
/// <para>
/// The Chinese font is not a decoration. A browser build brings no fonts of its own: the desktop falls back to
/// whatever the operating system has, and a page in a browser has nothing behind it, so a gallery that speaks two
/// languages and ships one font is a page of empty boxes as soon as a reader picks the other language. That is
/// what the published gallery was before this font was added - measured by looking at it, not guessed.
/// </para>
/// <para>
/// What is shipped is a <em>subset</em> of Noto Sans SC, narrowed to the characters the gallery actually writes -
/// a few hundred of them, about a hundred kilobytes, where the whole face is eighteen megabytes. The subset is the
/// part that makes the naming honest: the SIL Open Font License reserves the name "Source" for the font it came
/// from, so a modified one is renamed, and the copyright and the licence travel with it in
/// <c>Assets/NotoSansSC-OFL.txt</c>.
/// </para>
/// </remarks>
public static class DemoFonts
{
    /// <summary>The family the gallery's Chinese is set in.</summary>
    /// <remarks>
    /// Spelled as an address rather than as a name, which is how a font that travels inside the application is
    /// asked for: there is no font collection to look a name up in, in a browser or anywhere else.
    /// </remarks>
    public const string ChineseFamily = "avares://AvChartsDemo/Assets/AvChartsChinese-Regular.ttf#AvCharts Chinese";

    /// <summary>The family the gallery's English is set in, which is the one it opens in.</summary>
    /// <remarks>
    /// The name rather than an address, because Inter arrives as a <em>collection</em> of six weights that the
    /// package registers under this name: a page that asked for one file of it would get one weight of it, and every
    /// picture of the gallery would come out in the wrong one.
    /// </remarks>
    public const string LatinFamily = "Inter";

    /// <summary>Dresses the gallery in them, whichever head is starting it.</summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// The Chinese font is a <em>fallback</em> rather than the default, so that the letters the gallery writes in
    /// English keep the face they have always had - the golden pictures are recorded in it, and a page that quietly
    /// changed typeface would change every one of them.
    /// </remarks>
    public static AppBuilder WithGalleryFonts(this AppBuilder builder) =>
        builder
            // The package's own call, so that the collection of Inter's weights is registered. It does not choose
            // Inter as the default - the platform does that, and the pictures of the gallery were drawn with what the
            // platform chose - so nothing here overrules it either.
            .WithInterFont()
            .With(new FontManagerOptions
            {
                // Nothing said about the default family, and the Chinese font only ever reached for a character the
                // default has not got: a fallback that took over the letters would change every picture.
                FontFallbacks = [new FontFallback { FontFamily = new FontFamily(ChineseFamily) }],
            });
}
