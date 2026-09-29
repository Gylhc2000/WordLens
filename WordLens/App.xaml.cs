using System;
using System.IO;
using System.Windows;
using WordLens.Capture;
using WordLens.Detection;
using WordLens.Dictionary;
using WordLens.Interop;
using WordLens.Magnification;
using WordLens.OCR;
using WordLens.Pipeline;
using WordLens.Services;

namespace WordLens;

public partial class App : Application
{
    private AppController? _controller;

    public App()
    {
        // Surface fatal UI-thread errors instead of silently exiting (which looks
        // like "double-click does nothing" to the user).
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var msg = $"{e.Exception.GetType().Name}: {e.Exception.Message}\n\n{e.Exception.StackTrace}";
            var crash = System.IO.Path.Combine(AppContext.BaseDirectory, "logs", "crash.log");
            System.IO.File.AppendAllText(crash, $"[{System.DateTime.Now:O}] UNHANDLED: {msg}\n");
            System.Windows.MessageBox.Show(msg, "WordLens 未处理异常", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        catch { /* never throw from the handler */ }
        e.Handled = true;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            StartupCore();
        }
        catch (Exception ex)
        {
            try
            {
                var crash = System.IO.Path.Combine(AppContext.BaseDirectory, "logs", "crash.log");
                System.IO.File.AppendAllText(crash, $"[{System.DateTime.Now:O}] STARTUP FATAL: {ex}\n");
            }
            catch { }
            System.Windows.MessageBox.Show(
                $"WordLens 启动失败：\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "WordLens 启动错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void StartupCore()
    {
        // Required before any Windows.* WinRT API is used.
        WinRT.ComWrappersSupport.InitializeComWrappers();
        // Per-monitor DPI Awareness V2 (must be before DPI-dependent Win32 calls).
        DpiHelper.SetPerMonitorV2();

        var baseDir = AppContext.BaseDirectory;
        var logger = new LoggingService(baseDir);
        logger.LogInfo("WordLens starting.");

        var dbPath = ResolveDictionaryPath(baseDir, logger);
        if (dbPath == null)
            logger.LogWarning("dictionary.db not found; translation will be unavailable (see README).");

        var capture = new GdiScreenCapture(logger);
        var ocr = new WindowsOcrService(logger, upscale: 2);
        if (!ocr.IsAvailable)
            logger.LogWarning("English OCR (en-US) is unavailable until first use; check the Windows OCR language pack.");

        var hitTester = new WordHitTester();
        var dictionary = new SqliteDictionaryService(dbPath ?? string.Empty, logger);
        var cache = new CacheService();
        var magnifier = new MagnifierController(logger);
        magnifier.Initialize();

        _controller = new AppController(logger, capture, ocr, hitTester, dictionary, cache, magnifier, dbPath);
        _controller.Start();

        // Create the OCR engine (synchronous; en-US must be installed).
        ocr.Initialize();
    }


    private static string? ResolveDictionaryPath(string baseDir, LoggingService logger)
    {
        var candidates = new[]
        {
            Path.Combine(baseDir, "Data", "dictionary.db"),
            Path.Combine(baseDir, "dictionary.db"),
            Path.Combine(baseDir, "..", "Data", "dictionary.db"),
            Path.Combine(baseDir, "..", "..", "Data", "dictionary.db"),
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full))
            {
                logger.LogInfo($"Using dictionary: {full}");
                return full;
            }
        }
        return null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
