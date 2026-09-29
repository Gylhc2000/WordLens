using System.Drawing;

namespace WordLens.Capture;

/// <summary>
/// Captures a rectangular region of the (physical) screen.
/// Coordinates are in physical pixels of the virtual screen.
/// </summary>
public interface IScreenCapture
{
    /// <returns>A bitmap of the region, or null on failure.</returns>
    Bitmap? CaptureRegion(Rectangle physicalRect);
}
