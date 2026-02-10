namespace WALE.ProcessFile.Core.Models;

public class NaldLicencePointDataLine
{
    public string? AabpId { get; set; }

    public string? AaipId { get; set; }

    public string? AmoaCode { get; set; }

    public string? Notes { get; set; }

    public string? FgacRegionCode { get; set; }

    // Properties from NALD_POINTS table
    public string? Ngr1Sheet { get; set; }
    public string? Ngr1East { get; set; }
    public string? Ngr1North { get; set; }
    public int? Cart1East { get; set; }
    public int? Cart1North { get; set; }
    public string? LocalName { get; set; }
    public string? AsrcCode { get; set; }
    public string? Disabled { get; set; }
    public string? LocalNameWelsh { get; set; }
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
    public string? AbanCode { get; set; }
    public string? LocationText { get; set; }
    public int? AaddId { get; set; }
    public decimal? Depth { get; set; }
    public string? WrbNo { get; set; }
    public string? BgsNo { get; set; }
    public string? RegWellIndexRef { get; set; }
    public string? HydroRef { get; set; }
    public decimal? HydroInterceptDist { get; set; }
    public decimal? HydroGwOffsetDist { get; set; }
    public string? PointNotes { get; set; }
}
