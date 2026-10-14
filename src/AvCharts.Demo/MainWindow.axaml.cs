using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvCharts.Demo;

/// <summary>
/// The demo on the desktop: the <see cref="Shell"/> in a window of its own.
/// </summary>
/// <remarks>
/// Nothing here but the frame - a title, a size, the place it opens at - because what the demo is belongs to
/// the shell, which the browser head is handed on its own (a browser has no window, and asking for one there
/// throws). Writing the frame down once is what lets both heads show the same gallery.
/// </remarks>
public partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
