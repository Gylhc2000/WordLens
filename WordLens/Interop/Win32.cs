using System;
using System.Runtime.InteropServices;

namespace WordLens.Interop;

/// <summary>
/// P/Invoke declarations for the Win32 APIs used by WordLens.
/// DPI / monitor APIs live in shcore.dll; the rest in user32/gdi32.
/// </summary>
internal static class NativeMethods
{
    // ---- user32 ----
    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateEllipticRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    // ---- shcore.dll (DPI) ----
    [DllImport("shcore.dll")]
    public static extern bool GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("shcore.dll")]
    public static extern int SetProcessDpiAwareness(PROCESS_DPI_AWARENESS value);

    // ---- gdi32 ----
    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    // Window styles / ex-styles
    public const int WS_CHILD = unchecked((int)0x40000000);
    public const int WS_VISIBLE = 0x10000000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int GWL_EXSTYLE = -20;

    // Virtual screen metrics
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    // Hotkeys
    public const uint MOD_NOREPEAT = 0x4000;
    public const int WM_HOTKEY = 0x0312;
    public const uint VK_F8 = 0x77;
    public const uint VK_F9 = 0x78;
    public const uint VK_F10 = 0x79;
    public const uint VK_ESCAPE = 0x1B;

    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int MDT_EFFECTIVE_DPI = 0;

    public const int SRCCOPY = 0x00CC0020;

    public enum PROCESS_DPI_AWARENESS
    {
        Unaware = 0,
        SystemAware = 1,
        PerMonitorAwareV2 = 2
    }
}
