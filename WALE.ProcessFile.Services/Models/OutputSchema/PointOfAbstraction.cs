namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PointOfAbstraction : Point
{
    public string? NaldId { get; set; }

    public string[] PurposeIds { get; init; } = [];
    
    public static PointOfAbstraction Empty => new()
    {
        Description = string.Empty,
        Id = string.Empty,
        NaldId = null,
        PurposeIds = [
            "4.1"
        ]
    };
}