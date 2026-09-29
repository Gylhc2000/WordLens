using System;
using System.Windows;
using WordLens.Interop;

namespace WordLens.Interop;

/// <summary>
/// Helpers to bridge physical-screen pixels (what GetCursorPos / BitBlt use)
/// and WPF DIPs (what Window.Left/Top use), honoring per-monitor DPI.
/// </summary>
public static class DpiHelper
{
    /// <summary>
    /// Enable Per-Monitor DPI Awareness V2. Must be called before any
    /// DPI-dependent Win32 call. Safe to call more than once.
    /// </summary>
    public static void SetPerMonitorV2()
    {
        try { NativeMethods.SetProcessDpiAwareness(NativeMethods.PROCESS_DPI_AWARENESS.PerMonitorAwareV2); }
        catch { /* already set or unsupported — ignore */ }
    }

    public static void GetDpiAtPoint(int physicalX, int physicalY, out double dpiX, out double dpiY)
    {
        var pt = new NativeMethods.POINT { X = physicalX, Y = physicalY };
        IntPtr hmon = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        uint dx = 96, dy = 96;
        if (hmon != IntPtr.Zero)
        {
            try
            {
                if (NativeMethods.GetDpiForMonitor(hmon, NativeMethods.MDT_EFFECTIVE_DPI, out dx, out dy)) { }
            }
            catch { /* keep 96 dpi default */ }
        }
        dpiX = dx;
        dpiY = dy;
    }

    /// <summary>Physical screen point -> WPF DIP.</summary>
    public static Point PhysicalToDip(int physicalX, int physicalY)
    {
        GetDpiAtPoint(physicalX, physicalY, out double dx, out _);
        return new Point(physicalX * 96.0 / dx, physicalY * 96.0 / dx);
    }

    /// <summary>DPI scale factor at a point (1.0 = 100%, 1.5 = 150%).</summary>
    public static double GetScaleAtPoint(int physicalX, int physicalY)
    {
        GetDpiAtPoint(physicalX, physicalY, out double dx, out _);
        return dx / 96.0;
    }
}
