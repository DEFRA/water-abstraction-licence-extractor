using System.Text.Json;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Services.Strategies;

public class LinkedLicencesMergeOverrideStrategy : IMergeOverrideStrategy
{
    public string SectionName => "Linked Licences";

    public void Merge(LicenceSectionVerification verification, OutputListDataItem listRow)
    {
        if (verification.VerificationType == "Reject")
        {
            return;
        }

        if (verification is { ScrapedDataIsDifferent: false, VerificationType: "Accept" })
        {
            return;
        }

        try
        {
            var overrideLicences = JsonSerializer.Deserialize<LinkedLicence[]>(
                verification.LicenceSectionOverrideValue
                ?? verification.LicenceSectionScrapedValue!, JsonHelper.GetSerializerOptions()) ?? [];

            var incomingOnlyLinkedLicences = (listRow.linkedLicences ?? [])
                .Where(x => x.ContainedIn != null &&
                            x.ContainedIn.All(c => c.Direction != LinkedLicenceDirection.Outgoing))
                .ToArray();

            listRow.linkedLicences = overrideLicences.Union(incomingOnlyLinkedLicences).ToArray();
        }
        catch
        {
            // If deserialization fails, don't apply the override
        }
    }
}