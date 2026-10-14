using Avalonia;
using Avalonia.Controls;
using AvCharts.Charts;
using AvCharts.Controls;

namespace AvCharts.Demo;

// The ECG page, its playground, and the synthetic lead they both draw.
public static partial class Pages
{
    /// <summary>A strip of ECG: a fixed scale, an even grid, sampling, and gaps where the lead came off.</summary>
    /// <returns>The page.</returns>
    internal static DemoPage Ecg()
    {
        // A hundred seconds at two hundred samples a second - twenty thousand readings - sampled down to the
        // pixels there are, with three dropouts in them.
        //
        // The scales are the paper of a strip chart: forty pixels a second across and eighty a unit up, so one
        // small square is eight pixels each way - a grid of squares rather than a grid of rectangles. The height
        // is what makes that come out: three units over two hundred and forty pixels of plot is eighty a unit.
        var chart = new CartesianChart
        {
            // Starting at twenty-nine seconds rather than at nought so that the first dropout is on the page: a
            // strip chart is read at a paper speed, and a reader can pan along it to the others.
            // Forty pixels a second across and eighty a unit up, with an interval of one second and of half a
            // unit: the small squares come out eight pixels either way, which is the paper a strip chart is read
            // on. An interval left to the axis itself would have been two seconds - eighty pixels against forty -
            // and the grid would have been a grid of rectangles.
            HorizontalAxis = new LinearAxis { ScaleMode = AxisScaleMode.Fixed, PixelsPerUnit = 40, Minimum = 29, Interval = 1 },
            VerticalAxis = new LinearAxis { Minimum = -1.5, Maximum = 1.5, Interval = 0.5 },
            Height = SquareGridHeight,
            ShowCrosshair = true,
        };

        chart.Series.Add(Lead());

        return Page(
            "ECG",
            "Twenty thousand samples at a fixed scale, sampled down, with dropouts.",
            Example("A hundred seconds of a synthetic lead", chart),
            Try("Try it: the same strip, at the paper speed and the gain you ask for", EcgPlayground()));
    }

    /// <summary>The strip the ECG page and its playground share: twenty thousand samples of a synthetic lead.</summary>
    /// <returns>The series.</returns>
    /// <remarks>
    /// The reading carries its own time, because a selector is handed the record rather than its place in the
    /// list: asking for "index / 200" of a sample that is a plain number gives the reading divided by two hundred,
    /// which is a range of a hundredth of a second - and a curve drawn in one pixel column, which is exactly what
    /// the page used to show.
    /// </remarks>
    private static LineSeries<Sample> Lead() =>
        new()
        {
            DisplayName = "Lead II",
            ItemsSource = [.. Enumerable.Range(0, 20_000).Select(index => new Sample(index / 200.0, Beat(index)))],
            XSelector = sample => sample.Seconds,
            YSelector = sample => sample.Reading,
            StrokeThickness = 1.5,
            Sampling = SamplingMode.MinMax,
        };

    /// <summary>One reading of the strip, with the moment it was taken at.</summary>
    /// <param name="Seconds">How far into the recording it is.</param>
    /// <param name="Reading">What the lead read there, or not-a-number where it came off.</param>
    private sealed record Sample(double Seconds, double Reading);

    /// <summary>One beat of a synthetic signal, and not-a-number where the lead came off.</summary>
    /// <param name="index">The sample.</param>
    /// <returns>The reading.</returns>
    private static double Beat(int index)
    {
        var second = index / 200.0;

        if (second is (> 31 and < 32.2) or (> 58 and < 58.4) or (> 74 and < 76))
        {
            return double.NaN;
        }

        var phase = (second % 0.8) / 0.8;

        return phase switch
        {
            < 0.04 => 0.1 * Math.Sin(phase * Math.PI / 0.04),
            < 0.08 => -0.15,
            < 0.11 => 1.2 * Math.Sin((phase - 0.08) * Math.PI / 0.03),
            < 0.15 => -0.35,
            < 0.3 => 0.05 * Math.Sin((phase - 0.15) * Math.PI / 0.15),
            _ => 0.02 * Math.Sin(second * 6),
        };
    }

