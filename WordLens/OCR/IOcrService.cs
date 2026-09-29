using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using WordLens.OCR;

namespace WordLens.OCR;

/// <summary>
/// Local OCR that returns per-word results (text + bounding rect), not just a blob of text.
/// </summary>
public interface IOcrService
{
    /// <summary>False when the en-US recognizer could not be created.</summary>
    bool IsAvailable { get; }

    Task<OcrWordInfo[]> RecognizeWordsAsync(Bitmap image, CancellationToken ct);
}
