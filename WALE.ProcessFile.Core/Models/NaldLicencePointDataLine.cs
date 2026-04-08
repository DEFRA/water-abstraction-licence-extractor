namespace WALE.ProcessFile.Core.Models;

public class NaldLicencePointDataLine
{
    // Derived props
    public string PurposeIdLookupKey => $"{FgacRegionCode}|{AabpId}";
    public int PointId => AaipId;
    public int PurposeId => AabpId;
    
    // Properties from NALD_ABS_PURP_POINTS table
    public required int AabpId { get; set; }
    public required int AaipId { get; set; }
    public required short FgacRegionCode { get; set; }

    // Properties from NALD_POINTS table
    public string? Ngr1Sheet { get; set; }
    public string? Ngr1East { get; set; }
    public string? Ngr1North { get; set; }
    public int? Cart1East { get; set; }
    public int? Cart1North { get; set; }
    public string? LocalName { get; set; }
    public string? Ngr2Sheet { get; set; }
    public string? Ngr2East { get; set; }
    public string? Ngr2North { get; set; }
    public int? Cart2East { get; set; }
    public int? Cart2North { get; set; }
    public string? Ngr3Sheet { get; set; }
    public string? Ngr3East { get; set; }
    public string? Ngr3North { get; set; }
    public int? Cart3East { get; set; }
    public int? Cart3North { get; set; }
    public string? Ngr4Sheet { get; set; }
    public string? Ngr4East { get; set; }
    public string? Ngr4North { get; set; }
    public int? Cart4East { get; set; }
    public int? Cart4North { get; set; }
    public string? AapcCode { get; set; }
    public string? AaptAptpCode { get; set; }
    public string? AaptAptsCode { get; set; }
}