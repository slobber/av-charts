using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Xunit;

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

    /// <summary>How many pixels were painted inside a rectangle.</summary>
    /// <param name="bitmap">The picture.</param>
    /// <param name="region">The rectangle to look in, in pixels.</param>
    /// <returns>The number of pixels.</returns>
    public static int Count(RenderTargetBitmap bitmap, Rect region)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = Pixels(bitmap);
        var count = 0;

        for (var y = Math.Max(0, (int)region.Top); y < Math.Min(size.Height, (int)region.Bottom); y++)
        {
            for (var x = Math.Max(0, (int)region.Left); x < Math.Min(size.Width, (int)region.Right); x++)
            {
                if (buffer[(y * stride) + (x * 4) + 3] > 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>How many pixels of one colour were painted inside a rectangle.</summary>
    /// <param name="bitmap">The picture.</param>
    /// <param name="colour">The colour to look for.</param>
    /// <param name="region">The rectangle to look in, in pixels.</param>
    /// <param name="tolerance">How far one channel may drift before a pixel stops counting as that colour.</param>
    /// <returns>The number of pixels.</returns>
    public static int Count(RenderTargetBitmap bitmap, Color colour, Rect region, int tolerance = 8)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = Pixels(bitmap);
        var (red, green, blue) = bitmap.Format == PixelFormat.Rgba8888 ? (0, 1, 2) : (2, 1, 0);
        var count = 0;

        for (var y = Math.Max(0, (int)region.Top); y < Math.Min(size.Height, (int)region.Bottom); y++)
        {
            for (var x = Math.Max(0, (int)region.Left); x < Math.Min(size.Width, (int)region.Right); x++)
            {
                var index = (y * stride) + (x * 4);

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
        }

        return count;
    }

    /// <summary>The rectangle the pixels of one colour occupy inside a region; all zeroes when there are none.</summary>
    /// <param name="bitmap">The picture.</param>
    /// <param name="column">Which column to look at.</param>
    /// <param name="colour">The colour to look for.</param>
    /// <param name="tolerance">How far one channel may drift before a pixel stops counting as that colour.</param>
    /// <returns>The top and the bottom of the pixels that carry the colour; -1 for both when the column has none.</returns>
    public static (int Top, int Bottom) Column(
        RenderTargetBitmap bitmap,
        int column,
        Color colour,
        int tolerance = 8)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = Pixels(bitmap);
        var (red, green, blue) = bitmap.Format == PixelFormat.Rgba8888 ? (0, 1, 2) : (2, 1, 0);
        var top = -1;
        var bottom = -1;

        for (var y = 0; y < size.Height; y++)
        {
            var index = (y * stride) + (column * 4);

            if (buffer[index + 3] == 0
                || Math.Abs(buffer[index + red] - colour.R) > tolerance
                || Math.Abs(buffer[index + green] - colour.G) > tolerance
                || Math.Abs(buffer[index + blue] - colour.B) > tolerance)
            {
                continue;
            }

            top = top < 0 ? y : top;
            bottom = y;
        }

        return (top, bottom);
    }

    /// <summary>How many pixels two pictures disagree about, allowing each channel to drift a little.</summary>
    /// <param name="first">One picture.</param>
    /// <param name="second">The other, drawn the same size.</param>
    /// <param name="tolerance">How far one channel may drift before a pixel counts as different.</param>
    /// <returns>The number of pixels that differ.</returns>
    public static int Differences(RenderTargetBitmap first, RenderTargetBitmap second, int tolerance = 8)
    {
        Assert.Equal(first.PixelSize, second.PixelSize);

        var left = Pixels(first);
        var right = Pixels(second);
        var differences = 0;

        for (var index = 0; index < left.Length; index += 4)
        {
            for (var channel = 0; channel < 4; channel++)
            {
                if (Math.Abs(left[index + channel] - right[index + channel]) > tolerance)
                {
                    differences++;
                    break;
                }
            }
        }

        return differences;
    }

    /// <summary>Where the pixels two pictures disagree about are; all zeroes when they agree.</summary>
    /// <param name="first">One picture.</param>
    /// <param name="second">The other, drawn the same size.</param>
    /// <param name="tolerance">How far one channel may drift before a pixel counts as different.</param>
    /// <returns>The rectangle the differing pixels occupy.</returns>
    /// <remarks>
    /// What changed between two drawings of the same thing, which is how a test can ask about one piece of writing
    /// without asking what colour it is: a claim about "the black pixels" would be a claim about how the rasterizer
    /// happened to round an edge, and that differs between machines.
    /// </remarks>
    public static (int Left, int Top, int Right, int Bottom) DifferenceBounds(
        RenderTargetBitmap first,
        RenderTargetBitmap second,
        int tolerance = 8)
    {
        Assert.Equal(first.PixelSize, second.PixelSize);

        var size = first.PixelSize;
        var stride = size.Width * 4;
        var left = Pixels(first);
        var right = Pixels(second);
        var foundLeft = size.Width;
        var foundTop = size.Height;
        var foundRight = -1;
        var foundBottom = -1;

        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var index = (y * stride) + (x * 4);

                if (Math.Abs(left[index] - right[index]) <= tolerance
                    && Math.Abs(left[index + 1] - right[index + 1]) <= tolerance
                    && Math.Abs(left[index + 2] - right[index + 2]) <= tolerance
                    && Math.Abs(left[index + 3] - right[index + 3]) <= tolerance)
                {
                    continue;
                }

                foundLeft = Math.Min(foundLeft, x);
                foundTop = Math.Min(foundTop, y);
                foundRight = Math.Max(foundRight, x);
                foundBottom = Math.Max(foundBottom, y);
            }
        }

        return foundRight < 0 ? (0, 0, 0, 0) : (foundLeft, foundTop, foundRight, foundBottom);
    }

    /// <summary>The rectangle the pixels of one colour occupy inside a region; all zeroes when there are none.</summary>
    /// <param name="bitmap">The picture.</param>
    /// <param name="colour">The colour to look for.</param>
    /// <param name="region">The rectangle to look in, in pixels.</param>
    /// <param name="tolerance">How far one channel may drift before a pixel stops counting as that colour.</param>
    /// <returns>The left, top, right and bottom of the pixels that carry the colour.</returns>
    public static (int Left, int Top, int Right, int Bottom) Bounds(
        RenderTargetBitmap bitmap,
        Color colour,
        Rect region,
        int tolerance = 8)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = Pixels(bitmap);
        var (red, green, blue) = bitmap.Format == PixelFormat.Rgba8888 ? (0, 1, 2) : (2, 1, 0);
        var left = size.Width;
        var top = size.Height;
        var right = -1;
        var bottom = -1;

        for (var y = Math.Max(0, (int)region.Top); y < Math.Min(size.Height, (int)region.Bottom); y++)
        {
            for (var x = Math.Max(0, (int)region.Left); x < Math.Min(size.Width, (int)region.Right); x++)
            {
                var index = (y * stride) + (x * 4);

                if (buffer[index + 3] == 0)
                {
                    continue;
                }

                if (Math.Abs(buffer[index + red] - colour.R) > tolerance
                    || Math.Abs(buffer[index + green] - colour.G) > tolerance
                    || Math.Abs(buffer[index + blue] - colour.B) > tolerance)
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
