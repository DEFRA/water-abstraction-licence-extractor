using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Options;
using WALE.Api.Areas.BFF.Models;
using WALE.Api.Models;
using WALE.Tools.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using Task = System.Threading.Tasks.Task;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class VerificationController(
    IAbstractionLicenceOutputService abstractionLicenceOutputService, IOptions<DbConfig> dbConfigOptions) : Controller
{
    [HttpGet]
    public async Task<IActionResult> ExtractHistoryAsync()
    {
        var verifications = await abstractionLicenceOutputService.GetExportVerificationsAsync();
        
        var file = await ToolHelper.CreateCsvAsync(verifications);

        var fileName =
            $"{dbConfigOptions.Value.PostgresqlHost}-verifications-{DateTime.Now:yyyyMMddHHmmssfff}.csv";

        return File(
            file,
            "text/csv",
            fileName);
    }

    [HttpGet]
    public async Task<ActionResult<VerificationDataStatus>> GetVerificationDataStatus()
    {
        var currentVerificationCountTask =  abstractionLicenceOutputService.GetCurrentVerificationsCountAsync();
        var currentBackupCountTask =  abstractionLicenceOutputService.GetCurrentBackupVerificationsCountAsync();
        var currentBackupVersionTask = abstractionLicenceOutputService.GetCurrentVerificationsBackupVersionAsync();

        await Task.WhenAll(
            currentBackupVersionTask,
            currentVerificationCountTask,
            currentBackupCountTask);

        var result = new VerificationDataStatus
        {
            CurrentVerificationsBackupCount = await currentBackupCountTask,
            CurrentVerificationsCount = await currentVerificationCountTask,
            CurrentVerificationsBackupVersion = await currentBackupVersionTask
        };
        
        return Ok(result);
    }
    
    [HttpPut]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportCsv(
        [FromForm] IFormFile file,
        [FromQuery] int processRunId,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest("CSV file is empty.");

        if (!string.Equals(
                Path.GetExtension(file.FileName),
                ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only CSV files are supported.");
        }

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CultureInfo("en-GB"));

        var records = new List<LicenceSectionVerification>();

        await foreach (var record in csv.GetRecordsAsync<LicenceSectionVerification>(cancellationToken))
        {
            records.Add(record);
        }

        var environment = dbConfigOptions.Value.PostgresqlHost;
        
        AssignProcessRun(file, processRunId, environment, records);

        await abstractionLicenceOutputService.ImportVerificationsAsync(records);

        return Ok(new
        {
            imported = records.Count
        });
    }

    private static void AssignProcessRun(IFormFile file, int processRunId, string? environment, List<LicenceSectionVerification> records)
    {
        if (string.IsNullOrEmpty(environment) || file.FileName.Contains(environment, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        
        foreach (var record in records)
        {
            record.ProcessRunId = processRunId;
        }
    }
}