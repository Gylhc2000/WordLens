using System;
using WordLens.Interop;
using WordLens.Services;

namespace WordLens.Magnification;

/// <summary>
/// Owns the native magnifier window (the "Magnifier" window class registered
/// by MagInitialize) and drives its source rect + zoom transform.
/// </summary>
public sealed class MagnifierController
{
    private IntPtr _hwnd;
    private readonly LoggingService _logger;

    public bool Initialized { get; private set; }

    public MagnifierController(LoggingService logger) => _logger = logger;

    public bool Initialize()
    {
        try
        {
            Initialized = MagnificationApi.MagInitialize();
            if (!Initialized)
                _logger.LogWarning("MagInitialize returned false (magnifier disabled, lens still works).");
            else
                _logger.LogInfo("Magnification API initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MagInitialize failed");
            Initialized = false;
        }
        return Initialized;
    }

    public void Uninitialize()
    {
        if (Initialized)
        {
            try { MagnificationApi.MagUninitialize(); }
            catch { /* ignore */ }
            Initialized = false;
        }
    }

    /// <summary>Called by MagnifierHost once the native child window is created.</summary>
    public void Attach(IntPtr hwnd) => _hwnd = hwnd;

    /// <summary>Exclude our own windows from being magnified (prevents feedback loop).</summary>
    public void SetFilterList(params IntPtr[] exclude)
    {
        if (_hwnd == IntPtr.Zero || exclude.Length == 0) return;
        try
        {
            MagnificationApi.MagSetWindowFilterList(
                _hwnd, MagnificationApi.MAG_FILTERMODE.MAG_FILTERMODE_EXCLUDE, exclude.Length, exclude);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MagSetWindowFilterList failed");
        }
    }

    /// <summary>
    /// Point the magnifier at a physical screen region centered on (cx, cy),
    /// magnified by <paramref name="zoom"/>. <paramref name="physicalLensSize"/>
    /// is the physical pixel size of the magnifier window.
    /// </summary>
    public void Update(int cx, int cy, double zoom, int physicalLensSize)
    {
        if (_hwnd == IntPtr.Zero) return;

        int srcW = (int)Math.Max(1, Math.Round(physicalLensSize / zoom));
        int srcH = (int)Math.Max(1, Math.Round(physicalLensSize / zoom));

        var src = new MagnificationApi.RECT
        {
            left = cx - srcW / 2,
            top = cy - srcH / 2,
            right = cx + srcW / 2,
            bottom = cy + srcH / 2
        };

        try
        {
            MagnificationApi.MagSetWindowSource(_hwnd, ref src);
            var t = new MagnificationApi.MAGTRANSFORM
            {
                m00 = (float)zoom,
                m11 = (float)zoom,
                m22 = 1f
            };
            MagnificationApi.MagSetWindowTransform(_hwnd, ref t);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Magnifier update failed");
        }
    }
}
