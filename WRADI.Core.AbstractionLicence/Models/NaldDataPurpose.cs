namespace WRADI.Core.AbstractionLicence.Models;

public class NaldDataPurpose
{
    public int Id { get; init; }
    public required NaldDataPurposeCategoryUse CategoryUse { get; init; }
    public required NaldDataQuantity Quantity { get; init; }
    public string? Notes { get; init; }
    public List<int> PointIds { get; init; } = [];

    public override string ToString()
    {
        return $"{Id}|{CategoryUse.Code}";
    }
}