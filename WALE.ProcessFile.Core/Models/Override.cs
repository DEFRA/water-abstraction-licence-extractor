namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents a change audit record for document path tracking
/// </summary>
public class Override
{
    /// <summary>
    /// Permit number associated with the document
    /// </summary>
    public string PermitNumber { get; set; } = string.Empty;

    public string LicenceReference { get; set; } = string.Empty;
    
    /// <summary>
    /// Overriden file Path
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// NALD Issue Number
    /// </summary>
    public string IssueNo { get; set; } = string.Empty;
    
    /// <summary>
    /// NALD Increment Number
    /// </summary>
    public int? IncrementNo { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    public string OverrideType { get; set; } = string.Empty;
    
    /// <summary>
    /// 
    /// </summary>
    public string FileId { get; set; } = string.Empty;
}