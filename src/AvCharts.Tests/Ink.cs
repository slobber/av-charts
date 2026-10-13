using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;

namespace AvCharts.Tests;

/// <summary>Counts the pixels a test actually painted, so "it drew something" is a claim with evidence.</summary>
internal static class Ink
{
    public static int Count(RenderTargetBitmap bitmap)
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
}
