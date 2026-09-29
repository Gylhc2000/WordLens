using System.Threading.Tasks;
using WordLens.Models;

namespace WordLens.Dictionary;

public interface IDictionaryService
{
    /// <summary>False when the database could not be opened.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Looks up a normalized (lowercase) word. Tries the base form first,
    /// then falls back through word_forms (lemma table).
    /// Returns null when the word is unknown.
    /// </summary>
    Task<TranslationResult?> LookupAsync(string normalizedWord);
}
