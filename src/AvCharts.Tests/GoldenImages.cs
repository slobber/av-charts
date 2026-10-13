using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Renders a control and compares the result with the image checked in beside the tests.
/// </summary>
/// <remarks>
/// <para>
/// A golden image catches what an assertion cannot describe: an axis gutter that moved by two pixels, a grid
/// line snapped to the wrong side of a pixel, a label that grew until it overlapped its neighbour.
/// </para>
/// <para>
/// Run with <c>AVCHARTS_UPDATE_GOLDENS=1</c> to record the images; without it the test only reads them, so a
/// failing build can never quietly rewrite its own expectations. A failed run leaves what it drew in
/// <c>Golden/Received</c>, which is ignored by git and there to be looked at.
/// </para>
/// </remarks>
internal static class GoldenImages
{
    /// <summary>How much of the picture is allowed to differ; a tenth of a percent.</summary>
    private const double AllowedDifference = 0.001;

    /// <summary>How far one channel may drift before a pixel counts as different.</summary>
    private const int ChannelTolerance = 8;

    private const string UpdateVariable = "AVCHARTS_UPDATE_GOLDENS";

    private static readonly PngBitmapEncoderOptions PngOptions = new();

    /// <summary>Renders a control and checks it against its golden image.</summary>
    /// <param name="name">The name of the image, without the extension.</param>
    /// <param name="control">The control to render.</param>
    /// <param name="width">The width to render at.</param>
    /// <param name="height">The height to render at.</param>
    public static void Verify(string name, Control control, int width, int height)
    {
        var approved = Path.Combine(GoldenDirectory("Approved"), name + ".png");
        var received = Path.Combine(GoldenDirectory("Received"), name + ".png");
        var bitmap = Render(control, width, height);

        Assert.True(Ink.Count(bitmap) > 0, $"{name}: nothing was drawn at all, which means the visual tree did not reach the layers");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(approved)!);
            bitmap.Save(approved, PngOptions);
            return;
        }

        Assert.True(
            File.Exists(approved),
            $"{name}: no golden image beside the tests. Record one with {UpdateVariable}=1 and check it in.");

        var expected = PixelsOf(approved);
        var actual = PixelsOf(bitmap);
        var size = bitmap.PixelSize;
        var differences = CountDifferences(expected, actual, size);
        var allowed = (int)(size.Width * (long)size.Height * AllowedDifference);

        if (differences > allowed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(received)!);
            bitmap.Save(received, PngOptions);
        }

        Assert.True(
            differences <= allowed,
            $"{name}: {differences} pixels differ (allowed {allowed}). What was drawn is in {received}; " +
            $"if the change is wanted, record it with {UpdateVariable}=1.");
    }

    private static RenderTargetBitmap Render(Control control, int width, int height)
    {
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));

        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(control);

        return bitmap;
    }

    private static byte[] PixelsOf(RenderTargetBitmap bitmap) => Copy(bitmap, bitmap.PixelSize);

    private static byte[] PixelsOf(string path)
    {
        using var image = new Bitmap(path);
        return Copy(image, image.PixelSize);
    }

    private static byte[] Copy(Bitmap bitmap, PixelSize size)
    {
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);

        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        return buffer;
    }

    private static int CountDifferences(byte[] expected, byte[] actual, PixelSize size)
    {
        Assert.Equal(expected.Length, actual.Length);

        var differences = 0;

        for (var index = 0; index < expected.Length; index += 4)
        {
            for (var channel = 0; channel < 4; channel++)
            {
                if (Math.Abs(expected[index + channel] - actual[index + channel]) > ChannelTolerance)
                {
                    differences++;
                    break;
                }
            }
        }

        return differences;
    }

    private static string GoldenDirectory(string kind)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AvCharts.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ??
            throw new InvalidOperationException("could not find the repository root from " + AppContext.BaseDirectory);

        return Path.Combine(root, "src", "AvCharts.Tests", "Golden", kind);
    }
}
