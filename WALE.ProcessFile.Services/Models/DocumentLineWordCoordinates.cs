namespace WALE.ProcessFile.Services.Models;

public class DocumentLineWordCoordinates(double top, double right, double bottom, double left)
{
    public double Top { get; set; } = top;

    public double Right { get; set; } = right;

    public double Bottom { get; set; } = bottom;

    public double Left { get; set; } = left;
}