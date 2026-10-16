using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

/// <summary>
/// Reads a table that names its columns in a header row, returning the body rows keyed by column
/// name. For the grids that hold a list of records rather than label/value pairs, which
/// <see cref="KeyValueTableHelper"/> cannot read because it only considers two-column tables.
/// </summary>
public static class HeaderedTableHelper
{
    /// <summary>
    /// Body rows of the best copy of the table whose header row carries every name in
    /// <paramref name="headers"/>.
    ///
    /// Rows holding nothing but blanks and <paramref name="placeholders"/> are dropped, so an
    /// unfilled form row does not become a record. Values matching a placeholder are returned
    /// empty.
    /// </summary>
    public static List<Dictionary<string, string>> ReadRows(
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

        var result = new List<Dictionary<string, string>>();

        foreach (var row in rows.Skip(1))
        {
            var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (header, columnIndex) in columnOfHeader)
            {
                var value = Normalise(
                    row.FirstOrDefault(cell => cell.ColumnIndex == columnIndex)?.Content);

                record[header] = IsPlaceholder(value, placeholders) ? string.Empty : value;
            }

            if (record.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                result.Add(record);
            }
        }

        return result;
    }

    /// <summary>
    /// Extractors emit the same grid more than once at different granularities. Requiring the
    /// whole header text is what separates them: where a column name wraps, the usable copy holds
    /// it whole in one cell while the fragmented copy has split it across rows and keeps only the
    /// first part. Fewest rows breaks any remaining tie, a fragmented copy always having more.
    /// </summary>
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
