namespace WALE.Api.Areas.BFF.Models;

public class VerificationDataStatus
{
    public int? CurrentVerificationsCount { get; set; }

    public int? CurrentVerificationsBackupCount { get; set; }

    public int? CurrentVerificationsBackupVersion { get; set; }

    public DateTime? LatestBackupVersionDate { get; set; }
}