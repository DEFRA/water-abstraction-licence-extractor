using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace WALE.ProcessFile.Services.Models.PdfPig;

/// <summary>
/// A block of text.
/// </summary>
public class DeserialisableTextBlock
{
    /// <summary>
    /// The text lines contained in the block.
    /// </summary>
    public IReadOnlyList<DeserialisableTextLine>? TextLines { get; set; }
    
    public TextBlock ToPdfPigTextBlock()
    {
        return new TextBlock(
            TextLines?.Select(textLine => textLine.ToPdfPigTextLine()).ToList());
    }    
}