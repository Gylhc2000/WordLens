using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WordLens.Services;

namespace WordLens.OCR;

/// <summary>
/// Local OCR via Windows.Media.Ocr (en-US only). Returns per-word text + bounding rect.
/// Runs fully offline; no cloud, no LLM.
/// </summary>
public sealed class WindowsOcrService : IOcrService
{
    private readonly LoggingService _logger;
    private readonly int _upscale;
    private OcrEngine? _engine;
    private bool _initTried;

    public WindowsOcrService(LoggingService logger, int upscale = 2)
    {
        _logger = logger;
        _upscale = Math.Max(1, upscale);
    }

    public bool IsAvailable => _engine != null;

    /// <summary>
    /// Create the en-US recognizer. Must run on a thread with a WinRT context
    /// (the UI/STA thread). Safe to call once at startup.
    /// </summary>
    public void Initialize()
    {
        if (_initTried) return;
        _initTried = true;
        try
        {
            if (!OcrEngine.IsLanguageSupported(new Language("en-US")))
            {
                _logger.LogWarning("English (en-US) OCR is not supported/installed on this machine.");
                _engine = null;
                return;
            }
            _engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"));
            if (_engine == null)
                _logger.LogWarning("Windows OCR engine for en-US is unavailable (English OCR language pack missing?).");
            else
                _logger.LogInfo("Windows OCR engine initialized for en-US.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Windows OCR engine");
            _engine = null;
        }
    }

    public async Task<OcrWordInfo[]> RecognizeWordsAsync(Bitmap image, CancellationToken ct)
    {
        if (image == null) return Array.Empty<OcrWordInfo>();
        if (!_initTried) Initialize();
        if (_engine == null) return Array.Empty<OcrWordInfo>();

        Bitmap? ocrInput = _upscale > 1 ? Upscale(image, _upscale) : image;
        bool ownsInput = ocrInput != image;
        try
        {
            using var ms = new MemoryStream();
            ocrInput.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            using var ras = new InMemoryRandomAccessStream();
            await RandomAccessStream.CopyAsync(ms.AsRandomAccessStream(), ras).AsTask(ct);
            ras.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(ras).AsTask(ct);
            using var soft = await decoder.GetSoftwareBitmapAsync().AsTask(ct);
            var result = await _engine.RecognizeAsync(soft).AsTask(ct);
            if (result == null) return Array.Empty<OcrWordInfo>();

            double scale = _upscale > 1 ? 1.0 / _upscale : 1.0;
            var list = new List<OcrWordInfo>();
            int lineIndex = 0;
            foreach (var line in result.Lines)
            {
                foreach (var word in line.Words)
                {
                    Rect r = word.BoundingRect;
                    var bounds = new Bounds(
                        (int)Math.Round(r.X * scale),
                        (int)Math.Round(r.Y * scale),
                        (int)Math.Round(r.Width * scale),
                        (int)Math.Round(r.Height * scale));
                    list.Add(new OcrWordInfo { Text = word.Text, BoundingRect = bounds, LineIndex = lineIndex });
                }
                lineIndex++;
            }
            return list.ToArray();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR recognition failed");
            return Array.Empty<OcrWordInfo>();
        }
        finally
        {
            if (ownsInput) ocrInput.Dispose();
        }
    }

    private static Bitmap Upscale(Bitmap src, int factor)
    {
        var dst = new Bitmap(src.Width * factor, src.Height * factor);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(src, 0, 0, dst.Width, dst.Height);
        return dst;
    }
}
