using System;
using System.Windows;
using System.Windows.Interop;
using WordLens.Interop;
using WordLens.Magnification;

namespace WordLens.Windows;

/// <summary>
/// The round, always-on-top, click-through magnifier window.
/// </summary>
public partial class LensWindow : Window
{
    private readonly MagnifierController _magnifier;

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    public LensWindow(MagnifierController magnifier)
    {
        InitializeComponent();
        _magnifier = magnifier;
        Magnifier.Controller = magnifier;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Click-through: the lens must never block the window underneath.
        int ex = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(Handle, NativeMethods.GWL_EXSTYLE, ex | NativeMethods.WS_EX_TRANSPARENT);
    }

    /// <summary>Move the lens so its center sits on the given physical screen point.</summary>
    public void SetCenterPhysical(int physicalX, int physicalY)
    {
        var dip = DpiHelper.PhysicalToDip(physicalX, physicalY);
        Left = dip.X - Width / 2;
        Top = dip.Y - Height / 2;
    }

    public void UpdateMagnifier(int physicalX, int physicalY, double zoom)
    {
        // The physical lens size now lives inside MagnifierHost (it uses the native
        // child window's actual applied size), so we just forward center + zoom here.
        Magnifier.UpdateMagnifier(physicalX, physicalY, zoom);
    }

    /// <summary>
    /// Enable (zoom &gt; 1) or disable (1.0x) the magnifier layer. At 1.0x the magnifier child
    /// is collapsed AND its native window hidden so the lens becomes a truly transparent ring
    /// (no capture, no lag, no jitter on the seamless edge). At zoom &gt; 1 both are restored.
    /// </summary>
    public void SetMagnifierActive(bool active)
    {
        Magnifier.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        Magnifier.SetMagnifierVisible(active);
    }
}
