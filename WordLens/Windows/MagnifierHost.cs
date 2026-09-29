using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WordLens.Interop;
using WordLens.Magnification;

namespace WordLens.Windows;

/// <summary>
/// Hosts the native "Magnifier" window (created by the Magnification API) as a
/// WPF child element so it can live inside the lens window's visual tree and
/// be positioned/clipped by WPF layout. The controller is assigned in code-behind
/// (the magnifier must be created before this HwndHost builds its child window).
/// </summary>
public sealed class MagnifierHost : HwndHost
{
    // Must match LensWindow's 220x220. HwndHost.Width can be NaN (stretch layout),
    // so we use this constant for the DIP size instead of reading the property.
    private const double LensDip = 220.0;

    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _parentHwnd = IntPtr.Zero;
    private int _lastW;
    private int _lastH;
    // The physical pixel size actually applied to the native child window. The magnifier
    // source rect is derived from THIS (not from Width*DPI) so the two never disagree.
    private int _appliedW;
    private int _appliedH;

    public MagnifierController? Controller { get; set; }

    public IntPtr MagnifierHandle => _hwnd;

    public MagnifierHost() { }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        var controller = Controller;
        _parentHwnd = hwndParent.Handle;

        // Width/Height are WPF DIPs; the native child must be sized in *physical* pixels.
        // Size it correctly from the start (primary-monitor DPI) so the first visible frame
        // already matches — otherwise a one-frame 220-vs-330 mismatch makes the lens "snap".
        double s = DpiHelper.GetScaleAtPoint(0, 0);
        int w = Math.Max(1, (int)Math.Round(LensDip * s));
        int h = Math.Max(1, (int)Math.Round(LensDip * s));
        _appliedW = w;
        _appliedH = h;

        _hwnd = NativeMethods.CreateWindowEx(
            0,
            "Magnifier",
            "WordLensMagnifier",
            NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE,
            0, 0, w, h,
            hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_hwnd != IntPtr.Zero && controller != null)
        {
            // Make the magnifier itself click-through (never eats input).
            int ex = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, ex | NativeMethods.WS_EX_TRANSPARENT);

            controller.Attach(_hwnd);
            ClipToCircle(w, h);
        }

        return new HandleRef(this, _hwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (hwnd.Handle != IntPtr.Zero)
            NativeMethods.DestroyWindow(hwnd.Handle);
    }

    /// <summary>
    /// WPF calls this whenever the HwndHost is laid out / moved / resized. The bounding
    /// box units are unreliable across DPI, so we size the native child to the parent's
    /// *actual* client rect (physical pixels) obtained from GetClientRect — this keeps the
    /// round magnifier exactly the same size and position as the WPF overlay ring.
    /// </summary>
    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        ApplyPhysicalSize();
        base.OnWindowPositionChanged(rcBoundingBox);
    }

    private void ApplyPhysicalSize()
    {
        if (_hwnd == IntPtr.Zero || _parentHwnd == IntPtr.Zero) return;
        if (!NativeMethods.GetClientRect(_parentHwnd, out var rc)) return;

        int w = rc.right - rc.left;
        int h = rc.bottom - rc.top;
        if (w <= 0 || h <= 0) return;
        // Skip if neither the size nor the applied size changed.
        if (w == _lastW && h == _lastH && w == _appliedW && h == _appliedH) return;

        NativeMethods.SetWindowPos(
            _hwnd, IntPtr.Zero, 0, 0, w, h,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        // Clip to a circle so the lens is round (matches the overlay ring size).
        ClipToCircle(w, h);

        _lastW = w;
        _lastH = h;
        _appliedW = w;
        _appliedH = h;
    }

    private void ClipToCircle(int w, int h)
    {
        IntPtr hrgn = NativeMethods.CreateEllipticRgn(0, 0, w, h);
        NativeMethods.SetWindowRgn(_hwnd, hrgn, true);
    }

    /// <summary>
    /// Drive the magnifier from the cursor. The source rect size is taken from the native
    /// child's ACTUAL applied physical size (_appliedW/_appliedH) so it always matches the
    /// window that's on screen — this is what prevents the first-frame "snap"/jitter.
    /// </summary>
    public void UpdateMagnifier(int physicalCenterX, int physicalCenterY, double zoom)
    {
        int size = Math.Max(1, Math.Min(_appliedW, _appliedH));
        Controller?.Update(physicalCenterX, physicalCenterY, zoom, size);
    }

    /// <summary>
    /// Show or hide the native magnifier child window. At 1.0x the lens is meant to be a
    /// transparent overlay (the desktop shows through untouched, zero capture latency), so the
    /// magnifier is hidden entirely — re-using MagSetWindowSource at 1:1 just re-captures the
    /// desktop behind the lens and re-introduces the ~1 frame capture lag that reads as a jitter
    /// on the seamless lens edge. Only at zoom &gt; 1 do we actually need to paint, so only then
    /// is the child shown.
    /// </summary>
    public void SetMagnifierVisible(bool visible)
    {
        if (_hwnd == IntPtr.Zero) return;
        NativeMethods.ShowWindow(_hwnd, visible ? NativeMethods.SW_SHOW : NativeMethods.SW_HIDE);
    }
}
