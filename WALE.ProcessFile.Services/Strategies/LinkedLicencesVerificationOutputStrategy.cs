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

    public void HandleVerifications(
        IEnumerable<LicenceSectionVerification> verifications,
        OutputListDataItem listRow,
        IEnumerable<InvertedLicenceSectionVerification> invertedVerifications)
    {
        var incomingOnlyLinkedLicences = (listRow.linkedLicences ?? [])
            .Where(x => x.ContainedIn?.All(
                c => c.Direction != InformationDirection.Outgoing) == true)
            .ToList();

        var outgoingLinkedLicences = (listRow.linkedLicences ?? [])
            .Where(x => x.ContainedIn?.Any(
                c => c.Direction == InformationDirection.Outgoing) == true)
            .ToList();

        ProcessOutgoingVerifications(verifications, listRow, outgoingLinkedLicences);
        ProcessIncomingVerifications(invertedVerifications, incomingOnlyLinkedLicences);

        listRow.linkedLicences = incomingOnlyLinkedLicences
            .Union(outgoingLinkedLicences)
            .ToArray();
    }
    
    private static void ProcessOutgoingVerifications(
        IEnumerable<LicenceSectionVerification> verifications,
        OutputListDataItem listRow,
        List<LinkedLicence> outgoingLinkedLicences)
    {
        HashSet<string> licenceNumbersSeen = [];
        
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

            // Skip processing older verifications for the same licence number
            if (!licenceNumbersSeen.Add(verification.LicenceSectionItemId!))
            {
                continue;
            }

            if (verification.ProcessRunId < listRow.processRunId)
            {
                var wasScrapedThisRun = (listRow.linkedLicences ?? [])
                    .Any(x => x.LicenceNumber == verification.LicenceSectionItemId
                              && x.ContainedIn != null
                              && x.ContainedIn.Any(c => c.Direction == InformationDirection.Outgoing));
                
                var wasScrapedOnVerificationRun = !string.IsNullOrEmpty(verification.LicenceSectionScrapedValue);

                verification.ScrapedDataIsDifferent = wasScrapedThisRun != wasScrapedOnVerificationRun;
            }
            
            //todo: calculate effective verification type (for multi's)

            try
            {
                var json = verification.LicenceSectionOverrideValue
                   ?? verification.LicenceSectionSnapshotValue
                   ?? verification.LicenceSectionScrapedValue;

                LinkedLicence? overrideLicence = null;
                
                if (!string.IsNullOrEmpty(json))
                {
                    overrideLicence = JsonSerializer.Deserialize<LinkedLicence>(
                        json,
                        JsonHelper.GetSerializerOptions());
                }

                var existingLinkedLicence =
                    outgoingLinkedLicences.FirstOrDefault(x => x.LicenceNumber == verification.LicenceSectionItemId);

                // todo: follow same logic as UI
                switch (verification.VerificationType)
                {
                    case "Confirmed":
                    case "AutoConfirm":
                        if (existingLinkedLicence == null && overrideLicence != null)
                        {
                            outgoingLinkedLicences.Add(overrideLicence);
                        }
                        else if (verification.ScrapedDataIsDifferent)
                        {
                            if (existingLinkedLicence != null)
                            {
                                outgoingLinkedLicences.Remove(existingLinkedLicence);
                            }

                            if (overrideLicence != null)
                            {
                                outgoingLinkedLicences.Add(overrideLicence);
                            }
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

                        if (overrideLicence != null)
                        {
                            outgoingLinkedLicences.Add(overrideLicence);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(LinkedLicencesVerificationOutputStrategy)} - {ex}");
            }
        }
    }
    
    private static void ProcessIncomingVerifications(
        IEnumerable<InvertedLicenceSectionVerification> invertedVerifications,
        List<LinkedLicence> incomingOnlyLinkedLicences)
    {
        var orderedInvertedVerifications = invertedVerifications
            .OrderByDescending(v => v.Verification.CreatedDateTimeUtc)
            .ToList();
        
        foreach (var invertedVerification in orderedInvertedVerifications)
        {
            try
            {
                var verification = invertedVerification.Verification;
                var json = verification.LicenceSectionOverrideValue
                   ?? verification.LicenceSectionSnapshotValue
                   ?? verification.LicenceSectionScrapedValue;

                LinkedLicence? overrideLicence = null;
                
                if (!string.IsNullOrEmpty(json))
                {
                    overrideLicence = JsonSerializer.Deserialize<LinkedLicence>(
                        json,
                        JsonHelper.GetSerializerOptions());

                    overrideLicence!.ContainedIn = overrideLicence.ContainedIn?
                        .Select(x => x with { Direction = InformationDirection.Incoming })
                        .ToArray();

                    overrideLicence.LicenceNumber = invertedVerification.SourceLicenceNumber;
                }

                var existingLinkedLicence =
                    incomingOnlyLinkedLicences.FirstOrDefault(x => x.LicenceNumber == invertedVerification.SourceLicenceNumber);

                switch (verification.VerificationType)
                {
                    case "Confirmed":
                    case "AutoConfirm":
                        if (existingLinkedLicence == null && overrideLicence != null)
                        {
                            incomingOnlyLinkedLicences.Add(overrideLicence);
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

                        if (overrideLicence != null)
                        {
                            incomingOnlyLinkedLicences.Add(overrideLicence);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(LinkedLicencesVerificationOutputStrategy)} - {ex}");
            }
        }
    }
}