using System.Text.Json;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Strategies;

public class LinkedLicencesVerificationOutputStrategy : IVerificationOutputStrategy
{
    private const string NoneOutgoing = "None Outgoing";
    
    public string SectionName => "Linked Licences";

    public void HandleVerifications(IEnumerable<LicenceSectionVerification> verifications, OutputListDataItem listRow, IEnumerable<InvertedLicenceSectionVerification> invertedVerifications)
    {
        var incomingOnlyLinkedLicences = (listRow.linkedLicences ?? [])
            .Where(x => x.ContainedIn != null &&
                        x.ContainedIn.All(c => c.Direction != LinkedLicenceDirection.Outgoing))
            .ToList();

        var outgoingLinkedLicences = (listRow.linkedLicences ?? [])
            .Where(x => x.ContainedIn != null &&
                        x.ContainedIn.Any(c => c.Direction == LinkedLicenceDirection.Outgoing))
            .ToList();

        ProcessOutgoingVerifications(verifications, listRow, incomingOnlyLinkedLicences, outgoingLinkedLicences);
        ProcessIncomingVerifications(invertedVerifications, incomingOnlyLinkedLicences);

        listRow.linkedLicences = incomingOnlyLinkedLicences.Union(outgoingLinkedLicences).ToArray();
    }

    private static void ProcessIncomingVerifications(IEnumerable<InvertedLicenceSectionVerification> invertedVerifications, List<LinkedLicence> incomingOnlyLinkedLicences)
    {
        foreach (var invertedVerification in invertedVerifications.OrderByDescending(v => v.Verification.CreatedDateTimeUtc))
        {
            try
            {
                var verification = invertedVerification.Verification;
                var overrideLicence = JsonSerializer.Deserialize<LinkedLicence>(
                    verification.LicenceSectionOverrideValue ?? verification.LicenceSectionScrapedValue!,
                    JsonHelper.GetSerializerOptions());

                overrideLicence!.ContainedIn = overrideLicence.ContainedIn?
                    .Select(x => x with { Direction = LinkedLicenceDirection.Incoming }).ToArray();
                
                overrideLicence.LicenceNumber = invertedVerification.SourceLicenceNumber;

                var existingLinkedLicence =
                    incomingOnlyLinkedLicences.FirstOrDefault(x => x.LicenceNumber == invertedVerification.SourceLicenceNumber);

                switch (verification.VerificationType)
                {
                    case "Confirmed":
                    case "AutoConfirm":
                        if (existingLinkedLicence == null)
                        {
                            incomingOnlyLinkedLicences.Add(overrideLicence!);
                        }

                        break;
                    case "Removed":
                        if (existingLinkedLicence != null)
                        {
                            incomingOnlyLinkedLicences.Remove(existingLinkedLicence);
                        }

                        break;
                    case "Edited":
                    case "Added":
                        if (existingLinkedLicence != null)
                        {
                            incomingOnlyLinkedLicences.Remove(existingLinkedLicence);
                        }

                        incomingOnlyLinkedLicences.Add(overrideLicence!);
                        break;
                }
            }
            catch
            {
                // If deserialization fails, don't apply the override
            }
        }
    }

    private static void ProcessOutgoingVerifications(IEnumerable<LicenceSectionVerification> verifications, OutputListDataItem listRow,
        List<LinkedLicence> incomingOnlyLinkedLicences, List<LinkedLicence> outgoingLinkedLicences)
    {
        var orderedVerifications = verifications
            .OrderByDescending(v => v.CreatedDateTimeUtc)
            .ToList();

        if (orderedVerifications.Count == 0)
        {
            return;
        }

        var firstVerification = orderedVerifications[0];
        if (firstVerification.LicenceSectionItemId == NoneOutgoing)
        {
            foreach (var verification in orderedVerifications[1..])
            {
                listRow.latestLicenceSectionVerifications!.Remove(verification);
            }

            if (firstVerification.ProcessRunId < listRow.processRunId && outgoingLinkedLicences.Count > 0)
            {
                firstVerification.ScrapedDataIsDifferent = true;
            }
            
            outgoingLinkedLicences.Clear();
            return;
        }

        foreach (var verification in orderedVerifications)
        {
            if (verification.LicenceSectionItemId == NoneOutgoing)
            {
                listRow.latestLicenceSectionVerifications!.Remove(verification);
                continue;
            }

            if (verification.ProcessRunId < listRow.processRunId)
            {
                var wasScrapedThisRun = (listRow.linkedLicences ?? [])
                    .Any(x => x.LicenceNumber == verification.LicenceSectionItemId
                              && x.ContainedIn != null
                              && x.ContainedIn.Any(c => c.Direction == LinkedLicenceDirection.Outgoing));

                verification.ScrapedDataIsDifferent = verification.VerificationType switch
                {
                    "Confirmed" or "AutoConfirm" or "Removed" or "Edited" => !wasScrapedThisRun,
                    "Added" => wasScrapedThisRun,
                    _ => false
                };
            }

            try
            {
                var overrideLicence = JsonSerializer.Deserialize<LinkedLicence>(
                    verification.LicenceSectionOverrideValue ?? verification.LicenceSectionScrapedValue!,
                    JsonHelper.GetSerializerOptions());

                var existingLinkedLicence =
                    outgoingLinkedLicences.FirstOrDefault(x => x.LicenceNumber == verification.LicenceSectionItemId);

                switch (verification.VerificationType)
                {
                    case "Confirmed":
                    case "AutoConfirm":
                        if (existingLinkedLicence == null)
                        {
                            outgoingLinkedLicences.Add(overrideLicence!);
                        }
                        else if (verification.ScrapedDataIsDifferent)
                        {
                            outgoingLinkedLicences.Remove(existingLinkedLicence);
                            outgoingLinkedLicences.Add(overrideLicence!);
                        }

                        break;
                    case "Removed":
                        if (existingLinkedLicence != null)
                        {
                            outgoingLinkedLicences.Remove(existingLinkedLicence);
                        }

                        break;
                    case "Edited":
                    case "Added":
                        if (existingLinkedLicence != null)
                        {
                            outgoingLinkedLicences.Remove(existingLinkedLicence);
                        }

                        outgoingLinkedLicences.Add(overrideLicence!);
                        break;
                }
            }
            catch
            {
                // If deserialization fails, don't apply the override
            }
        }
    }
}