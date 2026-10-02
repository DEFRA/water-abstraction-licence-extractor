using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Models.Csv;
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

    /// <summary>
    /// The handful of per-file fields the inspection report list shows as columns. The list used
    /// to get these by calling WrInspectionReportString once per file, which is one request per
    /// row - 17,000+ on a full process run. Cached, because the work is the same conversion the
    /// CSV export does and the underlying results don't change within a run.
    /// </summary>
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

                var form = WrInspectionReportSchemaConverter.ToForm(
                    matchesResult, null, GetKnownTemplate(matchesResult));

                // Every field is read back off the serialized JSON rather than the typed model, so
                // this returns exactly what the list used to compute for itself from this same
                // payload - notably Template, which JsonHelper writes as its name ("unknown")
                // where the default options would write the enum's ordinal.
                using var document = JsonDocument.Parse(
                    JsonSerializer.Serialize(form, JsonHelper.GetSerializerOptions()));

                var root = document.RootElement;
                var metadata = root.GetProperty("metadata");

                return (object)new
                {
                    fileId = simpleResult.FileId,
                    template = GetStringOrNull(metadata, "template"),
                    date = GetInspectionDate(root, metadata),
                    completeness = ComputeCompleteness(root),
                    isScan = metadata.TryGetProperty("isScan", out var isScan)
                             && isScan.ValueKind == JsonValueKind.True
                };
            }
            finally
            {
                semaphore.Release();
            }
        });

        return (await Task.WhenAll(tasks)).Where(summary => summary != null).ToList()!;
    }

    private static string? GetStringOrNull(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    // The inspection date proper, falling back to the form's own printed date - the same order of
    // preference the list applied client-side.
    private static string? GetInspectionDate(JsonElement report, JsonElement metadata)
    {
        if (report.TryGetProperty("inspectionDate", out var inspectionDate))
        {
            var dateTime = GetStringOrNull(inspectionDate, "dateTime");

            if (dateTime != null)
            {
                return dateTime.Split('T')[0];
            }
        }

        return metadata.TryGetProperty("date", out var date)
            ? GetStringOrNull(date, "date")
            : null;
    }

    // Rough completeness proxy - the percentage of the report's top-level sections carrying any
    // content at all.
    private static int ComputeCompleteness(JsonElement report)
    {
        string[] sectionNames =
        [
            "licenceNumber", "licenceNumberCleaned", "inspectionClass", "address", "metWith",
            "inspectingOfficer", "inspectionDate", "licenceProvisions", "measurementDetails",
            "generalComments"
        ];

        var withContent = sectionNames.Count(sectionName =>
            report.TryGetProperty(sectionName, out var section) && HasContent(section));

        return (int)Math.Round(withContent / (double)sectionNames.Length * 100);
    }

    private static bool HasContent(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(element.GetString()),
            JsonValueKind.Array => element.EnumerateArray().Any(HasContent),
            JsonValueKind.Object => element.EnumerateObject().Any(property => HasContent(property.Value)),
            _ => true
        };

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
        var processRuns = await outputService.GetAllProcessRunsAsync();

        var currentProcessId = processRuns.OrderByDescending(x => x.ProcessRunId).FirstOrDefault()?.ProcessRunId;
        var processRunIds = new List<int> {verification.ProcessRunId};

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

                var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null, GetKnownTemplate(matchesResult));
                var line = WrInspectionReportCsvLine.FromForm(form);

                // A raw s3:// URI isn't clickable and needs direct bucket credentials nobody
                // reading this export has - link through FilesController's own presigned-URL
                // redirect instead, so it resolves to a real, working HTTPS download on click.
                // Deliberately not embedding a presigned URL directly here: AwsS3FileService.
                // GetPresignedUrlAsync expires in 2 minutes, which suits that redirect's
                // generate-then-immediately-follow flow but would already have expired by the
                // time anyone opens this CSV.
                line.Metadata__FileUrl = matchesResult.Filename == null
                    ? null
                    : $"{Request.Scheme}://{Request.Host}/BFF/Files/GetAsync?filename={Uri.EscapeDataString(matchesResult.Filename)}";

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