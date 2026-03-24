namespace WALE.Tools.Models;

public class OverrideOldFormat
{
    /// <summary>
    /// Permit number associated with the document
    /// </summary>
    public string PermitNumber { get; set; } = string.Empty;

    /// <summary>
    /// Overriden file Path
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// NALD Issue Number
    /// </summary>
    public string IssueNo { get; set; } = string.Empty;
    
    /// <summary>
    /// 
    /// </summary>
    public string FileId { get; set; } = string.Empty;
}