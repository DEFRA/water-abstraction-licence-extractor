using UglyToad.PdfPig.Content;

namespace WALE.ProcessFile.Services.Models.PdfPig;

/// <summary>
/// A word.
/// </summary>
public class DeserialisableWord
{
    /// <summary>
    /// The letters contained in the word.
    /// </summary>
    public IReadOnlyList<DeserialisableLetter>? Letters { get; set; }
    
    public Word ToPdfPigWord()
    {
        return new Word(
            Letters!.Select(letter => letter.ToPdfPigLetter()).ToList());
    }
}