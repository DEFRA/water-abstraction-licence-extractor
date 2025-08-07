using UglyToad.PdfPig.Core;

namespace WALE.ProcessFile.Services.Models;

public class DocumentLineWordCoordinates(double top, double right, double bottom, double left)
{
    public double Top { get; set; } = top;

    public double Right { get; set; } = right;

    public double Bottom { get; set; } = bottom;

    public double Left { get; set; } = left;

    public static DocumentLineWordCoordinates Convert(PdfRectangle rectangle)
    {
        return new DocumentLineWordCoordinates(
            rectangle.Top,
            rectangle.Right,
            rectangle.Bottom,
            rectangle.Left);
    }
}