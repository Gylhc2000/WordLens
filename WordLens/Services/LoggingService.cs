using System;
using System.IO;
using System.Threading;

namespace WordLens.Services;

public enum LogLevel { Info, Warning, Error }

/// <summary>
/// Minimal thread-safe file logger. Never throws.
/// Writes logs/app.log under <paramref name="baseDir"/>.
/// Does NOT log raw OCR text or screen content (privacy).
/// </summary>
public sealed class LoggingService
{
    private readonly string _filePath;
    private readonly object _gate = new();

    public LoggingService(string baseDir)
    {
        try
        {
            var dir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "app.log");
            Write(LogLevel.Info, "Logging initialized.", null);
        }
        catch
        {
            _filePath = Path.Combine(Path.GetTempPath(), "wordlens_app.log");
        }
    }

    public void LogInfo(string message, params object[] args) => Write(LogLevel.Info, message, null, args);
    public void LogWarning(string message, params object[] args) => Write(LogLevel.Warning, message, null, args);
    public void LogError(Exception? ex, string message, params object[] args) => Write(LogLevel.Error, message, ex, args);

    private void Write(LogLevel level, string message, Exception? ex, object[]? args = null)
    {
        try
        {
            string text = (args != null && args.Length > 0) ? string.Format(message, args) : message;
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {text}";
            if (ex != null) line += $" | {ex.GetType().Name}: {ex.Message}";
            lock (_gate)
            {
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
