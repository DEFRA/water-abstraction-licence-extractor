namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents the result of delta process
/// </summary>
public class DeltaResult
{
    /// <summary>
    /// The permit number from NALD extract
    /// </summary>
    public string? PermitNumber { get; set; }

    /// <summary>
    /// The URL of the matched file from DMS extract
    /// </summary>
    public string? FileUrl { get; set; }
}