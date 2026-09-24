using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public static class StructuredTableLabelConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels() =>
    [
        ("LicenceSerialNo", GetLicenceSerialNo())
    ];
    
    private static List<LabelToMatch> GetLicenceSerialNo()
    {
        return
        [
            new LabelToMatch
            {
                Name = "LicenceSerialNo",
                Format = "Text",
                Text =
                [
                    new("Licence Serial")
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                LayoutExtractor = LayoutExtractor.TableBased,
                LayoutExtractorTableShape = LayoutExtractorTableShape.Structured
            }
        ];
    }
}