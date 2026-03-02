using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models;

public class DocumentLineWordCoordinates(double top, double right, double bottom, double left)
{
    private const double NotKnownCoordinate = -1;
    
    [JsonConstructor]
    public DocumentLineWordCoordinates() : this(
        NotKnownCoordinate,
        NotKnownCoordinate,
        NotKnownCoordinate,
        NotKnownCoordinate) { }

    public double Top { get; set; } = top;

    public double Right { get; set; } = right;

    public double Bottom { get; set; } = bottom;

    public double Left { get; set; } = left;

    public static DocumentLineWordCoordinates NotKnown()
    {
        return new DocumentLineWordCoordinates(
            NotKnownCoordinate,
            NotKnownCoordinate,
            NotKnownCoordinate,
            NotKnownCoordinate);
    }
}