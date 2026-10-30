namespace WALE.ProcessFile.Core.Models.Dms;

/// <summary>
/// Container for various lookup dictionaries to optimize DMS record searching
/// </summary>
public class DmsLookupIndexes
{
    public Dictionary<string, List<DmsExtract>> ByPermitNumber { get; set; }
        = new();
    public Dictionary<string, List<DmsExtract>> ByManualFixPermitNumber { get; set; }
        = new();
}