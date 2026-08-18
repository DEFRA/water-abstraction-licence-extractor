using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

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
                    DocumentDescription = string.Empty,
                    DocumentId = string.Empty
                }
            ],
            Purposes = [
                new()
                {
                    DocumentDescription = string.Empty,
                    DocumentId = string.Empty
                }
            ],
            Units = string.Empty,
            Value = 0
        }
    };
}