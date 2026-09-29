using WordLens.Util;
using Xunit;

namespace WordLens.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("  Hello ", "hello")]
    [InlineData("WORLD", "world")]
    [InlineData("MixedCase-Word", "mixedcase-word")]
    public void Normalize_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TextNormalizer.Normalize(null!));
    }

    [Theory]
    [InlineData("hello", true)]
    [InlineData("don't", true)]
    [InlineData("user-friendly", true)]
    [InlineData("mother-in-law", true)]
    [InlineData("win10", true)]
    [InlineData("test.", true)]
    [InlineData("a", true)]
    public void IsEnglishWord_ValidTokens(string input, bool expected)
    {
        Assert.Equal(expected, TextNormalizer.IsEnglishWord(input));
    }

    [Theory]
    [InlineData("")]               // empty
    [InlineData("   ")]            // whitespace only
    [InlineData("123")]            // no letter
    [InlineData("hello world")]    // space
    [InlineData("foo@bar")]        // email-ish
    [InlineData("a/b")]            // slash
    [InlineData("C#")]             // '#' invalid
    [InlineData("hello:world")]    // colon
    [InlineData("你好")]        // CJK
    [InlineData("こんにちは")]      // Hiragana
    public void IsEnglishWord_RejectedTokens(string input)
    {
        Assert.False(TextNormalizer.IsEnglishWord(input));
    }

    [Fact]
    public void IsEnglishWord_TooLong_ReturnsFalse()
    {
        Assert.False(TextNormalizer.IsEnglishWord(new string('a', 61)));
        Assert.True(TextNormalizer.IsEnglishWord(new string('a', 60)));
    }
}
