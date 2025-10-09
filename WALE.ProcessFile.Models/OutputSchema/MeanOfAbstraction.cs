using WALE.ProcessFile.Models.Enums.OutputSchema;

namespace WALE.ProcessFile.Models.OutputSchema;

public class MeanOfAbstraction
{
    public string? Id { get; set; }
    
    public string? Description { get; set; }
    
    public AbstractionLimit? AbstractionLimit { get; set; }
    
    public static MeanOfAbstraction Template => new()
    {
        Description = string.Empty,
        Id = "(1)",
        AbstractionLimit = new()
        {
            ImplicitLimit = false,
            PeriodType = LimitPeriodType.PerYear,
            Points = [
                new()
                {
                    Description = string.Empty,
                    Id = string.Empty
                }
            ],
            Purposes = [
                new()
                {
                    Description = string.Empty,
                    Id = string.Empty
                }
            ],
            Units = string.Empty,
            Value = 0
        }
    };
}