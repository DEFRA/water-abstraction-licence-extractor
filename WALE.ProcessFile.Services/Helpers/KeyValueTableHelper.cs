using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

/// <summary>
/// Reads two-column key/value tables - a label cell on the left, its value on the right.
///
/// Extractors report the same region at more than one granularity: a v3 permit yields both a 7x2
/// and an 18x2 table starting at the same key, and the copies disagree, one holding a wrapped
/// value complete where the other has it truncated. Hence longest-value-wins throughout.
/// </summary>
public static class KeyValueTableHelper
{
    /// <summary>
    /// Every pair across the supplied tables, longest value kept per key. Only two-column tables
    /// contribute.
    /// </summary>
    public static Dictionary<string, string> ReadPairs(IReadOnlyList<DocumentTable> tables)
    {
        var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables.Where(table => table.ColumnCount == 2))
        {
            foreach (var row in table.Cells.GroupBy(cell => cell.RowIndex))
            {
                var key = Normalise(row.FirstOrDefault(cell => cell.ColumnIndex == 0)?.Content);
                var value = row.FirstOrDefault(cell => cell.ColumnIndex == 1)?.Content?.Trim();

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!pairs.TryGetValue(key, out var existing) || value.Length > existing.Length)
                {
                    pairs[key] = value;
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// Matched on the key's stem, so "Site address" finds "Site address (if different)". An exact
    /// match wins over a stem match, so a short key cannot shadow a longer one starting with it.
    /// </summary>
    public static string? FindValue(Dictionary<string, string> pairs, string key)
    {
        if (pairs.TryGetValue(key, out var exact))
        {
            return exact;
        }

        var candidates = pairs
            .Where(pair => pair.Key.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key.Length)
            .ToList();

        return candidates.Count > 0 ? candidates[0].Value : null;
    }

    private static string Normalise(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            content.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }
}
