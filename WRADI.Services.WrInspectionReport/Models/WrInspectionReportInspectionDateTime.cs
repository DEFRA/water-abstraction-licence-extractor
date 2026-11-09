namespace WRADI.DocumentType.WrInspectionReport.Models;

public class WrInspectionReportInspectionDateTime
{
    public DateTime? DateTime { get; set; }
    
    public string? RawDate { get; set; }
    
    public string? RawTime { get; set; }

    // v3 prints a start and an end time where v1 printed one. RawTime holds the start.
    public string? RawEndTime { get; set; }
}