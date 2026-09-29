using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WordLens.Models;
using WordLens.Services;

namespace WordLens.Dictionary;

/// <summary>
/// Reads from dictionary.db (SQLite). Two-step lookup:
///   1. entries.word
///   2. if not found, resolve via word_forms.form -> lemma, then entries.word
/// All queries are parameterized.
/// </summary>
public sealed class SqliteDictionaryService : IDictionaryService
{
    private readonly string _dbPath;
    private readonly LoggingService _logger;
    private readonly object _gate = new();

    public bool IsOpen { get; private set; }

    public SqliteDictionaryService(string dbPath, LoggingService logger)
    {
        _dbPath = dbPath;
        _logger = logger;

        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
        {
            _logger.LogWarning("dictionary.db not found at '{Path}'. Translation disabled.", _dbPath);
            IsOpen = false;
            return;
        }
        try
        {
            using var conn = Open();
            IsOpen = true;
            _logger.LogInfo("Opened dictionary.db ({Path}).", _dbPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open dictionary.db");
            IsOpen = false;
        }
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly;Cache=Shared;");
        conn.Open();
        return conn;
    }

    public async Task<TranslationResult?> LookupAsync(string normalizedWord)
    {
        if (!IsOpen) return null;
        try
        {
            await using var conn = Open();
            var row = QueryEntry(conn, normalizedWord);
            if (row == null)
            {
                var lemma = QueryLemma(conn, normalizedWord);
                if (lemma != null) row = QueryEntry(conn, lemma);
            }
            if (row == null) return null;

            return new TranslationResult
            {
                Word = normalizedWord,
                Phonetic = NormalizeField(row.Value.phonetic),
                PartOfSpeech = NormalizeField(row.Value.pos),
                Translation = NormalizeField(row.Value.translation) ?? string.Empty,
                Definition = NormalizeField(row.Value.definition)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dictionary lookup failed for '{Word}'", normalizedWord);
            return null;
        }
    }

    /// <summary>
    /// ECDICT stores multi-sense fields with a literal backslash-n ("\n") separator.
    /// Convert those into real newlines so the popup can wrap each sense onto its own line.
    /// </summary>
    private static string? NormalizeField(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\\n", "\n").Trim();
    }

    private static (string? phonetic, string? pos, string? translation, string? definition)? QueryEntry(
        SqliteConnection conn, string word)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT phonetic, pos, translation, definition FROM entries WHERE word = @w LIMIT 1;";
        cmd.Parameters.Add(new SqliteParameter("@w", word));
        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            return (
                r.IsDBNull(0) ? null : r.GetString(0),
                r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3));
        }
        return null;
    }

    private static string? QueryLemma(SqliteConnection conn, string form)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT lemma FROM word_forms WHERE form = @f LIMIT 1;";
        cmd.Parameters.Add(new SqliteParameter("@f", form));
        var v = cmd.ExecuteScalar();
        return v == null ? null : v.ToString();
    }
}
