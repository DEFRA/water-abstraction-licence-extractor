namespace WALE.ProcessFile.Core.Models.Dms;

/// <summary>
/// Represents manual fix data for permit numbers that couldn't be automatically matched
/// </summary>
public class DmsManualFixExtract
{
    /// <summary>
    /// The DMS permit number to match
    /// </summary>
    public string PermitNumber { get; set; } = string.Empty;

    /// <summary>
    /// The folder name where the permit documents should be found
    /// </summary>
    public string PermitNumberFolder { get; set; } = string.Empty;
}