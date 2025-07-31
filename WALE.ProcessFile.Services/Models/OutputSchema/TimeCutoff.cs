using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class TimeCutoff
{
    public CutoffType CutoffType { get; set; }

    public DateTime? Date { get; set; }
}