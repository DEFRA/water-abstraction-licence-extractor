using UglyToad.PdfPig.Core;

namespace WALE.ProcessFile.Services.PdfPig.Models;

/// <summary>
/// A word.
/// </summary>
public class MinimalPdfRectangle
{
    public double Top { get; set; }
    
    public double Right { get; set; }
    
    public double Bottom { get; set; }

    public double Left { get; set; }
    
    public double CentroidX { get; set; }
    
    public static MinimalPdfRectangle FromPdfPigPdfRectangel(PdfRectangle pdfPigRectangle)
    {
        return new MinimalPdfRectangle
        {
            Top = pdfPigRectangle.Top,
            Right = pdfPigRectangle.Right,
            Bottom = pdfPigRectangle.Bottom,
            Left = pdfPigRectangle.Left,
            CentroidX = pdfPigRectangle.Centroid.X
        };
    }
}