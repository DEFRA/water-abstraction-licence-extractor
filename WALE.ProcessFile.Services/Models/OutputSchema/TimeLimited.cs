using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class TimeLimited
{
    public LimitationType LimitationType { get; set; }

    public DateTime? Date { get; set; }
}