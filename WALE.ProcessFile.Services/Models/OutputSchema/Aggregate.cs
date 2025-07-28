using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class Aggregate
{
    public string? Id { get; set; }
    
    public string? GroupId { get; set; }
    
    public string? LicenceNumber { get; set; }
    
    public string? LicenceVersionId { get; set; }
    
    public PrimaryType? PrimaryType { get; set; }
    
    public SubType? SubType { get; set; }
    
    public string? NaldType { get; set; }
    
    public TimePeriod? TimePeriod { get; set; }

    public TimeCutoff? TimeCutoff { get; set; }
    
    public Purpose[]? Purposes { get; set; }
    
    public Point[]? Points { get; set; }
    
    public LinkedLicence[]? LinkedLicences { get; set; }
    
    public AbstractionLimit[]? Limits { get; set; }
}