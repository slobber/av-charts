using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using static AvCharts.Demo.Gallery;
using static AvCharts.Demo.Readings;

namespace AvCharts.Demo.Pages;

// The export page: one chart, the picture written of it, and the knobs that decide what that picture is.
internal static class ExportPage
{
    /// <summary>A chart beside the picture written of it, at a size and a pitch the reader chooses.</summary>
    /// <returns>The page.</returns>
    /// <remarks>
    /// One block rather than an example and a playground, and the chart is part of it: the subject here is the
    /// writing out, so the chart on screen is there to be the thing written while every knob on the page is a knob
    /// on the picture beside it. A frame that only held the picture would be a frame with nothing to compare it
    /// with - and what a reader wants to know about an exported picture is what it has to do with the chart.
    /// </remarks>
    public static DemoPage Create() => Page(
        "Export",
        "A chart written into a picture of its own: the size and the pitch are yours to ask for, and the bytes come out of a stream.",
        Try("Try it: the picture, at the size and the pitch you ask for", ExportPlayground()));

    /// <summary>The chart, the picture of it, and the three knobs that decide what that picture is.</summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// <para>
    /// The knobs are the three questions a reader has about an exported picture. <em>Size</em> is the one that
    /// shows what exporting is: the picture is <em>laid out</em> at the size asked for - so a square one has its
    /// labels where a square chart's labels go - rather than the picture on screen being stretched to fit it.
    /// <em>Pixels a point</em> is the same drawing at a finer pitch, which is what a printed page wants: twice the
    /// pixels and twice the detail rather than twice the blur. <em>Preview</em> is the honest one: shown to fit the
    /// frame, a four thousand pixel picture and a four hundred pixel one look much alike - at the reader's own
    /// pixels they do not.
    /// </para>
    /// <para>
    /// The line under the two halves is the fourth thing worth seeing: the picture becomes a PNG of a countable
    /// number of bytes, and the number moves when the knobs do.
    /// </para>
    /// </remarks>
    private static Control ExportPlayground()
    {
        var chart = Chart(0, 6, 0, 9);

        chart.Series.Add(Line("Readings", [1.0, 4.0, 2.5, 5.5, 3.0, 7.0]));
        chart.Series.Add(Line("Another", [3.0, 2.0, 4.5, 1.5, 6.0, 4.0]));

        // The picture is given the room it is shown in rather than left to want one of its own: an exported
        // picture is four thousand pixels across one moment and four hundred the next, and a control that
        // answered "as wide as my pixels are" would drag the whole frame out to whatever was written last - the
        // frame is as wide as what is in it, and a playground only comes back to its column when its frame is
        // dragged. A fixed room keeps the two halves level whatever is written into it.
        var picture = new Image { Stretch = Stretch.Uniform, Width = 170, Height = 120 };
        var report = new TextBlock { FontSize = 11, Opacity = 0.6, TextWrapping = TextWrapping.Wrap };
        var stage = new Grid
        {
            // The captions, the two halves, and the line about what was written. The line is inside the frame
            // rather than beside it because it is about the picture: a reader comparing the two halves should not
            // have to look away from them to read the number.
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };

        stage.Children.Add(Caption("On screen", 0));
        stage.Children.Add(Caption("The picture", 1));

        Grid.SetRow(chart, 1);
        stage.Children.Add(chart);

        // The picture is clipped to the room it is given, which is what makes "actual pixels" readable: at the
        // reader's own pixels a picture of several thousand is bigger than the frame, and what is shown of it is
        // the part that fits - which is the whole point of the knob.
        var window = new Border { ClipToBounds = true, Child = picture };

        Grid.SetRow(window, 1);
        Grid.SetColumn(window, 1);
        stage.Children.Add(window);

        Grid.SetRow(report, 2);
        Grid.SetColumnSpan(report, 2);
        stage.Children.Add(report);

        var size = new PixelSize(480, 300);
        var scaling = 1;

        void Write()
        {
            picture.Source = chart.RenderImage(size, scaling);

            // The bytes the way a host gets them: the library writes the picture as a PNG into whatever stream it
            // is handed rather than into a path, because a browser has no paths - what a host does with them (a
            // file, a download, an email) is the host's business.
            var written = new MemoryStream();

            chart.SaveImage(written, size, scaling);

            report.Text = Text.Of(
                $"{size.Width} by {size.Height} pixels, {scaling} to a point: {written.Length:N0} bytes of PNG.",
                $"{size.Width} × {size.Height} 像素，一点 {scaling} 个像素：PNG {written.Length:N0} 字节。");
        }

        // The first picture waits for the chart to be laid out, and the knobs write straight away once it has been:
        // a chart is dressed by joining a tree and is given its room by being laid out, so a page built before
        // either has happened would write a picture of a chart nobody had dressed. Written once, not on every
        // layout: the picture is made at the size the knob asks for, so dragging the frame changes nothing about it.
        var written = false;

        chart.SizeChanged += (_, _) =>
        {
            if (written)
            {
                return;
            }

            written = true;
            Write();
        };

        return Playground(
            stage,
            Choice("Size", ["480 by 300", "600 by 600", "900 by 300"], "480 by 300", picked =>
            {
                size = picked switch
                {
                    "600 by 600" => new PixelSize(600, 600),
                    "900 by 300" => new PixelSize(900, 300),
                    _ => new PixelSize(480, 300),
                };

                Write();
            }),
            Choice("Pixels a point", ["1", "2", "3"], "1", picked =>
            {
                scaling = picked switch { "2" => 2, "3" => 3, _ => 1 };
                Write();
            }),
            Choice("Preview", ["fit the box", "actual pixels"], "fit the box", picked =>
            {
                // Which is about the frame and not about the picture: the picture is the same picture either way.
                picture.Stretch = picked == "fit the box" ? Stretch.Uniform : Stretch.None;
            }));

        Control Caption(string words, int column)
        {
            var text = new TextBlock { Text = words, FontSize = 11, Opacity = 0.7 };

            Grid.SetColumn(text, column);
            return text;
        }
    }
}
