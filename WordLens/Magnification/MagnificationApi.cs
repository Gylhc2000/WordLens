using System;
using System.Runtime.InteropServices;

namespace WordLens.Magnification;

/// <summary>
/// P/Invoke for the Windows Magnification API (Magnification.dll).
/// All P/Invoke for magnification lives in this one file per the spec.
/// </summary>
internal static class MagnificationApi
{
    [DllImport("Magnification.dll")]
    public static extern bool MagInitialize();

    [DllImport("Magnification.dll")]
    public static extern bool MagUninitialize();

    [DllImport("Magnification.dll")]
    public static extern bool MagSetWindowSource(IntPtr hwnd, ref RECT pRect);

    [DllImport("Magnification.dll")]
    public static extern bool MagGetWindowSource(IntPtr hwnd, out RECT pRect);

    [DllImport("Magnification.dll")]
    public static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM pTransform);

    [DllImport("Magnification.dll")]
    public static extern bool MagSetWindowFilterList(
        IntPtr hwnd, MAG_FILTERMODE dwFilterMode, int count, IntPtr[] pExcludeHwnds);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    // float[3][3] in row-major order.
    [StructLayout(LayoutKind.Sequential)]
    public struct MAGTRANSFORM
    {
        public float m00, m01, m02;
        public float m10, m11, m12;
        public float m20, m21, m22;
    }

    public enum MAG_FILTERMODE
    {
        MAG_FILTERMODE_EXCLUDE = 0,
        MAG_FILTERMODE_INCLUDE = 1
    }
}
