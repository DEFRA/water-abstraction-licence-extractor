using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Models.Csv;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Interfaces;
using WRADI.DocumentType.WrInspectionReport.Services;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class FileDataController(
    IOutputService outputService,
    IAbstractionLicenceOutputService abstractionLicenceOutputService,
    IUiProcessRunService uiProcessRunService,
    IFileService fileService,
    IInspectionReportFinderCacheService inspectionReportFinderCacheService,
    IWrInspectionReportSummaryReadService wrInspectionReportSummaryReadService,
    IMemoryCache memoryCache) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetWrInspectionReportsByDateAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? template,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        // Served from wr_inspection_report_summary, so this is one query rather than a result
        // fetch and a conversion per file, and it spans runs - a report is immutable, so the
        // newest run's extraction of a file is the only one worth showing.
        var summaries = await wrInspectionReportSummaryReadService.GetLatestPerFileAsync(
            from, to, template, skip, take);

        var discovery = await GetDiscoveryAsync();

        return Ok(summaries.Select(summary => new
        {
            fileId = summary.FileId,
            processRunId = summary.ProcessRunId,
            matchesResultId = summary.MatchesResultId,
            inspectionDate = summary.InspectionDate?.ToString("yyyy-MM-dd"),
            formDate = summary.FormDate?.ToString("yyyy-MM-dd"),
            dmsDocumentDate = summary.DmsDocumentDate?.ToString("yyyy-MM-dd"),
            dmsUploadDate = summary.DmsUploadDate?.ToString("yyyy-MM-dd"),
            sortDate = summary.SortDate?.ToString("yyyy-MM-dd"),
            dateSource = summary.DateSource.ToString(),
            dateWarning = summary.DateWarning.ToString(),
            permitNumber = summary.PermitNumber,
            originalFileName = discovery.GetValueOrDefault(summary.FileId.ToString()).FileName,
            template = summary.Template,
            completeness = summary.Completeness,
            isScan = summary.IsScan
        }));
    }

    [HttpGet]
    public async Task<ActionResult<List<(string filename, string status)>>> GetSimpleMatchResultsAsync(
        [FromQuery] int processRunId)
    {
        var result = await outputService.GetSimpleMatchResults(processRunId);
        return Ok(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<MatchesResult?>> GetMatchesResultAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int processRunId)
    {
        var result = await outputService.GetMatchesResultAsync(fileId, processRunId);
        return Ok(result);
    }
    
    // This version of the method just here so the generated TS client doesn't mangle some properties
    [HttpGet]
    public async Task<ActionResult<string?>> MatchesResultStringAsync([FromQuery] Guid fileId)
    {
        var result = await outputService.GetMatchesResultAsync(fileId);
        return Ok(JsonSerializer.Serialize(result, JsonHelper.GetSerializerOptions()));
    }

    // This version of the method just here so the generated TS client doesn't mangle some properties
    [HttpGet]
    public async Task<ActionResult<string?>> WrInspectionReportStringAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int processRunId)
    {
        var matchesResult = await outputService.GetMatchesResultAsync(fileId, processRunId);
        if (matchesResult == null) return Ok((string?)null);

        var result = WrInspectionReportSchemaConverter.ToForm(matchesResult, null, WrInspectionReportSummaryBuilder.GetKnownTemplate(matchesResult));
        return Ok(JsonSerializer.Serialize(result, JsonHelper.GetSerializerOptions()));
    }

    [HttpGet]
    public async Task<ActionResult> GetWrInspectionReportSummariesAsync(
        [FromQuery] int processRunId)
    {
        var cacheKey = $"wr-inspection-report-summaries:{processRunId}";

        var summaries = await memoryCache.GetOrCreateAsync(
            cacheKey,
            async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await BuildWrInspectionReportSummariesAsync(processRunId);
            });

        return Ok(summaries);
    }

    private async Task<List<object>> BuildWrInspectionReportSummariesAsync(int processRunId)
    {
        var discovery = await GetDiscoveryAsync();

        // Written as a run completes, and rebuildable on demand, so every column this list shows
        // comes back in one query.
        var projected = await wrInspectionReportSummaryReadService.GetForProcessRunAsync(processRunId);

        var rows = projected
            .Select(summary => (object)new
            {
                fileId = summary.FileId,
                originalFileName = discovery.GetValueOrDefault(summary.FileId.ToString()).FileName,
                template = summary.Template,
                date = summary.SortDate?.ToString("yyyy-MM-dd"),
                dateSource = summary.DateSource.ToString(),
                dateWarning = summary.DateWarning.ToString(),
                completeness = summary.Completeness,
                isScan = summary.IsScan
            })
            .ToList();

        // A file that failed extraction has nothing to project, but its DMS name is known from
        // discovery - without a row it appears in the list as a blank line.
        var projectedFileIds = projected.Select(summary => summary.FileId).ToHashSet();

        rows.AddRange((await outputService.GetSimpleMatchResults(processRunId))
            .Where(result => !projectedFileIds.Contains(result.FileId))
            .Select(result => (object)new
            {
                fileId = result.FileId,
                originalFileName = discovery.GetValueOrDefault(result.FileId.ToString()).FileName,
                template = (string?)null,
                date = (string?)null,
                dateSource = "None",
                dateWarning = "None",
                completeness = 0,
                isScan = false
            }));

        return rows;
    }

    /// The list shows the ingress key, which says nothing about the document. The DMS name, the
    /// permit number and the DMS date are all held only by the discovery result, keyed by the same
    /// file id.
    private async Task<Dictionary<string, DiscoveredFile>> GetDiscoveryAsync() =>
        (await inspectionReportFinderCacheService.GetInspectionReportFinderResultsAsync(0, int.MaxValue))
            .Where(result => !string.IsNullOrWhiteSpace(result.FileId))
            .GroupBy(result => result.FileId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new DiscoveredFile(
                    group.First().FileName,
                    group.First().PermitNumber,
                    group.First().DocumentDate,
                    // DMS holds an upload date for every file where a document date can be blank,
                    // but the finder does not persist it yet, so some files have no date at all.
                    UploadDate: null),
                StringComparer.OrdinalIgnoreCase);

    private record DiscoveredFile(
        string? FileName,
        string? PermitNumber,
        string? DocumentDate,
        string? UploadDate);


    [HttpGet]
    public async Task<ActionResult<MatchesResult?>> GetMatchesResultByMatchesResultIdAsync(
        [FromQuery] int matchesResultId)
    {
        var result = await outputService.GetMatchesResultAsync(matchesResultId);
        return Ok(result);
    }

    // This version of the method just here so the generated TS client doesn't mangle some properties
    [HttpGet]
    public async Task<ActionResult<string?>> GetMatchesResultByMatchesResultIdStringAsync(
        [FromQuery] int matchesResultId)
    {
        var result = await outputService.GetMatchesResultAsync(matchesResultId);
        return Ok(JsonSerializer.Serialize(result, JsonHelper.GetSerializerOptions()));
    }

    [HttpGet]
    public async Task<ActionResult> ExportWrInspectionReportCsvAsync(
        [FromQuery] int processRunId,
        [FromQuery] bool excludeInternalColumns = false)
    {
        var lines = await BuildWrInspectionReportCsvLinesAsync(processRunId);
        var bytes = WrInspectionReportReportBuilder.BuildCsv(lines, excludeInternalColumns);
        return File(bytes, "text/csv", $"WR51-ProcessRun-{processRunId}.csv");
    }

    [HttpGet]
    public async Task<ActionResult> ExportWrInspectionReportXlsxAsync(
        [FromQuery] int processRunId,
        [FromQuery] bool excludeInternalColumns = false)
    {
        var lines = await BuildWrInspectionReportCsvLinesAsync(processRunId);
        var bytes = WrInspectionReportReportBuilder.BuildXlsx(lines, excludeInternalColumns);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"WR51-ProcessRun-{processRunId}.xlsx");
    }

    [HttpGet]
    public async Task<ActionResult<Dictionary<Guid, List<LicenceFileMapEntry>>>> GetLicenceFileIdMapAsync(
        [FromQuery] int processRunId)
    {
        var result = await GetLicenceFileIdsAsync(processRunId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<Licence?>> LicenceAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int processRunId,
        [FromQuery] bool applyVerifications = false)
    {
        var result = await abstractionLicenceOutputService.GetLicenceAsync(
            fileId,
            processRunId,
            applyVerifications);
        
        return Ok(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<Licence?>> LicenceByLicenceIdAsync(
        [FromQuery] int licenceId,
        [FromQuery] bool applyVerifications = false)
    {
        var result = await abstractionLicenceOutputService.GetLicenceAsync(
            licenceId,
            applyVerifications);
        
        return Ok(result);
    }

    // This version of the method just here so the generated TS client doesn't mangle some properties
    [HttpGet]
    public async Task<ActionResult<string?>> LicenceByLicenceIdStringAsync(
        [FromQuery] int licenceId,
        [FromQuery] bool applyVerifications = false)
    {
        var result = await abstractionLicenceOutputService.GetLicenceAsync(
            licenceId,
            applyVerifications);
        
        return Ok(JsonSerializer.Serialize(result, JsonHelper.GetSerializerOptions()));
    }
    
    // This version of the method just here so the generated TS client doesn't mangle some properties
    [HttpGet]
    public async Task<ActionResult<string?>> LicenceStringAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int processRunId,
        [FromQuery] bool applyVerifications = false)
    {
        var result = await abstractionLicenceOutputService.GetLicenceAsync(fileId, processRunId, applyVerifications);
        return Ok(JsonSerializer.Serialize(result, JsonHelper.GetSerializerOptions()));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LinkedLicence>>> IncomingLinkedLicencesAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int processRunId)
    {
        var licence = await abstractionLicenceOutputService.GetLicenceAsync(fileId, processRunId);
        var linkedLicences = licence?.LinkedLicences;

        if (linkedLicences == null)
        {
            return NotFound();
        }

        var filtered = linkedLicences
            .Where(ll => ll.ContainedIn?.Any(cc => cc.Direction == InformationDirection.Incoming) == true);

        return Ok(filtered);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LinkedLicence>>> OutgoingLinkedLicencesAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int processRunId)
    {
        var licence = await abstractionLicenceOutputService.GetLicenceAsync(fileId, processRunId);
        var linkedLicences = licence?.LinkedLicences;

        if (linkedLicences == null)
        {
            return NotFound();
        }

        // Only return the outgoing links even if there are also incoming links for the same licence
        var filtered = linkedLicences
            .Where(ll => ll.ContainedIn?.Any(cc => cc.Direction == InformationDirection.Outgoing) == true)
            .ToList();

        foreach (var lic in filtered)
        {
            lic.ContainedIn = lic.ContainedIn!
                .Where(c => c.Direction == InformationDirection.Outgoing)
                .ToArray();
        }

        return Ok(filtered);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenceSet>>> LicenceSetsAsync([FromQuery] Guid fileId)
    {
        var results = await abstractionLicenceOutputService.GetLicenceSetsAsync(fileId);
        return Ok(results);
    }

    // This version of the method just here so the generated TS client doesn't mangle some properties
    [HttpGet]
    public async Task<ActionResult<string?>> LicenceSetsStringAsync([FromQuery] Guid fileId)
    {
        var results = await abstractionLicenceOutputService.GetLicenceSetsAsync(fileId);
        return Ok(JsonSerializer.Serialize(results, JsonHelper.GetSerializerOptions()));
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenceSet>>> LicenceSetsByLicenceIdAsync([FromQuery] int licenceId)
    {
        var results = await abstractionLicenceOutputService.GetLicenceSetsAsync(licenceId);
        return Ok(results);
    }

    // This version of the method just here so the generated TS client doesn't mangle some properties
    [HttpGet]
    public async Task<ActionResult<string?>> LicenceSetsByLicenceIdStringAsync([FromQuery] int licenceId)
    {
        var results = await abstractionLicenceOutputService.GetLicenceSetsAsync(licenceId);
        return Ok(JsonSerializer.Serialize(results, JsonHelper.GetSerializerOptions()));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenceSectionVerification>>> LicenceSectionVerifications(
        [FromQuery] Guid licenceFileId)
    {
        var results = await abstractionLicenceOutputService.GetLicenceSectionVerificationsAsync(licenceFileId);
        return Ok(results);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenceSectionVerification>>> GetAllVerificationsAsync(
        [FromQuery] int maxProcessRunId = int.MaxValue)
    {
        var results =
            await abstractionLicenceOutputService.GetAllVerificationsAsync(maxProcessRunId);

        return Ok(results);
    }

    [HttpPost]
    public ActionResult<string[]> AggregateIds([FromBody] Aggregate[] aggregates)
        => Ok(aggregates.Select(a => a.Id).ToArray());

    [HttpPost]
    public async Task<ActionResult<int>> CreateLicenceSectionVerification(
        [FromBody] LicenceSectionVerification verification)
    {
        var result = await abstractionLicenceOutputService.SaveLicenceSectionVerificationAsync(verification);

        if (result != 0)
        {
            await RefreshCurrentProcessRunLicenceListData(verification);
            FireAndForgetAffectedProcessRunsDataRefresh(verification);
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<int>> DeleteLicenceSectionVerification(
        [FromBody] LicenceSectionVerification verification)
    {
        var result = await abstractionLicenceOutputService.DeleteLicenceSectionVerificationAsync(
            verification.LicenceSectionVerificationId);

        if (result != 0)
        {
            await RefreshCurrentProcessRunLicenceListData(verification);
            FireAndForgetAffectedProcessRunsDataRefresh(verification);
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<int>> DeleteLicenceSectionVerifications(
        [FromBody] LicenceSectionVerification[] verifications)
    {
        if (verifications.Length == 0)
        {
            return BadRequest("No verifications supplied.");
        }

        var result = await abstractionLicenceOutputService.DeleteLicenceSectionVerificationsAsync(
            verifications.Select(v => v.LicenceSectionVerificationId));

        if (result != 0)
        {
            await RefreshCurrentProcessRunLicenceListData(verifications.MinBy(v => v.ProcessRunId)!);
            // Entries share a licence file and item ID, so refreshing from the earliest process run covers them all
            FireAndForgetAffectedProcessRunsDataRefresh(verifications.MinBy(v => v.ProcessRunId)!);
        }

        return Ok(result);
    }

    private void FireAndForgetAffectedProcessRunsDataRefresh(LicenceSectionVerification verification)
    {
        _ = Task.Run((Func<Task?>)(async () =>
        {
            try
            {
                await RefreshRecentProcessRunsLicenceListData(verification);
            }
            catch
            {
                // intentionally swallowed
            }
        }));
    }

    private async Task RefreshCurrentProcessRunLicenceListData(LicenceSectionVerification verification)
    {
        var mainLicence = await GetLicenceNumberFromFileId(verification.LicenceFileId, verification.ProcessRunId);

        if (!string.IsNullOrWhiteSpace(mainLicence))
        {
            var licenceList = new List<string> { mainLicence };

            if (!string.IsNullOrWhiteSpace(verification.LicenceSectionItemId))
            {
                licenceList.Add(verification.LicenceSectionItemId);
            }
            
            await uiProcessRunService.UpdateProcessRunByLicenceNumbersAsync(verification.ProcessRunId,
                licenceList.ToArray());
        }
    }

    private async Task RefreshRecentProcessRunsLicenceListData(LicenceSectionVerification verification)
    {
        var processRuns = await outputService.GetAllProcessRunsAsync();

        var currentProcessId = processRuns.OrderByDescending(x => x.ProcessRunId).FirstOrDefault()?.ProcessRunId;
        var processRunIds = new List<int>();

        if (currentProcessId is > 0 &&
            currentProcessId > verification.ProcessRunId)
        {
            foreach (var processRun in processRuns
                         .Where(x => x.ProcessRunId > verification.ProcessRunId)
                         .OrderBy(x => x.ProcessRunId))
            {
                processRunIds.Add(processRun.ProcessRunId);

                if (processRun.ProcessRunId == currentProcessId)
                {
                    break;
                }
            }
        }

        if (processRunIds.Count == 0)
        {
            return;
        }
        
        var mainLicence = await GetLicenceNumberFromFileId(verification.LicenceFileId, verification.ProcessRunId);
        
        if (!string.IsNullOrWhiteSpace(mainLicence))
        {
            var licenceList = new List<string> { mainLicence };

            if (!string.IsNullOrWhiteSpace(verification.LicenceSectionItemId))
            {
                licenceList.Add(verification.LicenceSectionItemId);
            }

            foreach (var processRunId in processRunIds)
            {
                await uiProcessRunService.UpdateProcessRunByLicenceNumbersAsync(processRunId,
                    licenceList.ToArray());
            }
        }
    }

    private async Task<List<WrInspectionReportCsvLine>> BuildWrInspectionReportCsvLinesAsync(int processRunId)
    {
        var simpleResults = await outputService.GetSimpleMatchResults(processRunId);

        using var semaphore = new SemaphoreSlim(10);

        var tasks = simpleResults.Select(async simpleResult =>
        {
            await semaphore.WaitAsync();

            try
            {
                var matchesResult = await outputService.GetMatchesResultAsync(simpleResult.FileId, processRunId);
                if (matchesResult == null)
                {
                    return null;
                }

                var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null, WrInspectionReportSummaryBuilder.GetKnownTemplate(matchesResult));
                var line = WrInspectionReportCsvLine.FromForm(form);

                // The file's own S3 address, not a link back through this API: durable, where a
                // presigned URL lapses after an hour and a BFF link dies with API reachability.
                // Needs the reader's own AWS credentials, which this export's pipeline consumer has.
                if (matchesResult.Filename == null)
                {
                    line.Metadata__FileUrl = null;
                }
                else
                {
                    var fileUrl = fileService.GetHttpsUrl(matchesResult.Filename, StorageFolder.Ingress);

                    line.Metadata__FileUrl = fileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? fileUrl
                        : $"{Request.Scheme}://{Request.Host}{fileUrl}";
                }

                return line;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var lines = await Task.WhenAll(tasks);
        return lines.Where(line => line != null).Cast<WrInspectionReportCsvLine>().ToList();
    }


    private async Task<string> GetLicenceNumberFromFileId(Guid fileId, int processRunId)
    {
        var fileIdToLicenceNumberMapping = await GetLicenceFileIdsAsync(processRunId);
        return fileIdToLicenceNumberMapping[fileId][0].LicenceNumber!;
    }

    private async Task<Dictionary<Guid, List<LicenceFileMapEntry>>> GetLicenceFileIdsAsync(int processRunId)
    {
        var cacheKey = $"licence-file-ids:{processRunId}";

        return await memoryCache.GetOrCreateAsync(
            cacheKey,
            async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(10);

                return await abstractionLicenceOutputService.GetLicenceFileIdsAsync(processRunId);
            }) ?? [];
    }
}