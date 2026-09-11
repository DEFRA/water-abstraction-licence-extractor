namespace WALE.ProcessFile.Core.Models;

public class ProcessRun
{
    public int ProcessRunId { get; set; }
    
    public string? Description { get; set; }
    
    public DateTime? StartDateTimeUtc { get; set; }
    
    public DateTime? EndDateTimeUtc { get; set; }
    
    public int NumberOfFiles { get; set; }
    
    public int SuccessCount { get; set; }

    public string? Status { get; set; }

    public int NumberOfFilesNotFound =>  NumberOfFiles - SuccessCount;

    // Defaults to AbstractionLicence for backward compatibility with rows written before this
    // column existed - see 069_AddDocumentTypeColumnToProcessRun and the wr51_portal_bff_analysis
    // memory ("Decided against one mixed ProcessRun per weekly batch covering both document
    // types - runs should be single-type"). A run covers exactly one document type; a caller
    // processing multiple types runs the orchestration cycle once per type instead.
    public string DocumentType { get; set; } = "AbstractionLicence";
}