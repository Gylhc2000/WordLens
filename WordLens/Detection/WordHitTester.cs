using System;
using System.Collections.Generic;
using WordLens.OCR;

namespace WordLens.Detection;

/// <summary>
/// Tuning knobs for the hit test. All distances are in capture-local pixels.
/// </summary>
public sealed class HitTestOptions
{
    /// <summary>How many pixels to grow each rect outward for the fuzzy second pass.</summary>
    public int ExpandPixels { get; set; } = 8;

    /// <summary>Maximum distance (to nearest rect edge) allowed for the nearest-word fallback.</summary>
    public double MaxDistance { get; set; } = 60.0;

    /// <summary>
    /// Multiplier applied to vertical distance when scoring the nearest word.
    /// &gt; 1 makes cross-line matches cost more, so a word on the same line
    /// is preferred over a vertically-far word even if it is geometrically closer.
    /// </summary>
    public double VerticalPenalty { get; set; } = 1.5;
}

/// <summary>
/// Determines which OCR word the lens center is pointing at.
/// Three priorities, in order:
///   1. point strictly inside a word's bounding rect,
///   2. point inside the rect expanded by a few pixels (OCR jitter tolerance),
///   3. the nearest word (distance to rect), with a vertical-line penalty and a hard max-distance cap.
/// Returns null when nothing is close enough.
/// </summary>
public sealed class WordHitTester
{
    private readonly HitTestOptions _options;

    public WordHitTester(HitTestOptions? options = null) => _options = options ?? new HitTestOptions();

    public OcrWordInfo? FindWord(IReadOnlyList<OcrWordInfo> words, int cursorX, int cursorY)
    {
        if (words == null || words.Count == 0) return null;

        // Priority 1: strict containment.
        foreach (var w in words)
            if (w.BoundingRect.Contains(cursorX, cursorY)) return w;

        // Priority 2: expanded rect (small tolerance for OCR bounding-box error).
        int e = _options.ExpandPixels;
        foreach (var w in words)
        {
            var b = w.BoundingRect;
            if (cursorX >= b.Left - e && cursorX <= b.Right + e &&
                cursorY >= b.Top - e && cursorY <= b.Bottom + e) return w;
        }

        // Priority 3: nearest word by distance to the rect, with vertical-line penalty.
        OcrWordInfo? best = null;
        double bestScore = double.MaxValue;
        double pen = _options.VerticalPenalty;
        foreach (var w in words)
        {
            var b = w.BoundingRect;
            double dx = Math.Max(b.Left - cursorX, Math.Max(0, cursorX - b.Right));
            double dy = Math.Max(b.Top - cursorY, Math.Max(0, cursorY - b.Bottom));
            double dyPen = dy * pen;
            double dist = Math.Sqrt(dx * dx + dyPen * dyPen);
            if (dist < bestScore) { bestScore = dist; best = w; }
        }

        if (best != null && bestScore <= _options.MaxDistance) return best;
        return null;
    }
}
