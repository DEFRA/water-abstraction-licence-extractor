namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PurposeOfAbstraction : Purpose
{
    public string? NaldId { get; set; }

    public double[] PointIds { get; set; } = [];
}