using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class AggregateVerificationMergeHelper
{
    public const string NoAggregatesSentinel = "None";

    public static (List<string> AggregateIds, List<LicenceSectionItemSummary> Summaries) MergeAggregateIds(
        IEnumerable<string>? scrapedIds,
        IEnumerable<LicenceSectionVerification> verifications)
    {
        var originalIds = (scrapedIds ?? []).ToList();
        var ids = originalIds.ToList();
        var summaries = new List<LicenceSectionItemSummary>();

        foreach (var verification in OrderVerifications(verifications))
        {
            UpdateSectionSummaries(summaries, verification);

            if (IsAutoOrBusinessReview(verification.VerificationType))
            {
                continue;
            }

            var itemId = verification.LicenceSectionItemId!;

            if (itemId == NoAggregatesSentinel)
            {
                if (ids.Count > 0)
                {
                    // Flag this because the verification confirmed there are zero aggregates but actually there are some
                    FlagItemSummary(summaries, itemId);
                }

                continue;
            }

            var wasScrapedThisRun = originalIds.Contains(itemId);
            var wasScrapedOnVerificationRun = !string.IsNullOrEmpty(verification.LicenceSectionScrapedValue);

            if (wasScrapedThisRun != wasScrapedOnVerificationRun)
            {
                FlagItemSummary(summaries, itemId);
            }

            switch (verification.VerificationType)
            {
                case "Confirmed":
                case "AutoConfirm":
                case "Edited":
                case "Added":
                    if (!ids.Contains(itemId))
                    {
                        ids.Add(itemId);
                    }

                    break;
                case "Removed":
                    ids.Remove(itemId);
                    break;
            }
        }

        // Drop entries that were Added and eventually Removed
        var compactedSummaries = summaries
            .Where(s => ids.Contains(s.LicenceSectionItemId)
                        || !(s.VerificationTypes.Contains("Added") && s.VerificationTypes.Contains("Removed")))
            .ToList();

        return (ids, compactedSummaries);
    }

    public static List<Aggregate> MergeAggregates(
        IEnumerable<Aggregate>? scrapedAggregates,
        IEnumerable<LicenceSectionVerification> verifications)
    {
        var aggregates = (scrapedAggregates ?? []).ToList();

        foreach (var verification in OrderVerifications(verifications))
        {
            var itemId = verification.LicenceSectionItemId!;

            if (itemId == NoAggregatesSentinel || IsAutoOrBusinessReview(verification.VerificationType))
            {
                continue;
            }

            var existingAggregate = aggregates.FirstOrDefault(a => a.Id == itemId);

            switch (verification.VerificationType)
            {
                case "Confirmed":
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
                            JsonSerializer.Deserialize<Aggregate>(json, WALE.ProcessFile.Core.Helpers.JsonHelper.GetSerializerOptions());

                        if (verificationAggregate == null)
                        {
                            ConsoleHelper.WriteLine(
                                $"ERROR - {nameof(AggregateVerificationMergeHelper)} - Verification {verification.LicenceSectionVerificationId} does not have valid JSON");
                            continue;
                        }

                        if (existingAggregate != null)
                        {
                            aggregates.Remove(existingAggregate);
                        }

                        aggregates.Add(verificationAggregate);
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelper.WriteLine($"ERROR - {nameof(AggregateVerificationMergeHelper)} - {ex}");
                    }

                    break;
                case "Removed":
                    if (existingAggregate != null)
                    {
                        aggregates.Remove(existingAggregate);
                    }

                    break;
            }
        }

        return aggregates;
    }

    private static List<LicenceSectionVerification> OrderVerifications(
        IEnumerable<LicenceSectionVerification> verifications)
        => verifications
            .Where(v => v.LicenceSectionItemId is not null)
            .OrderBy(v => v.CreatedDateTimeUtc)
            .ToList();

    private static bool IsAutoOrBusinessReview(string? verificationType)
        => verificationType is "AutoWarn" or "AutoFail"
            or "RequestBusinessReview" or "CompleteBusinessReview";

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

            if (!IsAutoOrBusinessReview(verification.VerificationType))
            {
                // Clear the flag, it'll be re-calculated for this verification later
                existingSummary.ScrapedDataIsDifferent = false;
            }
        }
    }

    private static void FlagItemSummary(List<LicenceSectionItemSummary> sectionSummaries, string? itemId)
    {
        var summary = sectionSummaries.FirstOrDefault(s => s.LicenceSectionItemId == itemId);

        if (summary != null)
        {
            summary.ScrapedDataIsDifferent = true;
            return;
        }

        ConsoleHelper.WriteLine(
            $"ERROR - {nameof(AggregateVerificationMergeHelper)} - Flag was not set - no summary found for {itemId}");
    }
}
