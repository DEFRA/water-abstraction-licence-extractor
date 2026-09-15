using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Csv;
using WRADI.DocumentType.WrInspectionReport.Enums;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class FileDataController(
    IOutputService outputService,
    IAbstractionLicenceOutputService abstractionLicenceOutputService,
    IUiProcessRunService uiProcessRunService,
    IMemoryCache memoryCache) : Controller
{
    [HttpGet]
    public async Task<ActionResult<List<(string filename, string status)>>> GetSimpleMatchResultsAsync(
        [FromQuery] int processRunId)
    {
        var result = await outputService.GetSimpleMatchResults(processRunId);
        return Ok(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<MatchesResult?>> MatchesResult([FromQuery] Guid fileId)
    {
        var result = await outputService.GetMatchesResultAsync(fileId);
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

        var result = WrInspectionReportSchemaConverter.ToForm(matchesResult, null, GetKnownTemplate(matchesResult));
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
    public async Task<ActionResult<Dictionary<Guid, string>>> GetLicenceFileIdMapAsync([FromQuery] int processRunId)
    {
        var result = await GetLicenceFileIdsAsync(processRunId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<Licence?>> Licence(
        [FromQuery] Guid fileId,
        [FromQuery] int processRunId,
        [FromQuery] bool applyVerifications = false)
    {
        var result = await abstractionLicenceOutputService.GetLicenceAsync(fileId, processRunId, applyVerifications);
        return Ok(result);
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
    public async Task<ActionResult<IEnumerable<LicenceSet>>> LicenceSets([FromQuery] Guid fileId)
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
            await RefreshLicenceListData(verification);
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
            await RefreshLicenceListData(verification);
        }

        return Ok(result);
    }

    private async Task RefreshLicenceListData(LicenceSectionVerification verification)
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

    // Reuses the exact same MatchesResult -> ToForm -> WrInspectionReportCsvLine path
    // WrInspectionReportStringAsync already uses for a single file's JSON view, just looped
    // across every file in the run with bounded concurrency (fetching each file's MatchesResult
    // is a DB read, not a re-scrape, so this stays cheap even for large runs) - skips files with
    // no saved MatchesResult (errored out before ever saving), matching the report's own
    // skip-on-null convention from the earlier standalone GenerateWrInspectionReportCsv tool.
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

                var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null, GetKnownTemplate(matchesResult));
                return WrInspectionReportCsvLine.FromForm(form);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var lines = await Task.WhenAll(tasks);
        return lines.Where(line => line != null).Cast<WrInspectionReportCsvLine>().ToList();
    }

    // AdditionalInformation values round-trip through JSON (matches_result.data), so a value
    // saved as a string comes back as a JsonElement, not a plain string - handle both rather
    // than assume the runtime shape of an untyped object? dictionary value.
    private static WrTemplateType? GetKnownTemplate(MatchesResult matchesResult)
    {
        if (matchesResult.AdditionalInformation == null
            || !matchesResult.AdditionalInformation.TryGetValue(
                WrInspectionReportSchemaConverter.AdditionalInformationTemplateKey, out var value))
        {
            return null;
        }

        var rawTemplate = value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null
        };

        return Enum.TryParse<WrTemplateType>(rawTemplate, out var template) ? template : null;
    }

    private async Task<string> GetLicenceNumberFromFileId(Guid fileId, int processRunId)
    {
        var fileIdToLicenceNumberMapping = await GetLicenceFileIdsAsync(processRunId);

        return fileIdToLicenceNumberMapping[fileId];
    }

    private async Task<Dictionary<Guid, string>> GetLicenceFileIdsAsync(int processRunId)
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