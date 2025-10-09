using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Models;

public class ExpressionResult
{
    public bool Continue { get; set; }
    public bool Return { get; set; }
    public bool ContinuePartialLoop { get; set; }
    public bool Break { get; set; }
    public DocumentLine? NewPartialLine { get; set; }
    public List<LabelGroupResult> Results { get; set; } = [];
}