using UglyToad.PdfPig.Core;

namespace WALE.ProcessFile.Services.Models.PdfPig;

public class DeserialisablePdfPoint
{
    /// <summary>
    /// The X coordinate of this point. (Horizontal axis).
    /// </summary>{
    public double? X { get; set; }

    /// <summary>
    /// The Y coordinate of this point. (Vertical axis).
    /// </summary>
    public double? Y { get; set; }
    
    public PdfPoint ToPdfPoint()
    {
        return new PdfPoint(X!.Value, Y!.Value);
    }
}