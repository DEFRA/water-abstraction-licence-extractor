namespace WALE.ProcessFile.Core.Models;

/// A value read out of a table cell, with that cell's region so a match built from it can be
/// outlined in the portal. Carries the text separately from the cell rather than the cell itself,
/// because a reader normalises and sometimes blanks what it returns, so the text a reader yields is
/// not always the cell's own content. Coordinates are null where the extractor reported no
/// rectangle.
public record DocumentTableValue(string Text, DocumentLineWordCoordinates? Coordinates)
{
    public static DocumentTableValue From(DocumentTableCell? cell, string text) =>
        new(text, cell?.ToCoordinates());
}
