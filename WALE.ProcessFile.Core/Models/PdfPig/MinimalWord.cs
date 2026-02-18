using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace WALE.ProcessFile.Core.Models.PdfPig;

/// <summary>
/// A word.
/// </summary>
public class MinimalWord
{
    public string Text { get; set; } = null!;

    public MinimalPdfRectangle BoundingBox { get; set; } = null!;

    public static MinimalWord FromPdfPigWord(Word pdfPigWord)
    {
        return new MinimalWord
        {
            Text = pdfPigWord.Text,
            BoundingBox = MinimalPdfRectangle.FromPdfPigPdfRectangel(pdfPigWord.BoundingBox)
        };
    }
}