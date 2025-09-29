using WALE.ProcessFile.Services.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Models;

public class ListRow
{
    public string? imagePath { get; set; }
    public string? filename{ get; set; }
    public string? licenceNumber{ get; set; }
    public string? licenceHolder{ get; set; }
    public string?[]? purposes { get; set; }
    public string?[]? points { get; set; }
    public int limitsCount { get; set; }
    public int aggregatesCount { get; set; }
    public bool ocr { get; set; }
    public string? issueDate { get; set; }
    public string? issuer { get; set; }
    public bool meansFound { get; set; }
    public LinkedLicence[]? linkedLicences { get; set; }
    public ListRowLicenceSet[]? licenceSets { get; set; }
}