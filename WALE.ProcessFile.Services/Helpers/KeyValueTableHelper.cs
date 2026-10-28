using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

/// Extractors report the same region at more than one granularity - a v3 permit yields both a 7x2
/// and an 18x2 table from the same key, and the copies disagree, one holding a wrapped value whole
/// where the other truncates it. Hence longest-value-wins throughout.
public static class KeyValueTableHelper
{
    /// Only two-column tables contribute.
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
                    pairs[key] = DocumentTableValue.From(cell, value, table.PageNumber);
                }
            }
        }

        return pairs;
    }

    /// Matched on the key's stem, so "Site address" finds "Site address (if different)". An exact
    /// match wins, so a short key cannot shadow a longer one starting with it.
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

        if (candidates.Count > 0)
        {
            return candidates[0].Value;
        }

        // "(s)" can sit mid-key - "Point(s) of abstraction" against "Point of abstraction" - so no
        // single stem reaches both forms.
        var singular = WithoutPluralMarker(key);

        return pairs
            .Where(pair => WithoutPluralMarker(pair.Key)
                .StartsWith(singular, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key.Length)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private static string WithoutPluralMarker(string key) =>
        key.Replace("(s)", string.Empty, StringComparison.OrdinalIgnoreCase);

    /// A new group each time <paramref name="firstKeyOfGroup"/> is seen again, in document order.
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

                current.TryAdd(key, DocumentTableValue.From(cell, value, table.PageNumber));
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
