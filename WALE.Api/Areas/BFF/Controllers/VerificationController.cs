using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Options;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Interfaces;
using WALE.Tools.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using Task = System.Threading.Tasks.Task;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class VerificationController(
    IAbstractionLicenceOutputService abstractionLicenceOutputService,
    IOptions<VerificationConfig> verificationConfig,
    IOutputService outputService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> ExtractHistory(
        [FromQuery] int chunk = 0)
    {
        var chunkSize = verificationConfig.Value.ChunkSize;

        var skip = chunk * chunkSize;
 
        // Grab one extra row so we know whether another chunk exists.
        var verifications =
            await abstractionLicenceOutputService.GetExportVerificationsAsync(
                skip,
                chunkSize + 1);

        var licenceSectionVerifications =
            verifications.ToList();

        var hasMore =
            licenceSectionVerifications.Count > chunkSize;

        var verificationChunk =
            licenceSectionVerifications
                .Take(chunkSize)
                .ToList();
 
        var file =
            await ToolHelper.CreateCsvAsync(
                verificationChunk);

        var csv =
            Encoding.UTF8.GetString(file);

        return Ok(
            new VerificationExportChunkResponse
            {
                FileName =
                    $"{verificationConfig.Value.PostgresqlHost}-verifications-{DateTime.UtcNow}.csv",

                Csv = csv,
                Chunk = chunk,
                HasMore = hasMore
            });
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

        var backupVersion = await currentBackupVersionTask;

        var result = new VerificationDataStatus
        {
            CurrentVerificationsBackupCount = await currentBackupCountTask,
            CurrentVerificationsCount = await currentVerificationCountTask,
            CurrentVerificationsBackupVersion = backupVersion.BackupVersion,
            LatestBackupVersionDate = backupVersion.BackupDateTimeUtc
        };
        
        return Ok(result);
    }
    
[HttpPut]
[Consumes("multipart/form-data")]
public async Task<IActionResult> ImportCsvChunk(
    [FromForm] IFormFile file,
    [FromQuery] int processRunId,
    [FromQuery] string uploadId,
    [FromQuery] int chunkIndex,
    [FromQuery] int totalChunks,
    [FromQuery] string fileName,
    CancellationToken cancellationToken)
{
    if (file.Length == 0)
        return BadRequest("CSV chunk is empty.");

    if (!string.Equals(
            Path.GetExtension(fileName),
            ".csv",
            StringComparison.OrdinalIgnoreCase))
    {
        return BadRequest("Only CSV files are supported.");
    }

    var tempFolder = Path.Combine(
        Path.GetTempPath(),
        "verification-imports");

    Directory.CreateDirectory(tempFolder);

    var tempFilePath = Path.Combine(
        tempFolder,
        $"{uploadId}.csv");

    await using (var outputStream = new FileStream(
                     tempFilePath,
                     chunkIndex == 0
                         ? FileMode.Create
                         : FileMode.Append,
                     FileAccess.Write))
    {
        await file.CopyToAsync(
            outputStream,
            cancellationToken);
    }

    // More chunks still to come
    if (chunkIndex < totalChunks - 1)
    {
        return Ok(new
        {
            chunkIndex,
            completed = false
        });
    }

    // Last chunk - process complete CSV
    try
    {
        await using var stream =
            System.IO.File.OpenRead(tempFilePath);

        using var reader =
            new StreamReader(stream);

        using var csv =
            new CsvReader(
                reader,
                new CultureInfo("en-GB"));

        var records =
            new List<LicenceSectionVerification>();

        await foreach (
            var record in csv.GetRecordsAsync<LicenceSectionVerification>(
                cancellationToken))
        {
            records.Add(record);
        }

        if (records.Count == 0)
        {
            return BadRequest("No records found to be imported.");
        }

        var environment =
            verificationConfig.Value.PostgresqlHost;

        AssignProcessRun(
            fileName,
            processRunId,
            environment,
            records);

       var success = await abstractionLicenceOutputService
            .ImportVerificationsAsync(records);

       if (success)
       {
          await FireAndForgetDataRefresh();
       }

        return Ok(new
        {
            imported = records.Count,
            completed = true
        });
    }
    catch (HeaderValidationException exception)
    {
        return BadRequest("Invalid file format, please check file columns.");
    }
    catch (Exception ex)
    {
        return BadRequest(ex.Message);
    }
    finally
    {
        if (System.IO.File.Exists(tempFilePath))
        {
            System.IO.File.Delete(tempFilePath);
        }
    }
}

    private static void AssignProcessRun(string fileName, int processRunId, string? environment, List<LicenceSectionVerification> records)
    {
        if (string.IsNullOrEmpty(environment) || fileName.Contains(environment, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        
        foreach (var record in records)
        {
            record.ProcessRunId = processRunId;
        }
    }

    private async Task FireAndForgetDataRefresh()
    {
        var processRuns = await outputService.GetAllProcessRunsAsync();

        var processRunId = processRuns.OrderByDescending(x => x.ProcessRunId).FirstOrDefault()?.ProcessRunId ?? 0;

        if (processRunId != 0)
        {
            _ = Task.Run((Func<Task?>)(async () =>
            {
                try
                {
                    await outputService
                        .UpdateProcessRunListProjectionAsync(processRunId);
                }
                catch
                {
                    // intentionally swallowed
                }
            }));
        }
    }
}