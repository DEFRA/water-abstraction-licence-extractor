namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class NaldPointData
{
    public string? Id { get; set; }
    
    public string? PrimaryType { get; set; }
    
    public string? SecondaryType { get; set; }
    
    public string? Name { get; set; }
    
    public string? Category { get; set; }
    
    // ReSharper disable once InconsistentNaming
    public NaldPointNgr? NGR { get; set; }
    
    // ReSharper disable once InconsistentNaming
    public NaldPointNgrCartesian? NGRCartesian { get; set; }
}