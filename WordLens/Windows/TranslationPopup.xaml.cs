using System;
using System.Windows;
using System.Windows.Interop;
using WordLens.Interop;
using WordLens.Models;

namespace WordLens.Windows;

/// <summary>
/// Small, borderless, click-through translation bubble shown near the lens.
/// </summary>
public partial class TranslationPopup : Window
{
    public IntPtr HandleOf() => new WindowInteropHelper(this).Handle;

    public TranslationPopup()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                ex | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE);
        };
    }

    public void ShowTranslation(string word, TranslationResult? result, int physicalAnchorX, int physicalAnchorY)
    {
        WordText.Text = word;

        if (result == null)
            TranslationText.Text = "暂无释义";
        else
        {
            var pos = string.IsNullOrEmpty(result.PartOfSpeech) ? "" : $"[{result.PartOfSpeech}] ";
            TranslationText.Text = pos + result.Translation;
        }

        Visibility = Visibility.Visible;

        // Place to the lower-right of the lens center.
        var dip = DpiHelper.PhysicalToDip(physicalAnchorX + 120, physicalAnchorY + 120);
        Left = dip.X;
        Top = dip.Y;
        // Keep on-screen-ish; nudge if it would overflow right/bottom is omitted for MVP.
    }

    public void HideTranslation() => Visibility = Visibility.Collapsed;

    private System.Windows.Threading.DispatcherTimer? _hintTimer;

    /// <summary>Show an arbitrary hint (not tied to a dictionary lookup) for a few seconds.</summary>
    public void ShowHint(string text)
    {
        WordText.Text = "WordLens";
        TranslationText.Text = text;
        Visibility = Visibility.Visible;

        _hintTimer?.Stop();
        _hintTimer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromSeconds(6) };
        _hintTimer.Tick += (s, e) =>
        {
            _hintTimer?.Stop();
            _hintTimer = null;
            Visibility = Visibility.Collapsed;
        };
        _hintTimer.Start();
    }
}
