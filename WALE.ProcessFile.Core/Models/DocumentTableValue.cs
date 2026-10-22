namespace WALE.ProcessFile.Core.Models;

/// Not the cell itself: a reader normalises and sometimes blanks what it returns, so the text is
/// not always the cell's own. Not the region alone either - an overlay buckets by page.
public record DocumentTableValue(
    string Text,
    DocumentLineWordCoordinates? Coordinates,
    int PageNumber = 0)
{
    public static DocumentTableValue From(DocumentTableCell? cell, string text, int pageNumber) =>
        new(text, cell?.ToCoordinates(), pageNumber);
}
