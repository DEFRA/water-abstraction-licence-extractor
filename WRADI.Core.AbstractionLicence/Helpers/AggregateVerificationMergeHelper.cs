using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class AggregateVerificationMergeHelper
{
    public const string NoAggregatesSentinel = "None";

    public static (List<string> AggregateIds, List<Aggregate> Aggregates, List<LicenceSectionItemSummary> Summaries) Merge(
        IEnumerable<string>? scrapedIds,
        IEnumerable<Aggregate>? scrapedAggregates,
        IEnumerable<LicenceSectionVerification> verifications,
        int? processRunId)
    {
        var originalIds = (scrapedIds ?? []).ToList();
        var ids = originalIds.ToList();
        var scrapedAggregateList = (scrapedAggregates ?? []).ToList();
        var aggregates = scrapedAggregateList.ToList();
        var summaries = new List<LicenceSectionItemSummary>();

        foreach (var verification in OrderVerifications(verifications))
        {
            UpdateSectionSummaries(summaries, verification);

            if (IsAutoOrRequestBusinessReview(verification.VerificationType))
            {
                continue;
            }

            var itemId = verification.LicenceSectionItemId!;

            if (itemId == NoAggregatesSentinel)
            {
                if (ids.Count > 0 || aggregates.Count > 0)
                {
                    FlagItemSummary(summaries, itemId, "'None' verification contradicted by existence of aggregates");
                    ids.Clear();
                    aggregates.Clear();

                    var staleItemIds = summaries
                        .Where(s => s.LicenceSectionItemId != NoAggregatesSentinel
                                    && s.CurrentVerificationType != "Removed")
                        .Select(s => s.LicenceSectionItemId)
                        .ToList();

                    foreach (var staleItemId in staleItemIds)
                    {
                        SimulateRemoval(summaries, staleItemId);
                    }
                }

                continue;
            }

            if (VerificationMergeHelper.IsCompleteBusinessReviewMissingJson(verification))
            {
                FlagItemSummary(summaries, itemId, VerificationMergeHelper.MissingJsonFlagReason);
                continue;
            }

            if (verification.ProcessRunId < processRunId)
            {
                var wasScrapedThisRun = originalIds.Contains(itemId);
                var wasScrapedOnVerificationRun = !string.IsNullOrEmpty(verification.LicenceSectionScrapedValue);

                if (wasScrapedThisRun != wasScrapedOnVerificationRun)
                {
                    var flagReason = wasScrapedThisRun
                        ? "Aggregate added to scraper output since the verification run"
                        : "Aggregate removed from scraper output since the verification run";
                    FlagItemSummary(summaries, itemId, flagReason);
                }
            }

            var existingAggregate = aggregates.FirstOrDefault(a => a.Id == itemId);

            switch (verification.VerificationType)
            {
                case "Confirmed":
                case "CompleteBusinessReview":
                case "AutoConfirm":
                case "Edited":
                case "Added":
                    try
                    {
                        var json = verification.LicenceSectionOverrideValue
                                   ?? verification.LicenceSectionSnapshotValue
                                   ?? verification.LicenceSectionScrapedValue;

                        if (string.IsNullOrEmpty(json))
                        {
                            ConsoleHelper.WriteLine(
                                $"ERROR - {nameof(AggregateVerificationMergeHelper)} - Verification {verification.LicenceSectionVerificationId} does not have any JSON");
                            continue;
                        }

                        var verificationAggregate =
                            VerificationMergeHelper.DeserializeVerificationValue<Aggregate>(json, out var verificationNode);

                        if (verificationAggregate == null)
                        {
                            ConsoleHelper.WriteLine(
                                $"ERROR - {nameof(AggregateVerificationMergeHelper)} - Verification {verification.LicenceSectionVerificationId} does not have valid JSON");
                            continue;
                        }

                        VerificationMergeHelper.ApplyCurrentRunValuesForMissingProperties(verificationAggregate,
                            scrapedAggregateList.FirstOrDefault(a => a.Id == itemId), verificationNode!);

                        if (existingAggregate != null)
                        {
                            aggregates.Remove(existingAggregate);
                        }

                        aggregates.Add(verificationAggregate);

                        if (!ids.Contains(itemId))
                        {
                            ids.Add(itemId);
                        }

                        var noneSummary = summaries.FirstOrDefault(s => s.LicenceSectionItemId == NoAggregatesSentinel);
                        if (noneSummary != null && noneSummary.CurrentVerificationType != "Removed")
                        {
                            // This aggregate contradicts the earlier "confirmed none" state
                            SimulateRemoval(summaries, NoAggregatesSentinel);
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelper.WriteLine($"ERROR - {nameof(AggregateVerificationMergeHelper)} - {ex}");
                    }

                    break;
                case "Removed":
                    ids.Remove(itemId);
                    if (existingAggregate != null)
                    {
                        aggregates.Remove(existingAggregate);
                    }

                    break;
            }
        }

        // Drop entries that were Added and eventually Removed
        var compactedSummaries = summaries
            .Where(s => ids.Contains(s.LicenceSectionItemId)
                        || !(s.VerificationTypes.Contains("Added") && s.VerificationTypes.Contains("Removed")))
            .ToList();

        return (ids, aggregates, compactedSummaries);
    }

    private static List<LicenceSectionVerification> OrderVerifications(
        IEnumerable<LicenceSectionVerification> verifications)
        => verifications
            .Where(v => v.LicenceSectionItemId is not null)
            .OrderBy(v => v.CreatedDateTimeUtc)
            .ToList();

    private static bool IsAutoOrRequestBusinessReview(string? verificationType)
        => verificationType is "AutoWarn" or "AutoFail"
            or "RequestBusinessReview";

    private static void UpdateSectionSummaries(List<LicenceSectionItemSummary> sectionSummaries,
        LicenceSectionVerification verification)
    {
        var itemId = verification.LicenceSectionItemId!;
        var existingSummary = sectionSummaries.FirstOrDefault(s => s.LicenceSectionItemId == itemId);

        if (existingSummary == null)
        {
            sectionSummaries.Add(new LicenceSectionItemSummary
            {
                LicenceSectionItemId = itemId,
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

    private static void SimulateRemoval(List<LicenceSectionItemSummary> sectionSummaries, string itemId)
    {
        var syntheticVerification = new LicenceSectionVerification
        {
            LicenceSectionItemId = itemId,
            VerificationType = "Removed"
        };

        UpdateSectionSummaries(sectionSummaries, syntheticVerification);
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
            $"ERROR - {nameof(AggregateVerificationMergeHelper)} - Flag was not set - no summary found for {itemId}");
    }
}
