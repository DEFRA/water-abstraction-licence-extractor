using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class VerificationMergeHelper
{
    public const string MissingJsonFlagReason = "BC Missing JSON";

    public static string GetVerificationWithNotes(LicenceSectionVerification verification)
    {
        var typeWithProcessRun = $"{verification.VerificationType}::{verification.ProcessRunId}";
        return string.IsNullOrWhiteSpace(verification.Notes) ? typeWithProcessRun : $"{typeWithProcessRun}::{verification.Notes}";
    }
    
    public static bool IsBusinessReviewWithNotes(string? verificationTypeWithNotes)
        => verificationTypeWithNotes != null 
           && (verificationTypeWithNotes.Contains("RequestBusinessReview") || verificationTypeWithNotes.Contains("CompleteBusinessReview"));
    
    public static bool IsExistingVerificationType(string? verificationTypeWithNotes, string verificationType)
        => verificationTypeWithNotes != null 
           && (verificationTypeWithNotes.Contains(verificationType));
    
    public static bool IsBusinessReview(string? verificationType)
        => verificationType is "RequestBusinessReview" or "CompleteBusinessReview";

    // CompleteBusinessReview verifications saved before JSON was recorded for them can't be merged
    public static bool IsCompleteBusinessReviewMissingJson(LicenceSectionVerification verification)
        => verification.VerificationType is "CompleteBusinessReview"
           && string.IsNullOrEmpty(verification.LicenceSectionOverrideValue)
           && string.IsNullOrEmpty(verification.LicenceSectionSnapshotValue)
           && string.IsNullOrEmpty(verification.LicenceSectionScrapedValue);

    public static void AddNewVerificationType(LicenceSectionVerification verification,
        LicenceSectionItemSummary existingSummary)
    {
        existingSummary.VerificationTypes = existingSummary.VerificationTypes
            .Append(verification.VerificationType!)
            .ToArray();

        existingSummary.VerificationTypesWithNotes = existingSummary.VerificationTypesWithNotes
            .Append(VerificationMergeHelper.GetVerificationWithNotes(verification))
            .ToArray();
    }
    
}    