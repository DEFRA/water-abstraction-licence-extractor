using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models;

public class DocumentLineWordCoordinates(double top, double right, double bottom, double left)
{
    [JsonConstructor]
    public DocumentLineWordCoordinates() : this(-2, -2, -2, -2) { }

    public double Top { get; set; } = top;

    public double Right { get; set; } = right;

    public double Bottom { get; set; } = bottom;

    public double Left { get; set; } = left;

    public static DocumentLineWordCoordinates NotKnown()
    {
        return new DocumentLineWordCoordinates(-3, -3, -3, -3);
    }
}