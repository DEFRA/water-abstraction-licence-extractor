namespace WALE.ProcessFile.Core.Models.Nald;

/// <summary>
/// Represents NALD (National Abstraction Licensing Database) extract data that can be extracted from CSV files.
/// </summary>
public class NaldSimpleRecord
{
    /// <summary>
    /// License number
    /// </summary>
    public string LicNo { get; init; } = string.Empty;
    
    /// <summary>
    /// Permit number
    /// </summary>
    public string DmsPermitNo { get; set; } = string.Empty;

    /// <summary>
    /// Cross registration indicator
    /// </summary>
    public string Region { get; init; } = string.Empty;
}