    /// <summary>
    /// The strip with knobs on: the paper speed across, the gain up, and the corner to drag.
    /// </summary>
    /// <returns>The playground.</returns>
    /// <remarks>
    /// What a real strip chart is read with is a cell of fixed size: the paper decides it, and what the settings
    /// change is what a cell means - how many seconds across, how many millivolts up. So both axes hold a fixed
    /// scale and the intervals are worked out from it, which keeps the squares square at every speed, every gain
    /// and every size the frame is dragged to. The window is then placed around the baseline rather than the plot
    /// being stretched around it: a taller frame is more paper, not a taller signal.
    /// </remarks>
    private static Control EcgPlayground()
    {
        // What one cell of the paper is, in pixels. The page's strip is drawn with the same idea held by hand -
        // it sets one interval and sizes the chart so that the squares come out square - and this one holds it by
        // arithmetic instead, because a frame that can be dragged has no size to work it out from.
        const double Cell = 40;

        var across = new LinearAxis
        {
            ScaleMode = AxisScaleMode.Fixed,
            PixelsPerUnit = Cell,
            Minimum = 29,
            Interval = 1,
        };

        var up = new LinearAxis
        {
            ScaleMode = AxisScaleMode.Fixed,
            PixelsPerUnit = Cell * 2,
            Minimum = -1.5,
            Interval = 0.5,
        };

        var chart = new CartesianChart
        {
            HorizontalAxis = across,
            VerticalAxis = up,
            ShowCrosshair = true,
        };

        chart.Series.Add(new LineSeries<Sample>
        {
            DisplayName = "Lead II",
            ItemsSource = [.. Enumerable.Range(0, 20_000).Select(index => new Sample(index / 200.0, Beat(index)))],
            XSelector = sample => sample.Seconds,
            YSelector = sample => sample.Reading,
            StrokeThickness = 1.5,
            Sampling = SamplingMode.MinMax,
        });

        // Where the paper is centred on the baseline: half a plot above and half below, whatever size the frame
        // has been dragged to and whatever gain is asked for. The chrome - the labels and the line under the plot
        // - is measured from the chart rather than guessed once there is a plot to measure it from, so the
        // window stays centred to the pixel; until then it is the thirty-two pixels a chart of this kind has.
        var chrome = 32.0;

        void Centre(Size size)
        {
            if (chart.PlotBounds.Height > 1)
            {
                chrome = chart.Bounds.Height - chart.PlotBounds.Height;
            }

            if (size.Height > 1)
            {
                up.Minimum = -((size.Height - chrome) / 2) / up.PixelsPerUnit;
            }
        }

        chart.SizeChanged += (_, e) => Centre(e.NewSize);

        var speeds = new[] { 20.0, 40.0, 80.0 };
        var gains = new[] { 0.5, 1.0, 2.0 };

        void Speed(double pixels)
        {
            across.PixelsPerUnit = pixels;

            // The cell keeps its size and the seconds it holds change, which is what paper speed means.
            across.Interval = Cell / pixels;
        }

        void Gain(double times)
        {
            up.PixelsPerUnit = Cell * 2 * times;
            up.Interval = 0.5 / times;
            Centre(chart.Bounds.Size);
        }

        // Started at the height of the strip on the page above it, so that the playground opens showing the paper
        // a strip is read on: three units of signal over two hundred and forty pixels of plot, the peaks included.
        return Playground(
            chart,
            SquareGridHeight,
            Choices("Paper speed", ["20 px/s", "40 px/s", "80 px/s"], 1, picked => Speed(speeds[picked])),
            Choices("Gain", ["half", "normal", "double"], 1, picked => Gain(gains[picked])));
    }
}
