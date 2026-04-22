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
        var currentLinkedLicences = (listRow.linkedLicences ?? [])
            .Where(x => x.ContainedIn != null &&
                        x.ContainedIn.Any(c => c.Direction == LinkedLicenceDirection.Outgoing))
            .ToArray();

        if (string.IsNullOrEmpty(verification.LicenceSectionScrapedValue))
        {
            return currentLinkedLicences is { Length: > 0 };
        }

        try
        {
            var scrapedLicences =
                JsonSerializer.Deserialize<LinkedLicence[]>(verification.LicenceSectionScrapedValue,
                    JsonHelper.GetSerializerOptions()) ?? Array.Empty<LinkedLicence>();

            if (currentLinkedLicences.Length != scrapedLicences.Length)
            {
                return true;
            }

            var currentLicenceNumbers = currentLinkedLicences
                .Select(x => x.LicenceNumber)
                .Where(x => x != null)
                .OrderBy(x => x)
                .ToList();

            var scrapedLicenceNumbers = scrapedLicences
                .Select(x => x.LicenceNumber)
                .Where(x => x != null)
                .OrderBy(x => x)
                .ToList();

            return currentLicenceNumbers.Count != scrapedLicenceNumbers.Count ||
                   currentLicenceNumbers.Where((t, i) => t != scrapedLicenceNumbers[i]).Any();
        }
        catch
        {
            return true;
        }
    }
}