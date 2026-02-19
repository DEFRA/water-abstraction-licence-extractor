namespace WALE.ProcessFile.Core.Models;

public class CartesianReference
{
    public int ReferenceIndex { get; init; }
    public int? East { get; init; }
    public int? North { get; init; }
    
    public override string ToString()
    {
        return $"{East} {North}";
    }
}
