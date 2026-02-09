using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace WALE.ProcessFile.Core.Models.PdfPig;

/// <summary>
/// A block of text.
/// </summary>
public class MinimalTextBlock
{
    /// <summary>
    /// The text lines contained in the block.
    /// </summary>
    public IReadOnlyList<MinimalTextLine> TextLines { get; set; } = [];
    
    public static MinimalTextBlock FromPdfPigTextBlock(TextBlock pdfPigTextBlock)
    {
        return new MinimalTextBlock
        {
            TextLines = pdfPigTextBlock.TextLines.Select(MinimalTextLine.FromPdfPigTextLine).ToList()
        };
    }
}