namespace WALE.ProcessFile.Core.Models.Nald;

/// <summary>
/// Represents NALD (National Abstraction Licensing Database) metadata extract data that can be pulled from API.
/// </summary>
public class NaldLicenceVersion
{
    /// <summary>
    /// License number(AABL_ID)
    /// </summary>
    public string LicenceNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// NALD Identifier(AABL_ID)
    /// </summary>
    public string? AablId { get; set; } = string.Empty;
    
    /// <summary>
    /// NALD Issue Number(Issue_No)
    /// </summary>
    public string IssueNo { get; set; } = string.Empty;
    
    /// <summary>
    /// NALD Issue Number(Issue_No)
    /// </summary>
    public int? IncrementNo { get; set; }
    
    /// <summary>
    /// Abbreviation Type(AABV_TYPE)
    /// </summary>
    public string? AabvType { get; set; } = string.Empty;

    /// <summary>
    /// Signature Date(LIC_SIG_DATE)
    /// </summary>
    public DateTime? SignatureDate { get; set; }
    
    /// <summary>
    /// Region
    /// </summary>
    public string Region { get; set; } = string.Empty;
    
    /// <summary>
    /// 
    /// </summary>
    public string? ArepEiucCode { get; set; }
}