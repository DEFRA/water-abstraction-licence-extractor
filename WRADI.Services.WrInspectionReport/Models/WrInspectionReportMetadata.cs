using WRADI.DocumentType.WrInspectionReport.Enums;

namespace WRADI.DocumentType.WrInspectionReport.Models;

public class WrInspectionReportMetadata
{
    public string? Filename { get; set; }

    public Guid? FileId { get; set; }

    // See WrTemplateType and WrInspectionReportSchemaConverter.ClassifyTemplate for how this is
    // derived - from marker label groups matched against the client's own TemplateSpec_v5.0.xlsx,
    // not a separate classification pass over raw document text.
    public WrTemplateType Template { get; set; } = WrTemplateType.Unknown;

    public string? DocumentHeader { get; set; }
    
    public string? DocumentTemplateVerison { get; set; }
    
    public bool? IsScan { get; set; }
    
    public string? FormSentTo { get; set; }

    public WrInspectionReportInspectionDate Date { get; set; } = new();
}