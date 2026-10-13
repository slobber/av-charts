using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AvCharts.Tests;

/// <summary>Counts the pixels a test actually painted, so "it drew something" is a claim with evidence.</summary>
internal static class Ink
{
    /// <summary>The rectangle the painted pixels occupy; all zeroes when nothing was painted.</summary>
    public static (int Left, int Top, int Right, int Bottom) Bounds(RenderTargetBitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var pixels = Pixels(bitmap);
        var left = size.Width;
        var top = size.Height;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                if (pixels[(y * stride) + (x * 4) + 3] == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0 ? (0, 0, 0, 0) : (left, top, right, bottom);
    }

    public static int Count(RenderTargetBitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = Pixels(bitmap);
        var count = 0;

        for (var index = 3; index < buffer.Length; index += 4)
        {
            if (buffer[index] > 0)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>How many pixels carry a colour, allowing each channel to drift a little.</summary>
    /// <param name="bitmap">The picture.</param>
    /// <param name="colour">The colour to look for.</param>
    /// <param name="tolerance">How far one channel may drift before a pixel stops counting as that colour.</param>
    /// <returns>The number of pixels.</returns>
    public static int Count(RenderTargetBitmap bitmap, Color colour, int tolerance = 8)
    {
        var buffer = Pixels(bitmap);
        var count = 0;

        // Which byte is red depends on the format the picture carries, and Skia hands back BGRA by default.
        var (red, green, blue) = bitmap.Format == PixelFormat.Rgba8888 ? (0, 1, 2) : (2, 1, 0);

        for (var index = 0; index < buffer.Length; index += 4)
        {
            if (buffer[index + 3] == 0)
            {
                continue;
            }

            if (Math.Abs(buffer[index + red] - colour.R) <= tolerance
                && Math.Abs(buffer[index + green] - colour.G) <= tolerance
                && Math.Abs(buffer[index + blue] - colour.B) <= tolerance)
            {
                count++;
            }
        }

        return count;
    }

    private static byte[] Pixels(RenderTargetBitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

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
}
