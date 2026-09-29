using System;

namespace WordLens.Util;

/// <summary>
/// Normalization + English-word filtering shared by the pipeline and tests.
/// </summary>
public static class TextNormalizer
{
    public static string Normalize(string raw)
    {
        if (raw == null) return string.Empty;
        return raw.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// True only for tokens that are plausibly a single English word:
    /// at least one letter, no spaces/URL/email/punctuation beyond
    /// apostrophe/hyphen/dot/underscore, and not pure CJK.
    /// </summary>
    public static bool IsEnglishWord(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (s.Length > 60) return false;

        bool hasLetter = false;
        foreach (var c in s)
        {
            if (char.IsLetter(c))
            {
                hasLetter = true;
                // Reject CJK / Kana / Hangul (OCR is en-US; be defensive).
                if ((c >= 0x4E00 && c <= 0x9FFF) ||   // CJK Unified
                    (c >= 0x3400 && c <= 0x4DBF) ||   // CJK Ext A
                    (c >= 0x3040 && c <= 0x30FF) ||   // Hiragana/Katakana
                    (c >= 0xAC00 && c <= 0xD7AF))     // Hangul
                {
                    return false;
                }
            }
            else if (char.IsDigit(c)) { /* allowed inside a word, e.g. "win10" */ }
            else if (c == '\'' || c == '-' || c == '.' || c == '_') { /* allowed */ }
            else return false; // spaces, '/', ':', '@', '%', etc. are invalid
        }

        return hasLetter;
    }
}
