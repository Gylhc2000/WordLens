using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WordLens.Capture;
using WordLens.Detection;
using WordLens.Dictionary;
using WordLens.Input;
using WordLens.Interop;
using WordLens.Magnification;
using WordLens.Models;
using WordLens.OCR;
using WordLens.Pipeline;
using WordLens.Services;
using WordLens.Windows;

namespace WordLens;

/// <summary>
/// Wires together lens/overlay/popup windows, the magnifier, OCR pipeline and hotkeys,
/// and drives the 60 FPS cursor-follow loop.
/// </summary>
public sealed class AppController : IDisposable
{
    // Zoom levels cycled by F10. Index 0 (1.0x) is the default = no magnification.
    private static readonly double[] ZoomLevels = { 1.0, 2.0, 3.0 };
    private int _zoomIndex = 0;
    private double _zoom = 1.0;
    private const int LensSize = 220;

    private readonly LoggingService _logger;
    private readonly IScreenCapture _capture;
    private readonly IOcrService _ocr;
    private readonly WordHitTester _hitTester;
    private readonly IDictionaryService _dictionary;
    private readonly CacheService _cache;
    private readonly MagnifierController _magnifier;

    private readonly LensWindow _lens;
    private readonly LensOverlay _overlay;
    private readonly TranslationPopup _popup;
    private readonly TranslationPipeline _pipeline;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly DispatcherTimer _cursorTimer;

    private bool _locked;
    private bool _lensVisible = true;
    // Whether the magnifier layer is currently painting. At 1.0x it's OFF (the lens is a
    // transparent overlay, desktop shows through with zero capture latency). Only zoom > 1
    // turns it ON. This is what removes the jitter that only appears at 1.0x.
    private bool _magnifierActive = false;
    private int _lastPx;
    private int _lastPy;

    // The cursor target to apply on the next rendered frame. Setting the magnifier source from
    // CompositionTarget.Rendering (instead of immediately in the timer tick) guarantees it runs
    // AFTER WPF has committed the window's new position for that frame — so the source rect and
    // the on-screen window are always aligned, eliminating the swim/jelly while moving.
    private int _pendingPx;
    private int _pendingPy;
    private double _pendingZoom;
    private bool _magnifierDirty;

    public AppController(
        LoggingService logger,
        IScreenCapture capture,
        IOcrService ocr,
        WordHitTester hitTester,
        IDictionaryService dictionary,
        CacheService cache,
        MagnifierController magnifier,
        string? dbPath)
    {
        _logger = logger;
        _capture = capture;
        _ocr = ocr;
        _hitTester = hitTester;
        _dictionary = dictionary;
        _cache = cache;
        _magnifier = magnifier;

        _lens = new LensWindow(magnifier);
        _overlay = new LensOverlay();
        _popup = new TranslationPopup();

        _pipeline = new TranslationPipeline(capture, ocr, hitTester, dictionary, cache, logger);
        _pipeline.TranslationUpdated += OnTranslationUpdated;
        _pipeline.NoWord += () => _popup.HideTranslation();

        _hotkeys = new GlobalHotkeyService(_lens, logger);
        _hotkeys.HotkeyPressed += OnHotkey;

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _cursorTimer.Tick += OnCursorTick;
    }

