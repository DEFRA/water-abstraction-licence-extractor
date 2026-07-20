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

    public void HandleVerifications(OutputListDataItem listRow, LicenceVerificationLookups verificationLookups,
        Guid fileId, string licenceNumber)
    {
        var hasOutgoingVerifications =
            verificationLookups.ByFileId.TryGetValue(fileId, out var outgoingVerifications);

        var hasIncomingVerifications =
            verificationLookups.ByItemId.TryGetValue(licenceNumber, out var incomingVerifications);

        if (!hasOutgoingVerifications && !hasIncomingVerifications)
        {
            return;
        }

        var incomingOnlyLinkedLicences = (listRow.linkedLicences ?? [])
            .Where(x => x.ContainedIn?.All(c => c.Direction != InformationDirection.Outgoing) == true)
            .ToList();

        var outgoingLinkedLicences = (listRow.linkedLicences ?? [])
            .Where(x => x.ContainedIn?.Any(c => c.Direction == InformationDirection.Outgoing) == true)
            .ToList();

        if (hasOutgoingVerifications)
        {
            var sectionSummaries = new List<LicenceSectionItemSummary>();
            ProcessOutgoingVerifications(outgoingVerifications!, listRow, outgoingLinkedLicences, sectionSummaries,
                incomingOnlyLinkedLicences);

            var summaries = listRow.licenceSectionVerifications?.ToList() ?? [];
            LicenceSectionVerificationSummary summary = new()
            {
                LicenceSectionName = SectionName,
                LicenceSectionItems = sectionSummaries.ToArray()
            };

            summaries.Add(summary);
            listRow.licenceSectionVerifications = summaries.ToArray();
        }

        if (hasIncomingVerifications)
        {
            ProcessIncomingVerifications(incomingVerifications!, incomingOnlyLinkedLicences);
        }

        listRow.linkedLicences = incomingOnlyLinkedLicences
            .Union(outgoingLinkedLicences)
            .ToArray();
    }

    private static void ProcessOutgoingVerifications(IEnumerable<LicenceSectionVerification> verifications,
        OutputListDataItem listRow,
        List<LinkedLicence> outgoingLinkedLicences,
        List<LicenceSectionItemSummary> sectionSummaries,
        List<LinkedLicence> incomingOnlyLinkedLicences)
    {
        var orderedVerifications = verifications
            .Where(v => v.LicenceSectionItemId is not null)
            .OrderBy(v => v.CreatedDateTimeUtc)
            .ToList();

        foreach (var verification in orderedVerifications)
        {
            var existingSummary =
                sectionSummaries.FirstOrDefault(s => s.LicenceSectionItemId == verification.LicenceSectionItemId);
            if (existingSummary == null)
            {
                sectionSummaries.Add(new LicenceSectionItemSummary
                {
                    LicenceSectionItemId = verification.LicenceSectionItemId!,
                    VerificationTypes = [verification.VerificationType!]
                });
            }
            else if (!existingSummary.VerificationTypes.Contains(verification.VerificationType!))
            {
                existingSummary.VerificationTypes = existingSummary.VerificationTypes
                    .Append(verification.VerificationType!)
                    .ToArray();
            }

            if (verification.LicenceSectionItemId == NoneOutgoing)
            {
                if (outgoingLinkedLicences.Count > 0)
                {
                    // Flag this because the verification confirmed there are zero LLs but actually there are some
                    verification.ScrapedDataIsDifferent = true;
                    outgoingLinkedLicences.Clear();
                }

                continue;
            }

            // Apply flag check
            if (verification.ProcessRunId < listRow.processRunId)
            {
                var wasScrapedThisRun = (listRow.linkedLicences ?? [])
                    .Any(x => x.LicenceNumber == verification.LicenceSectionItemId
                              && x.ContainedIn != null
                              && x.ContainedIn.Any(c => c.Direction == InformationDirection.Outgoing));

                var wasScrapedOnVerificationRun = !string.IsNullOrEmpty(verification.LicenceSectionScrapedValue);

                verification.ScrapedDataIsDifferent = wasScrapedThisRun != wasScrapedOnVerificationRun;
            }

            // Apply verification
            try
            {
                var json = verification.LicenceSectionOverrideValue
                           ?? verification.LicenceSectionSnapshotValue
                           ?? verification.LicenceSectionScrapedValue;

                if (string.IsNullOrEmpty(json))
                {
                    ConsoleHelper.WriteLine(
                        $"ERROR - {nameof(LinkedLicencesVerificationOutputStrategy)} - Verification {verification.LicenceSectionVerificationId} does not have any JSON");
                    continue;
                }

                var verificationLicence =
                    JsonSerializer.Deserialize<LinkedLicence>(json, JsonHelper.GetSerializerOptions());

                if (verificationLicence == null)
                {
                    ConsoleHelper.WriteLine(
                        $"ERROR - {nameof(LinkedLicencesVerificationOutputStrategy)} - Verification {verification.LicenceSectionVerificationId} does not have valid JSON");
                    continue;
                }

                var existingOutgoingLinkedLicence =
                    outgoingLinkedLicences.FirstOrDefault(x =>
                        x.LicenceNumber == verification.LicenceSectionItemId);

                var existingIncomingLinkedLicence =
                    incomingOnlyLinkedLicences.FirstOrDefault(x =>
                        x.LicenceNumber == verification.LicenceSectionItemId);

                switch (verification.VerificationType)
                {
                    case "Confirmed":
                    case "AutoConfirm":
                    case "Edited":
                    case "Added":
                        if (existingIncomingLinkedLicence != null)
                        {
                            incomingOnlyLinkedLicences.Remove(existingIncomingLinkedLicence);
                        }

                        if (existingOutgoingLinkedLicence != null)
                        {
                            outgoingLinkedLicences.Remove(existingOutgoingLinkedLicence);
                        }

                        outgoingLinkedLicences.Add(verificationLicence);
                        break;
                    case "Removed":
                        if (existingOutgoingLinkedLicence != null)
                        {
                            outgoingLinkedLicences.Remove(existingOutgoingLinkedLicence);
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
        IEnumerable<LicenceSectionVerification> invertedVerifications,
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
                    incomingOnlyLinkedLicences.FirstOrDefault(x =>
                        x.LicenceNumber == invertedVerification.SourceLicenceNumber);

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