using System.Text.Json;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Services.Strategies;

public class LinkedLicencesScrapedDataComparisonStrategy : IScrapedDataComparisonStrategy
{
    public string SectionName => "Linked Licences";

    public bool ScrapedDataIsDifferent(LicenceSectionVerification verification, OutputListDataItem listRow)
    {
        // Only consider outgoing links
        var wasScrapedThisRun = (listRow.linkedLicences ?? [])
            .Any(x => x.LicenceNumber == verification.LicenceSectionItemId
                      && x.ContainedIn != null
                      && x.ContainedIn.Any(c => c.Direction == LinkedLicenceDirection.Outgoing));

        return verification.VerificationType switch
        {
            "Confirmed" or "AutoPass" or "Removed" or "Edited" => !wasScrapedThisRun,
            "Added" => wasScrapedThisRun,
            _ => false
        };
    }
}