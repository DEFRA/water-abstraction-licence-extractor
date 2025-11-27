using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class TimeCutoff
{
    public CutoffType? CutoffType { get; set; }

    public string? Date { get; set; }
}