namespace WALE.ProcessFile.Core.Models;

public class NaldDataPurposeCategoryUse
{
    public string Code => $"{PrimaryCategoryCode}-{SecondaryCategoryCode}-{UseCode}";
    public required string PrimaryCategoryCode { get; init; }
    public required string PrimaryCategoryDescription { get; init; }
    public required string SecondaryCategoryCode { get; init; }
    public required string SecondaryCategoryDescription { get; init; }
    public required int UseCode { get; init; }
    public required string UseDescription { get; init; }
}