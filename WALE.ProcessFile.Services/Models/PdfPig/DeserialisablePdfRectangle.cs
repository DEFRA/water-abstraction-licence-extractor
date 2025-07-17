using UglyToad.PdfPig.Core;

namespace WALE.ProcessFile.Services.Models.PdfPig;

public class DeserialisablePdfRectangle
{
    /// <summary>
    /// Top right point of the rectangle.
    /// </summary>
    public DeserialisablePdfPoint? TopRight { get; set; }

    /// <summary>
    /// Bottom left point of the rectangle.
    /// </summary>
    public DeserialisablePdfPoint? BottomLeft { get; set; }
    
    public PdfRectangle ToPdfRectangle()
    {
        return new PdfRectangle(BottomLeft!.ToPdfPoint(), TopRight!.ToPdfPoint());
    }
}