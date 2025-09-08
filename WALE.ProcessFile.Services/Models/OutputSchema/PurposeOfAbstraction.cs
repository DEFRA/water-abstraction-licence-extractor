namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PurposeOfAbstraction : Purpose
{
    public string? NaldId { get; set; }

    public string[] PointIds { get; set; } = [];
    
    public static PurposeOfAbstraction Template => new()
    {
        Description = string.Empty,
        Id = string.Empty,
        NaldId = null,
        PointIds = [
            "2.1"
        ]
    };
}