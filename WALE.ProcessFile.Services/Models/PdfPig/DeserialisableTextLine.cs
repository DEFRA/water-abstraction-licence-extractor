using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace WALE.ProcessFile.Services.Models.PdfPig;

/// <summary>
/// A line of text.
/// </summary>
public class DeserialisableTextLine
{
    /// <summary>
    /// The words contained in the line.
    /// </summary>
    public IReadOnlyList<DeserialisableWord>? Words { get; set; }
    
    public TextLine ToPdfPigTextLine()
    {
        return new TextLine(
            Words?.Select(word => word.ToPdfPigWord()).ToList());
    }
}