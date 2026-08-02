namespace WRADI.Core.AbstractionLicence.Models;

/// <summary>
/// Represents the result of license file matching process
/// </summary>
public class LicenceFinderResult
{
    /// <summary>
    /// The permit number from NALD extract
    /// </summary>
    public string PermitNumber { get; set; } = string.Empty;

    /// <summary>
    /// The DMS permit number folder
    /// </summary>
    public string DmsPermitNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// The URL of the matched file from DMS extract
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// The name of the rule used to find the match
    /// </summary>
    public string RuleUsed { get; set; } = string.Empty;

    /// <summary>
    /// Whether the file is affected by change audit
    /// </summary>
    public string ChangeAuditAction { get; set; } = string.Empty;

    /// <summary>
    /// The license number from NALD that was processed
    /// </summary>
    public string LicenseNumber { get; set; } = string.Empty;

    /// <summary>
    /// The latest document date from DMS extract
    /// </summary>
    public string? DocumentDate { get; set; } = string.Empty;

    /// <summary>
    /// The latest signature date from NALD metadata file
    /// </summary>
    public string? SignatureDate { get; set; } = string.Empty;

    /// <summary>
    /// The date of issue from file reader extract
    /// </summary>
    public string? DateOfIssue { get; set; } = string.Empty;

    /// <summary>
    /// Other reference from DMS extract
    /// </summary>
    public string OtherReference { get; set; } = string.Empty;

    /// <summary>
    /// File Size from DMS extract
    /// </summary>
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    /// Disclosure Status from DMS extract
    /// </summary>
    public string DisclosureStatus { get; set; } = string.Empty;

    /// <summary>
    /// Disclosure Status from DMS extract
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// NALD ID
    /// </summary>
    public int NaldId { get; set; }
    
    /// <summary>
    /// NALD Issue No
    /// </summary>
    public int NaldIssueNo { get; set; }
    
    /// <summary>
    /// NALD Increment No
    /// </summary>
    public int? NaldIncrementNo { get; set; }
    
    /// <summary>
    /// Primary Template
    /// </summary>
    public string? PrimaryTemplate { get; set; }
    
    /// <summary>
    /// Secondary Template
    /// </summary>
    public string? SecondaryTemplate { get; set; }
    
    /// <summary>
    /// Number Of Pages Template
    /// </summary>
    public int? NumberOfPages{ get; set; }
    
    /// <summary>
    /// If DOI matches signature date
    /// </summary>
    public bool DoiSignatureDateMatch{ get; set; }
    
    /// <summary>
    /// If licence is handled in version match
    /// </summary>
    public bool IncludedInVersionMatch{ get; set; }
    
    /// <summary>
    /// If single licence identified in version match
    /// </summary>
    public bool? SingleLicenceInVersionMatch{ get; set; }

    /// <summary>
    /// The URL of the matched file from versionMatch
    /// </summary>
    public string? VersionMatchFileUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// If duplicate Licences are found in version Match results
    /// </summary>
    public bool? DuplicateLicenceInVersionMatchResult{ get; set; }
    
    /// <summary>
    /// NALD Issue in version Match results
    /// </summary>
    public bool? NaldIssue{ get; set; }
    
    /// <summary>
    /// File Id
    /// </summary>   
    public string? FileId { get; set; }
    
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
    
    /// <summary>
    /// Has folder name been autocorrected
    /// </summary>
    public bool? FolderNameAutoCorrect { get; set; }
    
    public bool SeenInDmsExtract { get; set; }
    
    public bool? WeHaveDownloaded { get; set; }
}