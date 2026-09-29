using System;
using System.Drawing;
using System.Drawing.Imaging;
using WordLens.Interop;
using WordLens.Services;

namespace WordLens.Capture;

/// <summary>
/// Screen capture via GDI BitBlt. Operates in physical (virtual-screen) pixels.
/// Isolated behind IScreenCapture so it can later be swapped for
/// Windows.Graphics.Capture without touching the rest of the app.
/// </summary>
public sealed class GdiScreenCapture : IScreenCapture
{
    private readonly LoggingService _logger;

    public GdiScreenCapture(LoggingService logger) => _logger = logger;

    public Bitmap? CaptureRegion(Rectangle rect)
    {
        try
        {
            // Clamp the source rectangle to the virtual screen so BitBlt never errors.
            int vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            int vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            int vw = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            int vh = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

            int x = Math.Max(rect.X, vx);
            int y = Math.Max(rect.Y, vy);
            int w = Math.Min(rect.Width, vx + vw - x);
            int h = Math.Min(rect.Height, vy + vh - y);
            if (w <= 0 || h <= 0) return null;

            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) return null;

            IntPtr memDc = NativeMethods.CreateCompatibleDC(screenDc);
            IntPtr hBmp = NativeMethods.CreateCompatibleBitmap(screenDc, w, h);
            IntPtr oldBmp = NativeMethods.SelectObject(memDc, hBmp);

            try
            {
                bool ok = NativeMethods.BitBlt(memDc, 0, 0, w, h, screenDc, x, y, NativeMethods.SRCCOPY);
                if (!ok)
                {
                    _logger.LogWarning("BitBlt failed for region {X},{Y},{W},{H}", x, y, w, h);
                    return null;
                }
                return Image.FromHbitmap(hBmp);
            }
            finally
            {
                NativeMethods.SelectObject(memDc, oldBmp);
                NativeMethods.DeleteObject(hBmp);
                NativeMethods.DeleteDC(memDc);
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Screen capture failed");
            return null;
        }
    }
}
