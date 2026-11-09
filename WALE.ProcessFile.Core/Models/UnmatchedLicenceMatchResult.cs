namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents the result of license file matching process
/// </summary>
public class UnmatchedLicenceMatchResult
{
    /// <summary>
    /// The permit number from NALD extract
    /// </summary>
    public string PermitNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// The file url that was identified as a licence
    /// </summary>
    public string OriginalFileUrlIdentifiedAsLicence { get; set; } = string.Empty;

    /// <summary>
    /// The URL of the matched file from DMS extract
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// The license number from NALD that was processed
    /// </summary>
    public string LicenseNumber { get; set; } = string.Empty;

    /// <summary>
    /// The latest signature date from NALD metadata file
    /// </summary>
    public string? SignatureDateOfFileEvaluated { get; set; } = string.Empty;
    
    /// <summary>
    /// Disclosure Status from DMS extract
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// File evaluated to find match
    /// </summary>
    public string? FileEvaluated { get; set; }

    /// <summary>
    /// File Type evaluated to find match
    /// </summary>
    public string? FileTypeEvaluated { get; set; }

    /// <summary>
    /// File identified as a licence
    /// </summary>
    public bool FileDeterminedAsLicence { get; set; }

    /// <summary>
    /// NALD ID
    /// </summary>
    public int NaldId { get; set; }
    
    /// <summary>
    /// NALD Issue No
    /// </summary>
    public int NaldIssueNo { get; set; }
    
    /// <summary>
    /// NALD Incrment No
    /// </summary>
    public int? NaldIncrementNo { get; set; }

    /// <summary>
    /// File identified as a licence
    /// </summary>
    public bool NaldDataQualityIssue { get; set; }

    /// <summary>
    /// Number of licences identified for permit number
    /// </summary>
    public int LicenceCount { get; set; }
    
    /// <summary>
    /// Date of issue of evaluated file
    /// </summary>
    public string? DateOfIssueOfEvaluatedFile { get; set; } = string.Empty;
    
    /// <summary>
    /// File Id
    /// </summary>   
    public string FileId { get; set; } = string.Empty;
    
    /// <summary>
    /// File Id status
    /// </summary>   
    public string? FileIdStatus { get; set; }

    /// <summary>
    /// File Id status change date
    /// </summary>
    public string? FileIdStatusChangeDate { get; set; }
    
    /// <summary>
    /// Is water company
    /// </summary>
    public bool? IsWaterCompany { get; set; }
}