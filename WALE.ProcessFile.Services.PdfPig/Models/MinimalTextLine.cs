using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace WALE.ProcessFile.Services.PdfPig.Models;

/// <summary>
/// A line of text.
/// </summary>
public class MinimalTextLine
{
    /// <summary>
    /// The words contained in the line.
    /// </summary>
    public IReadOnlyList<MinimalWord> Words { get; set; } = [];
    
    public static MinimalTextLine FromPdfPigTextLine(TextLine pdfPigTextLine)
    {
        return new MinimalTextLine
        {
            Words = pdfPigTextLine.Words.Select(MinimalWord.FromPdfPigWord).ToList()
        };
    }
}