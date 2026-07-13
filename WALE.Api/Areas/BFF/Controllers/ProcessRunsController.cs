using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class ProcessRunsController(IOutputService outputService) : Controller
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessRun>>> GetProcessRuns()
    {
        var processRuns = await outputService.GetProcessRunsAsync();
        return Ok(processRuns.OrderByDescending(pr => pr.ProcessRunId));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessRun>>> GetAllProcessRuns()
    {
        var processRuns = await outputService.GetAllProcessRunsAsync();
        return Ok(processRuns.OrderByDescending(pr => pr.ProcessRunId));
    }

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, LicenceSet>>> GetProcessRunLicenceSetsAsync(
        [FromQuery] int processRunId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue)
    {
        var licences = await outputService.GetLicencesAsync(processRunId, skip, take);
        var licenceSets = await outputService.GetLicenceSetsAsync(processRunId, licences);

        return Ok(licenceSets);
    }

    [HttpGet("{processRunId:int}")]
    public async Task<ActionResult<ProcessRunResponse>> GetProcessRun(
        [FromRoute] int processRunId,
        [FromQuery] ProcessRunQuery query)
    {
        var take = query.Take;
        var skip = query.Skip;
        query.Take = int.MaxValue;
        query.Skip = 0;
        var completeNumber = 1;
        var fileNumber = 1;
        var allLatestLicenceSectionVerificationsTask =
            outputService.GetLatestLicenceSectionVerificationsAsync();
        var licencesAll = await outputService.GetLicencesSearchAsync(processRunId, query);
        var licenceSetsAll = await outputService.GetLicenceSetsAsync(processRunId, licencesAll);
        var allLatestLicenceSectionVerifications =
            (await allLatestLicenceSectionVerificationsTask).ToList();

        var paginationOutputLines = licencesAll
            .Where(licence => licence.Status == LicenceStatus.Ok)
            .Select(licence => JsOutputHelper.ToOutputLine(
                licence,
                DateTime.Now,
                completeNumber++,
                fileNumber++,
                licenceSetsAll))
            .ToList();

        var paginationListData = await JsOutputHelper.ToListDataAsync(
            paginationOutputLines,
            outputService,
            new ProcessRun
            {
                ProcessRunId = processRunId
            },
            false,
            allLatestLicenceSectionVerifications);

        if (!string.IsNullOrEmpty(query.ShortLicenceSetId))
        {
            paginationListData = paginationListData.Where(x =>
                    x.licenceSets?.Any(item => item?.ShortLicenceSetId?.Equals(query.ShortLicenceSetId) == true) ==
                    true)
                .ToList();
        }

        if (!string.IsNullOrEmpty(query.VerificationType))
        {
            paginationListData = paginationListData.Where(x =>
                x.latestLicenceSectionVerifications?.Any(item =>
                    item?.VerificationType?.Equals(query.VerificationType) == true) == true).ToList();
        }

        if (!string.IsNullOrEmpty(query.SortField) && query.SortAscending != null)
        {
            var sortedPaginationListData = SortOutputListDataItems(paginationListData, query.SortField, query.SortAscending.Value);
            var sortedProcessRun = new ProcessRunResponse
            {
                TotalRecords = sortedPaginationListData.Count,
                Records = sortedPaginationListData.Skip(skip).Take(take).ToList(),
                NoPaginationRecords = sortedPaginationListData,
            };

            return Ok(sortedProcessRun);
        }

        var processRun = new ProcessRunResponse
        {
            TotalRecords = paginationListData.Count,
            Records = paginationListData.Skip(skip).Take(take).ToList(),
            NoPaginationRecords = paginationListData,
        };

        return Ok(processRun);
    }

    [HttpGet]
    public async Task<ActionResult<int>> GetTotalLicenceCountAsync([FromQuery] int processRunId)
    {
        var total = await outputService.GetTotalLicenceCountAsync(processRunId, null);
        return Ok(total);
    }

    private static List<OutputListDataItem> SortOutputListDataItems(
        IEnumerable<OutputListDataItem> items,
        string? sortField,
        bool ascending)
    {
        return sortField switch
        {
            "filename" => Sort(items, x => x.filename, ascending),

            "licenceNumber" => Sort(items, x => x.licenceNumber, ascending),

            "licenceHolder" => Sort(items, x => x.licenceHolder, ascending),

            "purposes" => Sort(items, x => JoinStrings(x.purposes), ascending),

            "points" => Sort(items, x => JoinStrings(x.points), ascending),

            "limitsCount" => Sort(items, x => x.limitsCount, ascending),

            "aggregatesCount" => Sort(items, x => x.aggregatesCount, ascending),

            "ocr" => Sort(items, x => x.ocr, ascending),

            "issueDate" => Sort(items, x => ParseDate(x.issueDate), ascending),

            "issuer" => Sort(items, x => x.issuer, ascending),

            "meansFound" => Sort(items, x => x.meansFound, ascending),

            "status" => Sort(items, x => x.status, ascending),

            "linkedLicences" => Sort(items, x => x.linkedLicences?.Length ?? 0, ascending),

            "licenceSets" => Sort(items, x => JoinLicenceSets(x.licenceSets), ascending),

            "latestLicenceSectionVerifications" => Sort(
                items,
                x => x.latestLicenceSectionVerifications?.Count ?? 0,
                ascending),

            _ => Sort(items, x => x.filename, ascending)
        };
    }

    private static List<OutputListDataItem> Sort<TKey>(
        IEnumerable<OutputListDataItem> items,
        Func<OutputListDataItem, TKey> keySelector,
        bool ascending)
    {
        return ascending
            ? items.OrderBy(keySelector).ToList()
            : items.OrderByDescending(keySelector).ToList();
    }

    private static string JoinStrings(string?[]? values)
    {
        if (values == null || values.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(", ",
            values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var date)
            ? date
            : null;
    }

    private static string JoinLicenceSets(OutputListDataItemLicenceSet?[]? licenceSets)
    {
        if (licenceSets == null || licenceSets.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(", ",
            licenceSets
                .Where(x => x != null)
                .Select(x => x!.ShortLicenceSetId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
    }
}