using System;
using System.Windows;
using System.Windows.Interop;
using WordLens.Interop;

namespace WordLens.Windows;

/// <summary>
/// Transparent click-through overlay that draws the lens ring + crosshair on top
/// of the magnifier without triggering HwndHost airspace issues.
/// </summary>
public partial class LensOverlay : Window
{
    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    public LensOverlay()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                ex | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE);
        };
    }

    public void SetCenterPhysical(int physicalX, int physicalY)
    {
        var dip = DpiHelper.PhysicalToDip(physicalX, physicalY);
        Left = dip.X - Width / 2;
        Top = dip.Y - Height / 2;
    }
}
