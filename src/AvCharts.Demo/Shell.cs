using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;

namespace AvCharts.Demo;

/// <summary>The gallery as one control, for a host that has a view rather than a window - a browser.</summary>
internal static class Shell
{
    /// <summary>Builds the shell: the same catalogue, the same pages and the same styles as the window's.</summary>
    /// <returns>The shell.</returns>
    /// <remarks>
    /// The desktop application shows a <see cref="Window"/>; the browser has one view and no title bar, so what
    /// is needed there is the window's content on its own. It is taken out of the window rather than written a
    /// second time, so that a page cannot come out one way in a browser and another way on the desktop - and so
    /// that the tests which walk the shell walk the one a browser is given.
    /// </remarks>
    public static Control Create()
    {
        var window = new MainWindow();
        var content = (Control)window.Content!;

        window.Content = null;

        // The demo's own look is worn by the window; a control carries its styles itself.
        content.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml"),
        });

        return content;
    }
}
