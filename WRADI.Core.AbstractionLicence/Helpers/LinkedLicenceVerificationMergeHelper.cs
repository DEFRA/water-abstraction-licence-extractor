using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class LinkedLicenceVerificationMergeHelper
{
    public const string NoneOutgoing = "None Outgoing";
    public const string Review = "Review";

    public static List<LicenceSectionItemSummary> MergeOutgoing(
        List<LinkedLicence> linkedLicences,
        IEnumerable<LicenceSectionVerification> verifications,
        LinkedLicence[]? originalLinkedLicences,
        int? processRunId)
    {
        var sectionSummaries = new List<LicenceSectionItemSummary>();

        var orderedVerifications = verifications
            .Where(v => v.LicenceSectionItemId is not null)
            .OrderBy(v => v.CreatedDateTimeUtc)
            .ToList();

        foreach (var verification in orderedVerifications)
        {
            UpdateSectionSummaries(sectionSummaries, verification);

            // Ignore review and auto-warn/fail - we just want the tags to appear to flag them for review
            if (verification.LicenceSectionItemId == Review
                || IsAutoOrRequestBusinessReview(verification.VerificationType))
            {
                continue;
            }

            if (verification.LicenceSectionItemId == NoneOutgoing)
            {
                if (linkedLicences.Any(l => true ==
                                            l.ContainedIn?.Any(c => c.Direction == InformationDirection.Outgoing)))
                {
                    // Flag this because the verification confirmed there are zero outgoing LLs but actually there are some
                    FlagItemSummary(sectionSummaries, verification.LicenceSectionItemId, "'None Outgoing' verification contradicted by existence of LLs");
                    foreach (var linkedLicence in linkedLicences)
                    {
                        RemoveAllLinksForDirection(linkedLicence, InformationDirection.Outgoing);
                    }

                    // These licences' active state is now contradicted by the newer NoneOutgoing confirmation
                    var staleItemIds = sectionSummaries
                        .Where(s => s.LicenceSectionItemId != NoneOutgoing
                                    && s.LicenceSectionItemId != Review
                                    && s.CurrentVerificationType != "Removed")
                        .Select(s => s.LicenceSectionItemId)
                        .ToList();

                    foreach (var staleItemId in staleItemIds)
                    {
                        SimulateRemoval(sectionSummaries, staleItemId);
                    }
                }

                continue;
            }

            if (VerificationMergeHelper.IsCompleteBusinessReviewMissingJson(verification))
            {
                FlagItemSummary(sectionSummaries, verification.LicenceSectionItemId, VerificationMergeHelper.MissingJsonFlagReason);
                continue;
            }

            // Apply verification
            try
            {
                var json = verification.LicenceSectionOverrideValue
                           ?? verification.LicenceSectionScrapedValue
                           ?? verification.LicenceSectionSnapshotValue;

                if (string.IsNullOrEmpty(json))
                {
                    ConsoleHelper.WriteLine(
                        $"ERROR - {nameof(LinkedLicenceVerificationMergeHelper)} - Verification {verification.LicenceSectionVerificationId} does not have any JSON");
                    continue;
                }

                var verificationLicence =
                    JsonSerializer.Deserialize<LinkedLicence>(json, WALE.ProcessFile.Core.Helpers.JsonHelper.GetSerializerOptions());

                if (verificationLicence == null)
                {
                    ConsoleHelper.WriteLine(
                        $"ERROR - {nameof(LinkedLicenceVerificationMergeHelper)} - Verification {verification.LicenceSectionVerificationId} does not have valid JSON");
                    continue;
                }

                // Apply data changed flag check
                if (verification.ProcessRunId < processRunId)
                {
                    var scrapedLinkedLicence = (originalLinkedLicences ?? [])
                        .FirstOrDefault(x => x.LicenceNumber == verification.LicenceSectionItemId
                                             && x.ContainedIn != null
                                             && x.ContainedIn.Any(c => c.Direction == InformationDirection.Outgoing));

                    var wasScrapedThisRun = scrapedLinkedLicence != null;
                    var wasScrapedOnVerificationRun = !string.IsNullOrEmpty(verification.LicenceSectionScrapedValue);

                    string? flagReason = null;

                    if (wasScrapedThisRun != wasScrapedOnVerificationRun)
                    {
                        flagReason = wasScrapedThisRun
                            ? "LL added to scraper output since the verification run"
                            : "LL removed from scraper output since the verification run";
                    }
                    else if (scrapedLinkedLicence != null)
                    {
                        var changes = new List<string>();

                        if (IsDeadNaldStatus(scrapedLinkedLicence.NaldStatus)
                            && !IsDeadNaldStatus(verificationLicence.NaldStatus))
                        {
                            changes.Add(scrapedLinkedLicence.NaldStatus.ToString());
                        }

                        if (IsSuperseded(scrapedLinkedLicence)
                            && !IsSuperseded(verificationLicence))
                        {
                            changes.Add("Superseded");
                        }

                        if (changes.Count > 0)
                        {
                            flagReason = $"Linked Licence {string.Join(" & ", changes)}";
                        }
                    }

                    if (flagReason != null)
                    {
                        FlagItemSummary(sectionSummaries, verification.LicenceSectionItemId, flagReason);
                    }
                }

                var existingLinkedLicence =
                    linkedLicences.FirstOrDefault(x => x.LicenceNumber == verification.LicenceSectionItemId);

                switch (verification.VerificationType)
                {
                    case "Confirmed":
                    case "CompleteBusinessReview":
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

                        var noneOutgoingSummary =
                            sectionSummaries.FirstOrDefault(s => s.LicenceSectionItemId == NoneOutgoing);
                        if (noneOutgoingSummary != null && noneOutgoingSummary.CurrentVerificationType != "Removed")
                        {
                            // This outgoing licence contradicts the earlier "confirmed none outgoing" state
                            SimulateRemoval(sectionSummaries, NoneOutgoing);
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
                ConsoleHelper.WriteLine($"ERROR - {nameof(LinkedLicenceVerificationMergeHelper)} - {ex}");
            }
        }

        return sectionSummaries;
    }

    public static void MergeIncoming(
        List<LinkedLicence> linkedLicences,
        IEnumerable<LicenceSectionVerification> verifications,
        Dictionary<Guid, List<LicenceFileMapEntry>> fileIdToLicenceNumberMapping)
    {
        var orderedVerifications = verifications
            .OrderBy(v => v.CreatedDateTimeUtc)
            .ToList();

        foreach (var verification in orderedVerifications)
        {
            var fileId = verification.LicenceFileId;
            if (!fileIdToLicenceNumberMapping.TryGetValue(fileId, out var sourceMapping))
            {
                ConsoleHelper.WriteLine(
                    $"ERROR - {nameof(LinkedLicenceVerificationMergeHelper)} - Incoming LL Verifications - No licence number found for {fileId}");
                continue;
            }

            // Ignore auto-warn/fail - it has no effect on incoming LLs
            if (IsAutoOrRequestBusinessReview(verification.VerificationType)
                || VerificationMergeHelper.IsCompleteBusinessReviewMissingJson(verification))
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
                        $"ERROR - {nameof(LinkedLicenceVerificationMergeHelper)} - Verification {verification.LicenceSectionVerificationId} does not have any JSON");
                    continue;
                }

                var verificationLicence =
                    JsonSerializer.Deserialize<LinkedLicence>(json, WALE.ProcessFile.Core.Helpers.JsonHelper.GetSerializerOptions());

                if (verificationLicence == null)
                {
                    ConsoleHelper.WriteLine(
                        $"ERROR - {nameof(LinkedLicenceVerificationMergeHelper)} - Verification {verification.LicenceSectionVerificationId} does not have valid JSON");
                    continue;
                }

                var licenceNumber = sourceMapping[0].LicenceNumber;
                
                var existingLinkedLicence =
                    linkedLicences.FirstOrDefault(x => x.LicenceNumber == licenceNumber);

                // TODO: We need to convert the verification licence to an incoming link - use the logic in WalSchemaConverter - but much of this will require looking up
                var convertedToIncoming = new LinkedLicence
                {
                    LicenceNumber = licenceNumber,
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
                    case "CompleteBusinessReview":
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
                ConsoleHelper.WriteLine($"ERROR - {nameof(LinkedLicenceVerificationMergeHelper)} - {ex}");
            }
        }
    }

    private static void RemoveAllLinksForDirection(LinkedLicence linkedLicence, InformationDirection directionToRemove)
        => linkedLicence.ContainedIn = linkedLicence.ContainedIn?
            .Where(c => c.Direction != directionToRemove).ToArray();

    private static void SimulateRemoval(List<LicenceSectionItemSummary> sectionSummaries, string itemId)
    {
        var syntheticVerification = new LicenceSectionVerification
        {
            LicenceSectionItemId = itemId,
            VerificationType = "Removed"
        };

        UpdateSectionSummaries(sectionSummaries, syntheticVerification);
    }

    private static void UpdateSectionSummaries(List<LicenceSectionItemSummary> sectionSummaries,
        LicenceSectionVerification verification)
    {
        // Clear the review item as soon as we have any other later manual verification
        if (verification.LicenceSectionItemId != Review &&
            verification.VerificationType is not ("AutoWarn" or "AutoFail" or "AutoConfirm"))
        {
            var existingReviewSummary =
                sectionSummaries.FirstOrDefault(s => s.LicenceSectionItemId == Review);

            if (existingReviewSummary != null)
            {
                sectionSummaries.Remove(existingReviewSummary);
            }
        }

        var existingSummary =
            sectionSummaries.FirstOrDefault(s => s.LicenceSectionItemId == verification.LicenceSectionItemId);

        if (existingSummary == null)
        {
            sectionSummaries.Add(new LicenceSectionItemSummary
            {
                LicenceSectionItemId = verification.LicenceSectionItemId!,
                VerificationTypes = [verification.VerificationType!],
                CurrentVerificationType = verification.VerificationType!,
                VerificationTypesWithNotes = [VerificationMergeHelper.GetVerificationWithNotes(verification)]
            });
        }
        else
        {
            // New business review tags should override previous ones - clear the previous ones first
            if (VerificationMergeHelper.IsBusinessReview(verification.VerificationType))
            {
                existingSummary.VerificationTypes = existingSummary.VerificationTypes
                    .Where(x => !VerificationMergeHelper.IsBusinessReview(x))
                    .ToArray();
                
                existingSummary.VerificationTypesWithNotes = existingSummary.VerificationTypesWithNotes
                    .Where(x => !VerificationMergeHelper.IsBusinessReviewWithNotes(x))
                    .ToArray();
            }

            existingSummary.CurrentVerificationType = verification.VerificationType!;
            if (!existingSummary.VerificationTypes.Contains(verification.VerificationType!))
            {
                VerificationMergeHelper.AddNewVerificationType(verification, existingSummary);
            }
            else
            {
                // remove existing and re add
                existingSummary.VerificationTypes = existingSummary.VerificationTypes
                    .Where(x => x != verification.VerificationType!)
                    .ToArray();
                
                existingSummary.VerificationTypesWithNotes = existingSummary.VerificationTypesWithNotes
                    .Where(x => !VerificationMergeHelper.IsExistingVerificationType(x, verification.VerificationType!))
                    .ToArray();
                
                VerificationMergeHelper.AddNewVerificationType(verification, existingSummary);
            }

            if (!IsAutoOrRequestBusinessReview(verification.VerificationType))
            {
                // Clear the flag, it'll be re-calculated for this verification later
                existingSummary.IsFlagged = false;
                existingSummary.FlagReason = null;
            }
        }
    }

    private static void FlagItemSummary(List<LicenceSectionItemSummary> sectionSummaries, string? itemId, string flagReason)
    {
        var summary = sectionSummaries.FirstOrDefault(s => s.LicenceSectionItemId == itemId);

        if (summary != null)
        {
            summary.IsFlagged = true;
            summary.FlagReason = flagReason;
            return;
        }

        ConsoleHelper.WriteLine(
            $"ERROR - {nameof(LinkedLicenceVerificationMergeHelper)} - Flag was not set - no summary found for {itemId}");
    }

    private static bool IsAutoOrRequestBusinessReview(string? verificationType)
        => verificationType is "AutoWarn" or "AutoFail"
           or "RequestBusinessReview";

    private static bool IsDeadNaldStatus(NaldLicenceStatus naldStatus)
        => naldStatus is NaldLicenceStatus.Expired or NaldLicenceStatus.Revoked or NaldLicenceStatus.Lapsed;

    private static bool IsSuperseded(LinkedLicence linkedLicence)
        => linkedLicence.ContainedIn?
            .SelectMany(c => c.History ?? [])
            .Any(h => h.LicenceNumber == linkedLicence.LicenceNumber
                      && h.FollowOnLicenceNumbers.Count > 0) == true;
}
