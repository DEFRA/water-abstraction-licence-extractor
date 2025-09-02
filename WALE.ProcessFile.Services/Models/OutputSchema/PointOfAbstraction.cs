namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PointOfAbstraction : Point
{
    public string? NaldId { get; set; }

    public string[] PurposeIds { get; init; } = [];
}