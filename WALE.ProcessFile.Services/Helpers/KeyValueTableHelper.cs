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
    public static Dictionary<string, DocumentTableValue> ReadPairs(IReadOnlyList<DocumentTable> tables)
    {
        var pairs = new Dictionary<string, DocumentTableValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables.Where(table => table.ColumnCount == 2))
        {
            foreach (var row in table.Cells.GroupBy(cell => cell.RowIndex))
            {
                var key = Normalise(row.FirstOrDefault(cell => cell.ColumnIndex == 0)?.Content);
                var cell = row.FirstOrDefault(cell => cell.ColumnIndex == 1);
                var value = cell?.Content?.Trim();

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!pairs.TryGetValue(key, out var existing) || value.Length > existing.Text.Length)
                {
                    pairs[key] = DocumentTableValue.From(cell, value);
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// Matched on the key's stem, so "Site address" finds "Site address (if different)". An exact
    /// match wins over a stem match, so a short key cannot shadow a longer one starting with it.
    /// </summary>
    public static DocumentTableValue? FindValue(Dictionary<string, DocumentTableValue> pairs, string key)
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

    /// <summary>
    /// One set of pairs per occurrence, a new group starting each time
    /// <paramref name="firstKeyOfGroup"/> is seen again. Rows are taken in document order.
    /// </summary>
    public static List<Dictionary<string, DocumentTableValue>> ReadRepeatingGroups(
        IReadOnlyList<DocumentTable> tables,
        string firstKeyOfGroup)
    {
        var groups = new List<Dictionary<string, DocumentTableValue>>();
        Dictionary<string, DocumentTableValue>? current = null;

        foreach (var table in tables.Where(table => table.ColumnCount == 2))
        {
            foreach (var row in table.Cells.GroupBy(cell => cell.RowIndex).OrderBy(row => row.Key))
            {
                var key = Normalise(row.FirstOrDefault(cell => cell.ColumnIndex == 0)?.Content);
                var cell = row.FirstOrDefault(cell => cell.ColumnIndex == 1);
                var value = cell?.Content?.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (key.StartsWith(firstKeyOfGroup, StringComparison.OrdinalIgnoreCase))
                {
                    current = new Dictionary<string, DocumentTableValue>(StringComparer.OrdinalIgnoreCase);
                    groups.Add(current);
                }

                if (current == null || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                current.TryAdd(key, DocumentTableValue.From(cell, value));
            }
        }

        // Overlapping tables yield each group more than once (5 boreholes came out as 10 records),
        // and the copies disagree, so deduplicating on identical content does not catch them.
        return MergeByGroupKey(groups, firstKeyOfGroup);
    }

    private static List<Dictionary<string, DocumentTableValue>> MergeByGroupKey(
        List<Dictionary<string, DocumentTableValue>> groups,
        string firstKeyOfGroup)
    {
        var merged = new List<Dictionary<string, DocumentTableValue>>();

        var byIdentity = new Dictionary<string, Dictionary<string, DocumentTableValue>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var identity = FindValue(group, firstKeyOfGroup)?.Text;

            if (string.IsNullOrWhiteSpace(identity))
            {
                merged.Add(group);
                continue;
            }

            if (!byIdentity.TryGetValue(identity, out var existing))
            {
                byIdentity[identity] = group;
                merged.Add(group);
                continue;
            }

            foreach (var (key, value) in group)
            {
                if (!existing.TryGetValue(key, out var held) || value.Text.Length > held.Text.Length)
                {
                    existing[key] = value;
                }
            }
        }

        return merged;
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
