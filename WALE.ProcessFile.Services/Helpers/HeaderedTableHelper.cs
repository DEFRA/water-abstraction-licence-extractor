using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

/// For grids holding a list of records rather than label/value pairs, which
/// <see cref="KeyValueTableHelper"/> cannot read: it only considers two-column tables.
public static class HeaderedTableHelper
{
    /// Rows holding nothing but blanks and <paramref name="placeholders"/> are dropped, so an
    /// unfilled form row does not become a record. A value matching a placeholder returns empty.
    public static List<Dictionary<string, DocumentTableValue>> ReadRows(
        IReadOnlyList<DocumentTable> tables,
        IReadOnlyList<string> headers,
        IReadOnlyCollection<string>? placeholders = null)
    {
        var table = SelectTable(tables, headers);

        if (table == null)
        {
            return [];
        }

        var rows = table.Cells
            .GroupBy(cell => cell.RowIndex)
            .OrderBy(row => row.Key)
            .ToList();

        var headerRow = rows[0];
        var columnOfHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var cell = headerRow.FirstOrDefault(c => Matches(c.Content, header));

            if (cell != null)
            {
                columnOfHeader[header] = cell.ColumnIndex;
            }
        }

        var result = new List<Dictionary<string, DocumentTableValue>>();

        foreach (var row in rows.Skip(1))
        {
            var record = new Dictionary<string, DocumentTableValue>(StringComparer.OrdinalIgnoreCase);

            foreach (var (header, columnIndex) in columnOfHeader)
            {
                var cell = row.FirstOrDefault(cell => cell.ColumnIndex == columnIndex);
                var value = Normalise(cell?.Content);

                record[header] = DocumentTableValue.From(
                    cell,
                    IsPlaceholder(value, placeholders) ? string.Empty : value,
                    table.PageNumber);
            }

            if (record.Values.Any(value => !string.IsNullOrWhiteSpace(value.Text)))
            {
                result.Add(record);
            }
        }

        return result;
    }

    /// Requiring the whole header text is what separates the duplicate copies of a grid: where a
    /// column name wraps, the usable copy holds it whole in one cell while the fragmented copy
    /// keeps only the first part. Fewest rows breaks any tie, a fragmented copy always having more.
    private static DocumentTable? SelectTable(
        IReadOnlyList<DocumentTable> tables,
        IReadOnlyList<string> headers) =>
        tables
            .Where(table => table.Cells.Count > 0)
            .Where(table => HeaderRowCarriesAll(table, headers))
            .OrderBy(table => table.Cells.Select(cell => cell.RowIndex).Distinct().Count())
            .FirstOrDefault();

    private static bool HeaderRowCarriesAll(DocumentTable table, IReadOnlyList<string> headers)
    {
        var firstRowIndex = table.Cells.Min(cell => cell.RowIndex);

        var headerCells = table.Cells
            .Where(cell => cell.RowIndex == firstRowIndex)
            .ToList();

        return headers.All(header => headerCells.Any(cell => Matches(cell.Content, header)));
    }

    private static bool Matches(string? content, string header) =>
        Normalise(content).Equals(header, StringComparison.OrdinalIgnoreCase);

    private static bool IsPlaceholder(string value, IReadOnlyCollection<string>? placeholders) =>
        placeholders != null
        && placeholders.Any(placeholder => value.Equals(placeholder, StringComparison.OrdinalIgnoreCase));

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