    public void Start()
    {
        _logger.LogInfo("Start: showing lens window...");
        _lens.Show();
        _logger.LogInfo("Start: lens shown.");
        _overlay.Show();
        _logger.LogInfo("Start: overlay shown.");
        _popup.Show();
        _popup.HideTranslation();
        _logger.LogInfo("Start: popup shown.");

        // Prime once at the current cursor so the very first painted frame is already
        // correctly positioned. The magnifier layer starts OFF at 1.0x (transparent overlay),
        // so we only prime its source when it's actually active.
        if (NativeMethods.GetCursorPos(out var primePt))
        {
            _lens.Dispatcher.Invoke(() =>
            {
                _lens.SetCenterPhysical(primePt.X, primePt.Y);
                _lens.SetMagnifierActive(_magnifierActive);
                if (_magnifierActive)
                    _lens.UpdateMagnifier(primePt.X, primePt.Y, _zoom);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        _hotkeys.Register();
        _logger.LogInfo("Start: hotkeys registered.");

        // Exclude our own windows from the magnifier to avoid feedback loops.
        var exclude = new System.Collections.Generic.List<IntPtr> { _lens.Handle, _overlay.Handle, _popup.HandleOf() };
        exclude.RemoveAll(h => h == IntPtr.Zero);
        if (exclude.Count > 0)
            _magnifier.SetFilterList(exclude.ToArray());
        _logger.LogInfo($"Start: magnifier filter set ({exclude.Count} windows).");

        _pipeline.Start();
        CompositionTarget.Rendering += OnRendering;
        _cursorTimer.Start();
        _logger.LogInfo("WordLens ready. F8 toggle lens, F10 zoom, F9 lock, Esc exit.");

        // Visible proof the app is alive (since the lens is mostly transparent).
        _popup.ShowHint("已启动 · F8 开关放大镜 · F10 放大 · F9 锁定 · Esc 退出");
    }

    private void OnCursorTick(object? sender, EventArgs e)
    {
        if (!_lensVisible) return;
        if (!NativeMethods.GetCursorPos(out var pt)) return;

        _lastPx = pt.X;
        _lastPy = pt.Y;

        _lens.SetCenterPhysical(pt.X, pt.Y);
        _overlay.SetCenterPhysical(pt.X, pt.Y);

        // Stash the target; the magnifier source is applied on the render frame (see OnRendering)
        // so it stays in lockstep with the window's committed position — no lead/lag, no jelly.
        _pendingPx = pt.X;
        _pendingPy = pt.Y;
        _pendingZoom = _zoom;
        _magnifierDirty = true;

        _pipeline.UpdateCursor(new System.Drawing.Point(pt.X, pt.Y));
    }

    /// <summary>
    /// Runs once per rendered frame, AFTER WPF has committed the lens/overlay window positions
    /// for that frame. Applying MagSetWindowSource here (instead of immediately in the timer
    /// tick) keeps the magnified source rect exactly aligned with where the window actually is,
    /// which is what removes the swim/jelly during movement.
    /// </summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_magnifierDirty || !_magnifier.Initialized || !_magnifierActive) return;
        _lens.UpdateMagnifier(_pendingPx, _pendingPy, _pendingZoom);
        _magnifierDirty = false;
    }

    private void OnTranslationUpdated(OcrWordInfo hit, TranslationResult? result)
    {
        _popup.ShowTranslation(hit.Text, result, _lastPx, _lastPy);
    }

    private void OnHotkey(WordLensHotkey hk)
    {
        switch (hk)
        {
            case WordLensHotkey.ToggleLens:
                _lensVisible = !_lensVisible;
                var vis = _lensVisible ? Visibility.Visible : Visibility.Collapsed;
                _lens.Visibility = vis;
                _overlay.Visibility = vis;
                if (!_lensVisible) _popup.HideTranslation();
                _logger.LogInfo(_lensVisible ? "Lens shown." : "Lens hidden.");
                break;

            case WordLensHotkey.ToggleZoom:
                _zoomIndex = (_zoomIndex + 1) % ZoomLevels.Length;
                _zoom = ZoomLevels[_zoomIndex];
                _pendingZoom = _zoom;
                _magnifierDirty = true;

                // The 1.0x level is a pure transparent overlay — no magnifier painting, so the
                // desktop shows through with zero capture latency (this is what kills the 1.0x
                // jitter). Only zoom > 1 actually needs the magnifier layer enabled.
                bool active = _zoom > 1.0;
                if (active != _magnifierActive)
                {
                    _magnifierActive = active;
                    _lens.SetMagnifierActive(active);
                }

                // Apply now too: if the cursor is stationary there's no layout change, so the
                // render-frame handler wouldn't fire — apply immediately for that case (only
                // meaningful while the magnifier layer is actually active).
                if (_magnifier.Initialized && _magnifierActive)
                    _lens.UpdateMagnifier(_lastPx, _lastPy, _zoom);
                _logger.LogInfo($"Zoom set to {_zoom:0.0}x (magnifier {(active ? "on" : "off")})");
                _popup.ShowHint(_zoom <= 1.0 ? "原比例 ×1.0（未放大）" : $"放大 ×{_zoom:0.0}");
                break;

            case WordLensHotkey.ToggleLock:
                _locked = !_locked;
                _pipeline.SetLocked(_locked);
                _logger.LogInfo(_locked ? "Locked." : "Unlocked.");
                break;

            case WordLensHotkey.Exit:
                _logger.LogInfo("Exit requested.");
                Application.Current.Shutdown();
                break;
        }
    }

    public void Dispose()
    {
        CompositionTarget.Rendering -= OnRendering;
        _cursorTimer.Stop();
        _pipeline.Dispose();
        _hotkeys.Dispose();
        _magnifier.Uninitialize();
        try { _lens.Close(); _overlay.Close(); _popup.Close(); } catch { }
    }
}
