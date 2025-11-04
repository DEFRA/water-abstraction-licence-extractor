using WALE.ProcessFile.Models.Enums.OutputSchema;

namespace WALE.ProcessFile.Models.OutputSchema;

public class TimeCutoff
{
    public CutoffType? CutoffType { get; set; }

    public string? Date { get; set; }
}