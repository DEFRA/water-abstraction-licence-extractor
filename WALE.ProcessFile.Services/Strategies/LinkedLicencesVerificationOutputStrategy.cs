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
    private const string Review = "Review";

    public string SectionName => "Linked Licences";

    public void HandleVerifications(OutputListDataItem listRow, LicenceVerificationLookups verificationLookups,
        Guid fileId, string licenceNumber, Dictionary<Guid, string> fileIdToLicenceNumberMapping)
    {
        var hasOutgoingVerifications =
            verificationLookups.ByFileId.TryGetValue(fileId, out var outgoingVerifications);

        var hasIncomingVerifications =
            verificationLookups.ByItemId.TryGetValue(licenceNumber, out var incomingVerifications);

        if (!hasOutgoingVerifications && !hasIncomingVerifications)
        {
            return;
        }

        var linkedLicences = listRow.linkedLicences?.ToList() ?? [];

        if (hasOutgoingVerifications)
        {
            var sectionSummaries = new List<LicenceSectionItemSummary>();
            ProcessOutgoingVerifications(outgoingVerifications!, listRow, sectionSummaries, linkedLicences);

            var summaries = listRow.licenceSectionVerifications?.ToList() ?? [];
            summaries.Add(new LicenceSectionVerificationSummary
            {
                LicenceSectionName = SectionName,
                LicenceSectionItems = sectionSummaries.ToArray()
            });
            listRow.licenceSectionVerifications = summaries.ToArray();
        }

        if (hasIncomingVerifications)
        {
            ProcessIncomingVerifications(incomingVerifications!, fileIdToLicenceNumberMapping, linkedLicences);
        }

        listRow.linkedLicences = linkedLicences.Where(ll => ll.ContainedIn?.Length > 0).ToArray();
    }

    private static void ProcessOutgoingVerifications(IEnumerable<LicenceSectionVerification> verifications,
        OutputListDataItem listRow, List<LicenceSectionItemSummary> sectionSummaries,
        List<LinkedLicence> linkedLicences)
    {
        var orderedVerifications = verifications
            .Where(v => v.LicenceSectionItemId is not null)
            .OrderBy(v => v.CreatedDateTimeUtc)
            .ToList();

        foreach (var verification in orderedVerifications)
        {
            UpdateSectionSummaries(sectionSummaries, verification);

            // Ignore review and auto-warn/fail - we just want the tags to appear to flag them for review
            if (verification.LicenceSectionItemId == Review
                || verification.VerificationType is "AutoWarn" or "AutoFail")
            {
                continue;
            }

            if (verification.LicenceSectionItemId == NoneOutgoing)
            {
                if (linkedLicences.Any(l => true ==
                                            l.ContainedIn?.Any(c => c.Direction == InformationDirection.Outgoing)))
                {
                    // Flag this because the verification confirmed there are zero outgoing LLs but actually there are some
                    verification.ScrapedDataIsDifferent = true;
                    foreach (var linkedLicence in linkedLicences)
                    {
                        RemoveAllLinksForDirection(linkedLicence, InformationDirection.Outgoing);
                    }
                }

                continue;
            }

            // Apply data changed flag check
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

                var existingLinkedLicence =
                    linkedLicences.FirstOrDefault(x => x.LicenceNumber == verification.LicenceSectionItemId);

                switch (verification.VerificationType)
                {
                    case "Confirmed":
                    case "AutoConfirm":
                    case "Edited":
                    case "Added":
                        linkedLicences.Add(verificationLicence);
                        if (existingLinkedLicence != null)
                        {
                            // Merge the Incoming (and Unknown) links with the overridden Outgoing links
                            verificationLicence.ContainedIn = (verificationLicence.ContainedIn ?? [])
                                .Where(c => c.Direction == InformationDirection.Outgoing)
                                .Union(existingLinkedLicence.ContainedIn?.Where(c =>
                                    c.Direction != InformationDirection.Outgoing) ?? []).ToArray();
                            linkedLicences.Remove(existingLinkedLicence);
                        }

                        break;
                    case "Removed":
                        if (existingLinkedLicence != null)
                        {
                            RemoveAllLinksForDirection(existingLinkedLicence, InformationDirection.Outgoing);
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

    private static void RemoveAllLinksForDirection(LinkedLicence linkedLicence, InformationDirection directionToRemove)
        => linkedLicence.ContainedIn = linkedLicence.ContainedIn?
            .Where(c => c.Direction != directionToRemove).ToArray();

    private static void UpdateSectionSummaries(List<LicenceSectionItemSummary> sectionSummaries,
        LicenceSectionVerification verification)
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
    }

    private static void ProcessIncomingVerifications(IEnumerable<LicenceSectionVerification> incomingVerifications,
        Dictionary<Guid, string> fileIdToLicenceNumberMapping, List<LinkedLicence> linkedLicences)
    {
        var orderedVerifications = incomingVerifications
            .OrderBy(v => v.CreatedDateTimeUtc)
            .ToList();

        foreach (var verification in orderedVerifications)
        {
            var fileId = verification.LicenceFileId;
            if (!fileIdToLicenceNumberMapping.TryGetValue(fileId, out var sourceLicenceNumber))
            {
                ConsoleHelper.WriteLine(
                    $"ERROR - {nameof(LinkedLicencesVerificationOutputStrategy)} - Incoming LL Verifications - No licence number found for {fileId}");
                continue;
            }

            // Ignore auto-warn/fail - it has no effect on incoming LLs
            if (verification.VerificationType is "AutoWarn" or "AutoFail")
            {
                continue;
            }

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

                var existingLinkedLicence =
                    linkedLicences.FirstOrDefault(x => x.LicenceNumber == sourceLicenceNumber);

                // TODO: We need to convert the verification licence to an incoming link - use the logic in WalSchemaConverter - but much of this will require looking up
                var convertedToIncoming = new LinkedLicence
                {
                    LicenceNumber = sourceLicenceNumber,
                    DmsFileId = fileId,
                    ContainedIn = verificationLicence.ContainedIn?.Select(c => new ContainedInInformation
                    {
                        Source = InformationSource.OtherDocument,
                        Direction = InformationDirection.Incoming,
                        SectionName = c.SectionName,
                        LinkReason = c.LinkReason,
                        LineNumber = c.LineNumber,
                        PageNumber = c.PageNumber
                    }).ToArray(),
                    /*RegionId = verificationLicence.RegionId,
                    RawScrapedLicenceNumber = scrapedLinkedLicenceNumber,
                    DmsPermitNumber = dmsFileData?.PermitNumber,
                    DmsPath = dmsFileData?.DmsPath,
                    Filename = filename,
                    NaldStatus = naldStatus,
                    LicenceType = licenceType,
                    LicenceVersion = licenceVersion*/
                };

                switch (verification.VerificationType)
                {
                    case "Confirmed":
                    case "AutoConfirm":
                    case "Edited":
                    case "Added":
                        if (existingLinkedLicence == null)
                        {
                            linkedLicences.Add(convertedToIncoming);
                        }
                        else
                        {
                            // Merge the Outgoing (and Unknown) links with the overridden Incoming links
                            existingLinkedLicence.ContainedIn = (existingLinkedLicence.ContainedIn ?? [])
                                .Where(c => c.Direction != InformationDirection.Incoming)
                                .Union(convertedToIncoming.ContainedIn?.Where(c =>
                                    c.Direction == InformationDirection.Incoming) ?? []).ToArray();
                        }

                        break;
                    case "Removed":
                        if (existingLinkedLicence != null)
                        {
                            RemoveAllLinksForDirection(existingLinkedLicence, InformationDirection.Incoming);
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