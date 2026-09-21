using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class VerificationMergeHelper
{
    public static string GetVerificationWithNotes(LicenceSectionVerification verification)
    {
        return $"{(string.IsNullOrWhiteSpace(verification.Notes) ? verification.VerificationType : $"{verification.VerificationType}::{verification.Notes}")}";
    }
    
    public static bool IsBusinessReviewWithNotes(string? verificationTypeWithNotes)
        => verificationTypeWithNotes != null 
           && (verificationTypeWithNotes.Contains("RequestBusinessReview") || verificationTypeWithNotes.Contains("CompleteBusinessReview"));
    
    public static bool IsExistingVerificationType(string? verificationTypeWithNotes, string verificationType)
        => verificationTypeWithNotes != null 
           && (verificationTypeWithNotes.Contains(verificationType));
    
    public static bool IsBusinessReview(string? verificationType)
        => verificationType is "RequestBusinessReview" or "CompleteBusinessReview";
    
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