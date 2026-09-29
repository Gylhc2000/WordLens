using System.Collections.Generic;
using WordLens.Detection;
using WordLens.OCR;
using Xunit;

namespace WordLens.Tests;

public class WordHitTesterTests
{
    private static OcrWordInfo Word(string text, int x, int y, int w, int h, int line = 0) =>
        new OcrWordInfo
        {
            Text = text,
            BoundingRect = new Bounds(x, y, w, h),
            LineIndex = line,
        };

    [Fact]
    public void EmptyList_ReturnsNull()
    {
        var tester = new WordHitTester();
        Assert.Null(tester.FindWord(new List<OcrWordInfo>(), 10, 10));
        Assert.Null(tester.FindWord(null!, 10, 10));
    }

    [Fact]
    public void Priority1_StrictContainment_ReturnsWordUnderCursor()
    {
        var words = new List<OcrWordInfo>
        {
            Word("alpha", 0, 0, 100, 20),   // [0,100] x [0,20]
            Word("beta", 0, 40, 100, 20),    // [0,100] x [40,60]
        };
        var tester = new WordHitTester();

        Assert.Same(words[0], tester.FindWord(words, 50, 10));
        Assert.Same(words[1], tester.FindWord(words, 50, 50));
    }

    [Fact]
    public void Priority1_InclusiveBoundary_IsContained()
    {
        var words = new List<OcrWordInfo> { Word("edge", 10, 10, 100, 20) };
        var tester = new WordHitTester();

        // Right/Bottom edges are inclusive in Bounds.Contains.
        Assert.Same(words[0], tester.FindWord(words, 110, 30));
        Assert.Same(words[0], tester.FindWord(words, 10, 10));
    }

    [Fact]
    public void Priority2_ExpandedRect_ReturnsWordJustOutside()
    {
        var words = new List<OcrWordInfo> { Word("near", 0, 0, 100, 20) };
        var tester = new WordHitTester();

        // 5px outside the right edge, within the default ExpandPixels=8 tolerance.
        Assert.Same(words[0], tester.FindWord(words, 105, 10));
        // 12px outside -> beyond tolerance, falls through to nearest pass.
        Assert.Same(words[0], tester.FindWord(words, 112, 10)); // still within MaxDistance
    }

    [Fact]
    public void Priority3_NearestWord_WinsWhenNoContainment()
    {
        var words = new List<OcrWordInfo>
        {
            Word("left", 0, 0, 50, 20),   // center (25,10)
            Word("right", 200, 0, 50, 20), // center (225,10)
        };
        var tester = new WordHitTester();

        // Cursor between them, closer to "left".
        Assert.Same(words[0], tester.FindWord(words, 40, 10));
        Assert.Same(words[1], tester.FindWord(words, 210, 10));
    }

    [Fact]
    public void Priority3_BeyondMaxDistance_ReturnsNull()
    {
        var words = new List<OcrWordInfo> { Word("far", 0, 0, 50, 20) };
        var tester = new WordHitTester();

        Assert.Null(tester.FindWord(words, 1000, 1000));
    }

    [Fact]
    public void OverlappingWords_FirstContainingWins()
    {
        var words = new List<OcrWordInfo>
        {
            Word("first", 0, 0, 100, 100),
            Word("second", 20, 20, 100, 100),
        };
        var tester = new WordHitTester();

        // (50,50) is inside both; strict pass returns the first match.
        Assert.Same(words[0], tester.FindWord(words, 50, 50));
    }

    [Fact]
    public void VerticalPenalty_PrefersSameLineOverCloserVerticalWord()
    {
        // Cursor (120,60).
        //  D: vertical neighbour  -> dx 0,  dy 40  (pure euclid = 40)
        //  C: same-line neighbour -> dx 50, dy 0   (pure euclid = 50)
        // Without penalty D is closer (40 < 50). With penalty x1.5, D costs
        // 60 > 50, so the same-line word C should win.
        var words = new List<OcrWordInfo>
        {
            Word("C", 170, 50, 40, 20),  // [170,210] x [50,70]
            Word("D", 100, 100, 40, 20), // [100,140] x [100,120]
        };

        var penalized = new WordHitTester(new HitTestOptions { VerticalPenalty = 1.5 });
        var plain = new WordHitTester(new HitTestOptions { VerticalPenalty = 1.0 });

        Assert.Same(words[0], penalized.FindWord(words, 120, 60));
        Assert.Same(words[1], plain.FindWord(words, 120, 60));
    }
}
