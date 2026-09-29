using System;
using System.Collections.Concurrent;
using WordLens.Models;

namespace WordLens.Services;

/// <summary>
/// In-memory cache so repeated hovers over the same word don't re-query SQLite
/// on every OCR tick.
/// </summary>
public sealed class CacheService
{
    private readonly ConcurrentDictionary<string, TranslationResult?> _cache =
        new(StringComparer.Ordinal);

    public TranslationResult? Get(string key) =>
        _cache.TryGetValue(key, out var v) ? v : null;

    public bool TryGet(string key, out TranslationResult? value) =>
        _cache.TryGetValue(key, out value!);

    public void Set(string key, TranslationResult? value) => _cache[key] = value;

    public int Count => _cache.Count;
}
