namespace WALE.ProcessFile.Services.Models.OutputSchema.Wr51;

public class Wr51FormMetadata
{
    public string? Filename { get; set; }
    
    public Guid? FileId { get; set; }
    
    public string? DocumentHeader { get; set; }
    
    public string? DocumentTemplateVerison { get; set; }
    
    public bool? IsScan { get; set; }
    
    public string? FormSentTo { get; set; }

    public Wr51FormInspectionDate Date { get; set; } = new();
}