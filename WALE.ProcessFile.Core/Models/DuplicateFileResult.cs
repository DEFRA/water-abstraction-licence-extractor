namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents the result of duplicate file detection
/// </summary>
public class DuplicateFileResult
{
    /// <summary>
    /// The permit number associated with the files
    /// </summary>
    public string PermitNumber { get; set; } = string.Empty;

    /// <summary>
    /// URL of the original file (Priority 4 match)
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// Name of the original file (Priority 4 match)
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// URL of the duplicate file
    /// </summary>
    public string DuplicateFileUrl { get; set; } = string.Empty;

    /// <summary>
    /// Name of the duplicate file
    /// </summary>
    public string DuplicateFileName { get; set; } = string.Empty;

    /// <summary>
    /// Type/description of the duplicate pattern
    /// </summary>
    public string DuplicateType { get; set; } = string.Empty;
}