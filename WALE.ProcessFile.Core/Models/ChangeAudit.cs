namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents a change audit record for document path tracking
/// </summary>
public class ChangeAudit
{
    /// <summary>
    /// Permit number associated with the document
    /// </summary>
    public string PermitNumber { get; set; } = string.Empty;

    /// <summary>
    /// Original path associated with the document
    /// </summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>
    /// Updated path associated with the document
    /// </summary>
    public string UpdatedPath { get; set; } = string.Empty;

    /// <summary>
    /// Action performed on the document
    /// </summary>
    public string Action { get; set; } = string.Empty;
}