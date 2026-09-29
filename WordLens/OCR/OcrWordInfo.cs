namespace WordLens.OCR;

/// <summary>
/// Axis-aligned integer rectangle in capture-local pixel coordinates
/// (origin = top-left of the captured screen region).
/// </summary>
public readonly record struct Bounds(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Right => X + Width;
    public int Top => Y;
    public int Bottom => Y + Height;
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;

    public bool Contains(int x, int y) => x >= Left && x <= Right && y >= Top && y <= Bottom;
}

/// <summary>
/// A single OCR'd word with its bounding rect and the line it belongs to.
/// Coordinates are in the capture bitmap's pixel space.
/// </summary>
public sealed class OcrWordInfo
{
    public required string Text { get; init; }
    public required Bounds BoundingRect { get; init; }
    public int LineIndex { get; init; }
}
