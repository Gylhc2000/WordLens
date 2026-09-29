using System;
using System.Windows;
using System.Windows.Interop;
using WordLens.Interop;
using WordLens.Services;

namespace WordLens.Input;

public enum WordLensHotkey { ToggleLens, ToggleZoom, ToggleLock, Exit }

/// <summary>
/// Global hotkeys via Win32 RegisterHotKey, bound to a window handle so they
/// work without the lens needing keyboard focus.
///   F8  -> toggle lens visibility (default shown, but NOT magnified)
///   F10 -> cycle zoom levels (1.0x -> 2.0x -> 3.0x -> back to 1.0x)
///   F9  -> lock / unlock current word
///   Esc -> exit
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private readonly Window _window;
    private readonly LoggingService _logger;
    private HwndSource? _source;

    public event Action<WordLensHotkey>? HotkeyPressed;

    public GlobalHotkeyService(Window window, LoggingService logger)
    {
        _window = window;
        _logger = logger;
    }

    public void Register()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(WndProc);

        bool f8 = NativeMethods.RegisterHotKey(hwnd, 1, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F8);
        bool f10 = NativeMethods.RegisterHotKey(hwnd, 4, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F10);
        bool f9 = NativeMethods.RegisterHotKey(hwnd, 2, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F9);
        bool esc = NativeMethods.RegisterHotKey(hwnd, 3, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_ESCAPE);
        _logger.LogInfo($"Hotkeys registered: F8={f8} F10={f10} F9={f9} Esc={esc}");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            WordLensHotkey? hk = id switch
            {
                1 => WordLensHotkey.ToggleLens,
                4 => WordLensHotkey.ToggleZoom,
                2 => WordLensHotkey.ToggleLock,
                3 => WordLensHotkey.Exit,
                _ => null
            };
            if (hk.HasValue)
            {
                HotkeyPressed?.Invoke(hk.Value);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        NativeMethods.UnregisterHotKey(hwnd, 1);
        NativeMethods.UnregisterHotKey(hwnd, 4);
        NativeMethods.UnregisterHotKey(hwnd, 2);
        NativeMethods.UnregisterHotKey(hwnd, 3);
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }
}
