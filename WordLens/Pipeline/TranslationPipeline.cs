using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using WordLens.Capture;
using WordLens.Detection;
using WordLens.Dictionary;
using WordLens.Models;
using WordLens.OCR;
using WordLens.Services;
using WordLens.Util;

namespace WordLens.Pipeline;

/// <summary>
/// Orchestrates the full vertical slice:
///   cursor -> capture region -> OCR words -> hit test center word
///          -> normalize -> dictionary (cached) -> UI callback.
///
/// Threading model:
///   * UpdateCursor / SetLocked are called from the UI thread every frame (cheap).
///   * OCR + lookup run on a throttled timer, marshalled onto the UI (STA) thread
///     via the captured SynchronizationContext so Windows.Media.Ocr is happy.
///   * At most ONE OCR runs at a time; stale results are discarded by request id.
/// </summary>
public sealed class TranslationPipeline : IDisposable
{
    private readonly IScreenCapture _capture;
    private readonly IOcrService _ocr;
    private readonly WordHitTester _hitTester;
    private readonly IDictionaryService _dictionary;
    private readonly CacheService _cache;
    private readonly LoggingService _logger;

    private readonly int _captureWidth;
    private readonly int _captureHeight;

    private System.Threading.Timer? _ocrTimer;
    private TaskScheduler? _uiScheduler;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts;

    private Point _latestCursor; // physical pixels
    private int _latestRequestId;
    private bool _locked;
    private string? _lastWord;

    public event Action<OcrWordInfo, TranslationResult?>? TranslationUpdated;
    public event Action? NoWord;

    public TranslationPipeline(
        IScreenCapture capture,
        IOcrService ocr,
        WordHitTester hitTester,
        IDictionaryService dictionary,
        CacheService cache,
        LoggingService logger,
        int captureWidth = 480,
        int captureHeight = 180)
    {
        _capture = capture;
        _ocr = ocr;
        _hitTester = hitTester;
        _dictionary = dictionary;
        _cache = cache;
        _logger = logger;
        _captureWidth = captureWidth;
        _captureHeight = captureHeight;
    }

    public void Start()
    {
        try { _uiScheduler = TaskScheduler.FromCurrentSynchronizationContext(); }
        catch { _uiScheduler = null; }

        _cts = new CancellationTokenSource();
        // ~110 ms debounce -> ~9 OCR/sec instead of 60, keeping CPU sane.
        _ocrTimer = new System.Threading.Timer(_ => Tick(), null, 110, 110);
    }

    public void Stop()
    {
        _ocrTimer?.Dispose();
        _ocrTimer = null;
        try { _cts?.Cancel(); } catch { }
    }

    public void UpdateCursor(Point physicalCursor) => _latestCursor = physicalCursor;

    public void SetLocked(bool locked) => _locked = locked;

    private void Tick()
    {
        if (_locked || !_ocr.IsAvailable) return;

        Task run;
        if (_uiScheduler != null)
            run = Task.Factory.StartNew(RunOcrAsync, CancellationToken.None, TaskCreationOptions.None, _uiScheduler).Unwrap();
        else
            run = RunOcrAsync();

        // Fire-and-forget; errors are handled inside RunOcrAsync.
        _ = run.ContinueWith(t => { if (t.Exception != null) { /* logged inside */ } }, TaskScheduler.Default);
    }

    private Rectangle ComputeCaptureRect(Point center)
    {
        return new Rectangle(
            center.X - _captureWidth / 2,
            center.Y - _captureHeight / 2,
            _captureWidth,
            _captureHeight);
    }

    private async Task RunOcrAsync()
    {
        int myId = Interlocked.Increment(ref _latestRequestId);

        // Cancel any in-flight OCR and make sure only one runs at a time.
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        if (!await _gate.WaitAsync(0, ct))
            return; // a previous OCR is still running; skip this tick (single-flight)

        try
        {
            var center = _latestCursor;
            if (myId != Volatile.Read(ref _latestRequestId)) return; // superseded already

            var rect = ComputeCaptureRect(center);
            using var bmp = _capture.CaptureRegion(rect);
            if (bmp == null) return;
            if (myId != Volatile.Read(ref _latestRequestId)) return;

            var words = await _ocr.RecognizeWordsAsync(bmp, ct);
            if (myId != Volatile.Read(ref _latestRequestId)) return;

            // Convert cursor into capture-local coordinates.
            int localX = center.X - rect.X;
            int localY = center.Y - rect.Y;
            var hit = _hitTester.FindWord(words, localX, localY);

            if (hit == null)
            {
                _lastWord = null;
                NoWord?.Invoke();
                return;
            }

            var normalized = TextNormalizer.Normalize(hit.Text);
            if (!TextNormalizer.IsEnglishWord(normalized))
            {
                _lastWord = null;
                NoWord?.Invoke();
                return;
            }

            // Avoid duplicate UI/dictionary work for the same word.
            if (normalized == _lastWord) return;
            _lastWord = normalized;

            TranslationResult? result = _cache.TryGet(normalized, out var cached)
                ? cached
                : await LookupAndCache(normalized, ct);

            if (myId != Volatile.Read(ref _latestRequestId)) return;
            TranslationUpdated?.Invoke(hit, result);
        }
        catch (OperationCanceledException)
        {
            // Stale request superseded — ignore.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline OCR tick failed");
        }
        finally
        {
            try { _gate.Release(); } catch { }
        }
    }

    private async Task<TranslationResult?> LookupAndCache(string word, CancellationToken ct)
    {
        var r = await _dictionary.LookupAsync(word);
        if (!ct.IsCancellationRequested)
            _cache.Set(word, r);
        return r;
    }

    public void Dispose() => Stop();
}
