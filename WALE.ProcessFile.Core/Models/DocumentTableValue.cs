namespace WALE.ProcessFile.Core.Models;

/// Carries the text separately from the cell rather than the cell itself: a reader normalises and
/// sometimes blanks what it returns, so its text is not always the cell's own content.
public record DocumentTableValue(string Text, DocumentLineWordCoordinates? Coordinates)
{
    public static DocumentTableValue From(DocumentTableCell? cell, string text) =>
        new(text, cell?.ToCoordinates());
}
