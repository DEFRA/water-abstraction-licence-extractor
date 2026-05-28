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
        try
        {
            var overrideLicence = JsonSerializer.Deserialize<LinkedLicence>(
                verification.LicenceSectionOverrideValue ?? verification.LicenceSectionScrapedValue!,
                JsonHelper.GetSerializerOptions());

            var incomingOnlyLinkedLicences = (listRow.linkedLicences ?? [])
                .Where(x => x.ContainedIn != null &&
                            x.ContainedIn.All(c => c.Direction != LinkedLicenceDirection.Outgoing))
                .ToArray();

            var outgoingLinkedLicences = (listRow.linkedLicences ?? [])
                .Where(x => x.ContainedIn != null &&
                            x.ContainedIn.Any(c => c.Direction == LinkedLicenceDirection.Outgoing))
                .ToArray();

            var existingLinkedLicence =
                outgoingLinkedLicences.FirstOrDefault(x => x.LicenceNumber == verification.LicenceSectionItemId);

            var result = incomingOnlyLinkedLicences.Union(outgoingLinkedLicences).ToList();

            switch (verification.VerificationType)
            {
                case "Confirmed":
                case "AutoPass":
                    if (existingLinkedLicence == null)
                    {
                        result.Add(overrideLicence!);
                    }
                    else if (verification.ScrapedDataIsDifferent)
                    {
                        result.Remove(existingLinkedLicence);
                        result.Add(overrideLicence!);
                    }

                    break;
                case "Removed":
                    if (existingLinkedLicence != null)
                    {
                        result.Remove(existingLinkedLicence);
                    }

                    break;
                case "Edited":
                case "Added":
                    if (existingLinkedLicence != null)
                    {
                        result.Remove(existingLinkedLicence);
                    }

                    result.Add(overrideLicence!);
                    break;
            }

            listRow.linkedLicences = result.ToArray();
        }
        catch
        {
            // If deserialization fails, don't apply the override
        }
    }
}