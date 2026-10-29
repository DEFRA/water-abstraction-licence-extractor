using System.Collections.Concurrent;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Services;

public static class TemplateTypeIdentifierServices
{
    /// The constructor builds a rule engine, so one instance per region, not per document.
    private static readonly ConcurrentDictionary<int, TemplateTypeIdentifierService> ByRegionCode = new();

    /// NALD FGAC region codes, duplicated from GenerateLicenceReaderExtract. The names are not free
    /// text - rules look up the label group "{Region}SplitLabels", and only NE, Anglian and NW have
    /// one, so the other five regions can never match Divisional.
    private static readonly Dictionary<int, string> RegionNames = new()
    {
        [1] = "Anglian",
        [2] = "Midlands",
        [3] = "NE",
        [4] = "NW",
        [5] = "South West",
        [6] = "Southern",
        [7] = "Thames",
        [8] = "Wales"
    };

    /// An unmapped code resolves to "Unknown", so "UnknownSplitLabels" never exists and no
    /// Divisional or split rule can fire for that file.
    public static TemplateTypeIdentifierService ForRegionCode(int fgacRegionCode)
    {
        return ByRegionCode.GetOrAdd(
            fgacRegionCode,
            code => new TemplateTypeIdentifierService(RegionNames.GetValueOrDefault(code, "Unknown")));
    }
}
