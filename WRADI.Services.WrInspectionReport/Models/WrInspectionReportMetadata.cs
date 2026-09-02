namespace WRADI.DocumentType.WrInspectionReport.Models;

public class WrInspectionReportMetadata
{
    public string? Filename { get; set; }
    
    public Guid? FileId { get; set; }
    
    public string? DocumentHeader { get; set; }
    
    public string? DocumentTemplateVerison { get; set; }
    
    public bool? IsScan { get; set; }
    
    public string? FormSentTo { get; set; }

    public WrInspectionReportInspectionDate Date { get; set; } = new();
}