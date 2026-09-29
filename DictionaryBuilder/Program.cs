using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;

namespace WordLens.DictionaryBuilder;

/// <summary>
/// Converts an ECDICT-style CSV into dictionary.db (SQLite).
///
/// Usage:
///   DictionaryBuilder --input ecdict.csv --output dictionary.db
///
/// Defaults:
///   --input   : ecdict.csv in cwd, else ecdict-sample.csv in cwd / beside exe
///   --output  : dictionary.db in cwd
///
/// The CSV header is read and columns are mapped by NAME, so column order does
/// not matter. Required columns: word, translation. Optional: phonetic, pos,
/// definition, exchange.
///
/// This build does NOT filter or downsample ECDICT — it imports everything that
/// passes basic cleaning, per the MVP spec.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string? input = null;
        string? output = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--input" && i + 1 < args.Length) input = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
        }

        if (input == null)
        {
            foreach (var candidate in new[] { "ecdict.csv", "ecdict-sample.csv",
                Path.Combine(AppContext.BaseDirectory, "ecdict.csv"),
                Path.Combine(AppContext.BaseDirectory, "ecdict-sample.csv") })
            {
                if (File.Exists(candidate)) { input = candidate; break; }
            }
        }
        if (output == null) output = "dictionary.db";

        if (input == null || !File.Exists(input))
        {
            Console.Error.WriteLine("ERROR: input CSV not found. Use --input <file>.");
            return 2;
        }

        Console.WriteLine($"Input : {Path.GetFullPath(input)}");
        Console.WriteLine($"Output: {Path.GetFullPath(output)}");

        try
        {
            Build(input, output);
            Console.WriteLine("Done.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAILED: {ex}");
            return 1;
        }
    }

    private static void Build(string input, string output)
    {
        if (File.Exists(output)) File.Delete(output);

        var connStr = new SqliteConnectionStringBuilder { DataSource = output }.ToString();
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE entries (
                    id INTEGER PRIMARY KEY,
                    word TEXT NOT NULL UNIQUE,
                    phonetic TEXT NULL,
                    pos TEXT NULL,
                    translation TEXT NOT NULL,
                    definition TEXT NULL
                );
                CREATE TABLE word_forms (
                    id INTEGER PRIMARY KEY,
                    form TEXT NOT NULL UNIQUE,
                    lemma TEXT NOT NULL
                );
                CREATE INDEX ix_entries_word ON entries(word);
                CREATE INDEX ix_word_forms_form ON word_forms(form);";
            cmd.ExecuteNonQuery();
        }

        // Map columns by header name.
        string[]? header = null;
        int idxWord = -1, idxPhonetic = -1, idxTranslation = -1, idxDefinition = -1, idxPos = -1, idxExchange = -1;

        long total = 0, inserted = 0, forms = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        using var tx = conn.BeginTransaction();
        using var cmdEntry = conn.CreateCommand();
        cmdEntry.CommandText = "INSERT OR IGNORE INTO entries(word, phonetic, pos, translation, definition) VALUES(@w,@ph,@pos,@tr,@def);";
        cmdEntry.Parameters.Add("w", SqliteType.Text);
        cmdEntry.Parameters.Add("ph", SqliteType.Text);
        cmdEntry.Parameters.Add("pos", SqliteType.Text);
        cmdEntry.Parameters.Add("tr", SqliteType.Text);
        cmdEntry.Parameters.Add("def", SqliteType.Text);

        using var cmdForm = conn.CreateCommand();
        cmdForm.CommandText = "INSERT OR IGNORE INTO word_forms(form, lemma) VALUES(@f,@l);";
        cmdForm.Parameters.Add("f", SqliteType.Text);
        cmdForm.Parameters.Add("l", SqliteType.Text);

        using var reader = new StreamReader(input, Encoding.UTF8);
        foreach (var record in ReadCsv(reader))
        {
            if (header == null)
            {
                header = record;
                idxWord = IndexOf(header, "word");
                idxPhonetic = IndexOf(header, "phonetic");
                idxTranslation = IndexOf(header, "translation");
                idxDefinition = IndexOf(header, "definition");
                idxPos = IndexOf(header, "pos");
                idxExchange = IndexOf(header, "exchange");
                if (idxWord < 0) throw new InvalidOperationException("CSV missing required column: word");
                if (idxTranslation < 0) throw new InvalidOperationException("CSV missing required column: translation");
                continue;
            }

            total++;
            string rawWord = Get(record, idxWord)?.Trim() ?? "";
            if (rawWord.Length == 0) continue;
            string word = rawWord.ToLowerInvariant();
            if (!seen.Add(word)) continue;

            string translation = Clean(Get(record, idxTranslation));
            if (translation.Length == 0) continue;

            SetText(cmdEntry.Parameters["w"], word);
            SetText(cmdEntry.Parameters["ph"], CleanOrNull(Get(record, idxPhonetic)));
            SetText(cmdEntry.Parameters["pos"], CleanOrNull(Get(record, idxPos)));
            SetText(cmdEntry.Parameters["tr"], translation);
            SetText(cmdEntry.Parameters["def"], CleanOrNull(Get(record, idxDefinition)));
            cmdEntry.ExecuteNonQuery();
            inserted++;

            string exchange = Get(record, idxExchange) ?? "";
            if (exchange.Length > 0)
            {
                foreach (var form in ParseExchangeForms(exchange, word))
                {
                    SetText(cmdForm.Parameters["f"], form);
                    SetText(cmdForm.Parameters["l"], word);
                    cmdForm.ExecuteNonQuery();
                    forms++;
                }
            }

            if (total % 100000 == 0)
                Console.WriteLine($"  processed {total:N0} rows, {inserted:N0} entries, {forms:N0} forms...");
        }

        tx.Commit();
        Console.WriteLine($"Total rows: {total:N0}, entries: {inserted:N0}, word_forms: {forms:N0}");
    }

    private static int IndexOf(string[] header, string name)
    {
        for (int i = 0; i < header.Length; i++)
            if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static string? Get(string[] record, int idx) =>
        idx >= 0 && idx < record.Length ? record[idx] : null;

    private static string Clean(string? s)
    {
        if (s == null) return "";
        return s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
    }

    private static string? CleanOrNull(string? s)
    {
        var c = Clean(s);
        return c.Length == 0 ? null : c;
    }

    private static void SetText(SqliteParameter p, string? value)
    {
        p.Value = value == null ? DBNull.Value : value;
    }

    /// <summary>
    /// ECDICT exchange format: p:downloaded/d:downloaded/i:downloading/3:downloads/0:download/s:downloads
    /// Each segment is type:form(s). We map every non-"0" form to the lemma (the row's own word).
    /// </summary>
    private static IEnumerable<string> ParseExchangeForms(string exchange, string lemma)
    {
        foreach (var seg in exchange.Split('/'))
        {
            if (seg.Length == 0) continue;
            int colon = seg.IndexOf(':');
            if (colon < 0) continue;
            string type = seg.Substring(0, colon);
            if (type == "0") continue; // base/lemma self-reference
            string rest = seg.Substring(colon + 1);
            foreach (var form in rest.Split(':'))
            {
                var f = form.Trim().ToLowerInvariant();
                if (f.Length > 0 && f != lemma)
                    yield return f;
            }
        }
    }

    /// <summary>
    /// RFC-4180-ish CSV reader that handles quoted fields (with embedded commas
    /// and newlines). Yields one string[] per record. The first record is the header.
    /// </summary>
    private static IEnumerable<string[]> ReadCsv(StreamReader r)
    {
        var sb = new StringBuilder();
        var fields = new List<string>();
        bool inQuotes = false;

        while (r.Read() is int c && c != -1)
        {
            char ch = (char)c;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (r.Peek() == '"') { sb.Append('"'); r.Read(); }
                    else inQuotes = false;
                }
                else sb.Append(ch);
            }
            else
            {
                if (ch == '"') inQuotes = true;
                else if (ch == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else if (ch == '\r') { /* skip; handled by \n */ }
                else if (ch == '\n') { fields.Add(sb.ToString()); sb.Clear(); yield return fields.ToArray(); fields.Clear(); }
                else sb.Append(ch);
            }
        }

        if (sb.Length > 0 || fields.Count > 0)
        {
            fields.Add(sb.ToString());
            yield return fields.ToArray();
        }
    }
}
