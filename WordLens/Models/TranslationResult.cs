namespace WordLens.Models;

/// <summary>
/// Result of looking up a (normalized) English word in the local dictionary.
/// </summary>
public sealed class TranslationResult
{
    public required string Word { get; init; }
    public string? Phonetic { get; init; }
    public string? PartOfSpeech { get; init; }
    public required string Translation { get; init; }
    public string? Definition { get; init; }
}
