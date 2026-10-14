using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvCharts.Charts;
using AvCharts.Controls;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

[Collection(AvaloniaCollection.Name)]
public class ChartControlTests
{
    private static CartesianChart Chart(bool showLabels = true, double width = 200, double height = 100)
    {
        var chart = new CartesianChart
        {
            HorizontalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 10, Interval = 5 },
        };

        chart.HorizontalRenderer.ShowLabels = showLabels;
        chart.VerticalRenderer.ShowLabels = showLabels;

        chart.Measure(new Size(width, height));
        chart.Arrange(new Rect(0, 0, width, height));

        return chart;
    }

    /// <summary>Readings every ten milliseconds, up to a moment of the recording.</summary>
    /// <param name="until">The last moment, in milliseconds.</param>
    /// <returns>The readings.</returns>
    private static List<double> Readings(double until)
    {
        var readings = new List<double>();

        for (var at = 0.0; at <= until; at += 10)
        {
            readings.Add(at);
        }

        return readings;
    }

    /// <summary>How many pixels differ between two frames, inside the plot and under it.</summary>
    /// <param name="before">The frame taken first.</param>
    /// <param name="after">The frame taken after.</param>
    /// <param name="plot">The rectangle the readings are drawn in.</param>
    /// <returns>The two counts.</returns>
    /// <remarks>
    /// Apart, because they answer different questions: inside the plot is the readings - a line that moved - and
    /// under it is the axis, where nothing but the labels and their grid is drawn. A chart that painted its line
    /// again and not its labels shows up as the first count and not the second.
    /// </remarks>
    private static (int InPlot, int Under) Differences(WriteableBitmap before, WriteableBitmap after, Rect plot)
    {
        var was = Pixels(before);
        var isNow = Pixels(after);
        var stride = before.PixelSize.Width * 4;
        var inPlot = 0;
        var under = 0;

        for (var index = 0; index < was.Length; index += 4)
        {
            if (was[index] == isNow[index] && was[index + 1] == isNow[index + 1] && was[index + 2] == isNow[index + 2])
            {
                continue;
            }

            if (index / stride < plot.Bottom)
            {
                inPlot++;
            }
            else
            {
                under++;
            }
        }

        return (inPlot, under);
    }

    /// <summary>Lets the application's own loop run: what was asked for is laid out, and a frame is drawn.</summary>
    /// <remarks>
    /// The dispatcher is pumped by hand because a test has no loop of its own, and the render timer is ticked the
    /// same way - the platform's own way of saying "draw a frame now". Both are what the compositor needs, and the
    /// compositor is what decides whether a layer is painted again or reused.
    /// </remarks>
    private static void Paint()
    {
        var until = System.Diagnostics.Stopwatch.StartNew();

        while (until.ElapsedMilliseconds < 200)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(10);
        }
    }

    /// <summary>The pixels of a frame, as one flat array of four bytes each.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Pixels(WriteableBitmap frame)
    {
        var size = frame.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);

        try
        {
            frame.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        return buffer;
    }

    [Fact]
    public void The_plot_leaves_room_for_the_axis_labels()
    {
        var chart = Chart();

        Assert.True(chart.PlotBounds.Left > 0, "the vertical axis' labels need room on the left");
        Assert.True(chart.PlotBounds.Bottom < 100, "the horizontal axis' labels need room at the bottom");
        Assert.True(chart.PlotBounds.Width > 0);
        Assert.True(chart.PlotBounds.Height > 0);
    }

    [Fact]
    public void An_axis_without_labels_only_leaves_room_for_its_marks()
    {
        var chart = Chart(showLabels: false);

        Assert.Equal(chart.VerticalRenderer.TickLength, chart.PlotBounds.Left, 6);
        Assert.Equal(100 - chart.HorizontalRenderer.TickLength, chart.PlotBounds.Bottom, 6);
    }

    [Fact]
    public void A_chart_with_no_data_still_lays_out()
    {
        var chart = new CartesianChart();
        chart.Measure(new Size(200, 100));
        chart.Arrange(new Rect(0, 0, 200, 100));

        Assert.True(chart.PlotBounds.Width > 0);
        Assert.True(chart.PlotBounds.Height > 0);
    }

    [Fact]
    public void Arranging_at_another_size_moves_the_plot()
    {
        var chart = Chart();
        var first = chart.PlotBounds;

        chart.Measure(new Size(400, 300));
        chart.Arrange(new Rect(0, 0, 400, 300));

        Assert.NotEqual(first, chart.PlotBounds);
        Assert.True(chart.PlotBounds.Width > first.Width);
    }

    [Fact]
    public void A_chart_that_stacks_its_columns_lays_itself_out_once_a_frame()
    {
        // Stacking happens in the middle of a layout pass and ends with the series saying its geometry is stale -
        // which used to ask the chart for another pass from inside the one that was running. A layout pass
        // answers that by running itself again, and again, until it gives up: measured, eleven of them a frame,
        // and two hundred milliseconds in a browser on a chart that had not been asked for anything else. The
        // same door is the one a pushed batch comes through, which is why a live chart used to lay itself out
        // again on every frame a reading arrived in.
        var chart = new CountingChart
        {
            HorizontalAxis = new CategoryAxis(),
            VerticalAxis = new LinearAxis { Minimum = 0, Maximum = 100 },
            SeriesLayout = SeriesLayout.Stacked,
        };

        chart.Series.Add(new ColumnSeries<double>
        {
            ItemsSource = [10.0, 20.0, 30.0],
            YSelector = value => value,
        });

        var window = new Window
        {
            Width = 400,
            Height = 200,
            Resources = ChartTheme.Create(),
            Content = chart,
        };

        window.Show();
        Paint();

        // One pass for the zoom, and none for the stacking that pass did.
        chart.Arrangements = 0;
        chart.ZoomBy(1.2, chart.PlotBounds.Center);
        Paint();

        Assert.Equal(1, chart.Arrangements);
        window.Close();
    }

    /// <summary>A chart that counts the layout passes it is put through, which is what a layout loop shows up as.</summary>
    private sealed class CountingChart : CartesianChart
    {
        /// <summary>How many times the chart has been laid out.</summary>
        public int Arrangements { get; set; }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            Arrangements++;
            return base.ArrangeOverride(finalSize);
        }
    }

    [Fact]
    public void An_axis_is_painted_again_when_a_series_goes_out_of_sight()
    {
        // The third way a window moves by itself, and the one nothing else covers: a series is hidden, so it stops
        // asking the axis for room - and no reading of any series changed, which is the only thing that would
        // normally ask for the axes to be painted again.
        var shown = new LineSeries<double>
        {
            ItemsSource = Readings(5_000),
            XSelector = value => value,
            YSelector = value => Math.Sin(value / 500),
        };

        var hidden = new LineSeries<double>
        {
            ItemsSource = Readings(20_000),
            XSelector = value => value,
            YSelector = value => Math.Cos(value / 500),
        };

        var chart = new CartesianChart
        {
            HorizontalAxis = new TimeSpanAxis(),
            VerticalAxis = new LinearAxis { Minimum = -1.5, Maximum = 1.5 },
        };

        chart.Series.Add(shown);
        chart.Series.Add(hidden);

        var window = new Window
        {
            Width = 640,
            Height = 400,
            Resources = ChartTheme.Create(),
            Content = chart,
        };

        window.Show();
        Paint();

        var before = window.CaptureRenderedFrame();

        Assert.NotNull(before);
        Assert.Equal(20_000, chart.Transform.GetLayout(ChartDimension.X).UnitRange.Maximum);

        // The wide one goes: the window is the narrow one's now, and the labels have to say so.
        hidden.Visible = false;
        Paint();

        var after = window.CaptureRenderedFrame();

        Assert.NotNull(after);
        Assert.Equal(5_000, chart.Transform.GetLayout(ChartDimension.X).UnitRange.Maximum);

        var (_, labels) = Differences(before, after, chart.PlotBounds);

        Assert.True(labels > 100, $"the axis was not painted again: {labels} pixels differ under the plot");

        window.Close();
    }

    [Fact]
    public void An_axis_is_painted_again_when_a_series_moves_the_window()
    {
        // The other way a window moves by itself: a series arrives whose readings spread further than the ones
        // already there. No reading changed, so nothing asks for a repaint on the series' behalf - but the window
        // the axis is labelled for has changed, and the labels have to say so.
        var series = new LineSeries<double>
        {
            ItemsSource = Readings(5_000),
            XSelector = value => value,
            YSelector = value => Math.Sin(value / 500),
        };

        var chart = new CartesianChart
        {
            HorizontalAxis = new TimeSpanAxis(),
            VerticalAxis = new LinearAxis { Minimum = -1.5, Maximum = 1.5 },
        };

        chart.Series.Add(series);

        var window = new Window
        {
            Width = 640,
            Height = 400,
            Resources = ChartTheme.Create(),
            Content = chart,
        };

        window.Show();
        Paint();

        var before = window.CaptureRenderedFrame();

        Assert.NotNull(before);
        Assert.Equal(5_000, chart.Transform.GetLayout(ChartDimension.X).UnitRange.Maximum);

        // Twice as far: the labels go from one a second to one every two seconds, in the same places.
        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = Readings(10_000),
            XSelector = value => value,
            YSelector = value => Math.Sin(value / 500),
        });

        Paint();

        var after = window.CaptureRenderedFrame();

        Assert.NotNull(after);
        Assert.Equal(10_000, chart.Transform.GetLayout(ChartDimension.X).UnitRange.Maximum);

        var (_, labels) = Differences(before, after, chart.PlotBounds);


        Assert.True(labels > 100, $"the axis was not painted again: {labels} pixels differ under the plot");

        window.Close();
    }

    [Fact]
    public void An_axis_is_painted_again_when_the_readings_move_the_window()
    {
        // What an axis covers, and what its labels say, are read off the readings in a layout pass; and a layer is
        // told where the plot is rather than what it shows. So a chart whose readings changed under a plot that
        // did not could paint its new line against the old grid and go on doing it - a live chart whose clock
        // stands still however fast its line moves, which is what the streaming page looked like.
        //
        // What is compared here is the frame the compositor produced, twice, and that is the only way a stale
        // layer can be caught: drawing a layer by hand always draws it afresh, so a picture taken that way would
        // show the labels this test is about whether the chart asks for them or not.
        var readings = new List<double>();

        for (var at = 0.0; at <= 5_000; at += 10)
        {
            readings.Add(at);
        }

        var series = new LineSeries<double>
        {
            ItemsSource = readings,
            XSelector = value => value,
            YSelector = value => Math.Sin(value / 500),
        };

        var chart = new CartesianChart
        {
            HorizontalAxis = new TimeSpanAxis(),
            VerticalAxis = new LinearAxis { Minimum = -1.5, Maximum = 1.5 },
        };

        chart.Series.Add(series);

        var window = new Window
        {
            Width = 640,
            Height = 400,
            Resources = ChartTheme.Create(),
            Content = chart,
        };

        window.Show();
        Paint();

        var before = window.CaptureRenderedFrame();

        Assert.NotNull(before);

        // The readings move two seconds on: the window slides, and the plot it slides over does not move at all.
        for (var index = 0; index < readings.Count; index++)
        {
            readings[index] += 2_000;
        }

        series.InvalidateData();
        Paint();

        var after = window.CaptureRenderedFrame();

        Assert.NotNull(after);

        var (line, labels) = Differences(before, after, chart.PlotBounds);


        // The readings are drawn again - the line moved - and the labels under the plot moved with them: the
        // axis says 00:00:03 where it said 00:00:01, and a chart that never asked for the repaint would still be
        // showing the window it opened with.
        Assert.True(line > 1_000, $"the readings were not drawn again either: {line} pixels differ in the plot");
        Assert.True(labels > 100, $"the axis was not painted again: {labels} pixels differ under the plot");

        window.Close();
    }

    [Fact]
    public void Adding_a_series_wires_it_to_the_transform_and_removing_it_lets_it_go()
    {
        var chart = Chart();
        var series = new LineSeries<double>();

        chart.Series.Add(series);

        Assert.Same(chart.Transform, series.Transform);
        Assert.Contains(series, chart.Transform.Elements);
        Assert.Contains(series, chart.SeriesLayer.Series);

        chart.Series.Remove(series);

        Assert.Null(series.Transform);
        Assert.DoesNotContain(series, chart.Transform.Elements);
        Assert.DoesNotContain(series, chart.SeriesLayer.Series);
    }

    [Fact]
    public void The_series_layer_paints_the_series_it_holds()
    {
        var chart = Chart();
        chart.Series.Add(new LineSeries<double>
        {
            ItemsSource = [0.0, 5.0, 10.0],
            YSelector = value => value,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
        });

        chart.Measure(new Size(200, 100));
        chart.Arrange(new Rect(0, 0, 200, 100));

        var bitmap = new RenderTargetBitmap(new PixelSize(200, 100));
        using (var context = bitmap.CreateDrawingContext())
        {
            chart.SeriesLayer.Render(context);
        }

        Assert.True(Ink.Count(bitmap) > 0, "the series layer should have painted the line");
    }

    [Fact]
    public void The_axis_layer_paints_the_grid_the_line_the_marks_and_the_labels()
    {
        var chart = Chart();
        chart.HorizontalRenderer.ShowLabels = true;
        chart.HorizontalRenderer.TickLength = 4;

        var layer = new AxisLayer(chart.HorizontalRenderer) { Transform = chart.Transform };
        layer.Configure(chart.PlotBounds, 1);
        layer.Arrange(new Rect(0, 0, 200, 100));
        layer.GridLinePen = new Pen(Brushes.LightGray, 1);
        layer.AxisLinePen = new Pen(Brushes.Gray, 1);
        layer.LabelBrush = Brushes.Black;

        var bitmap = new RenderTargetBitmap(new PixelSize(200, 100));
        using (var context = bitmap.CreateDrawingContext())
        {
            layer.Render(context);
        }

        Assert.True(Ink.Count(bitmap) > 0, "the axis layer should have painted grid, ticks and labels");
    }
}
