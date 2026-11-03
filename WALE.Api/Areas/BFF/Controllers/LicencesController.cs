using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WALE.Api.Areas.BFF.Models;
using WALE.Api.Interfaces;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class LicencesController(
    ILicenceListRepository licenceListRepository,
    ILicenceListItemModelService licenceListItemModelService,
    IThumbnailUrlService thumbnailUrlService,
    IAbstractionLicenceCacheService abstractionLicenceCacheService,
    IAbstractionLicenceOutputService abstractionLicenceOutputService,
    IMemoryCache memoryCache) : Controller
{
    [HttpGet]
    public async Task<ActionResult<LicenceDetailResponse>> GetLicenceDetail(
        [FromQuery] string licenceNumber)
    {
        if (string.IsNullOrWhiteSpace(licenceNumber))
        {
            return BadRequest("A licence number is required.");
        }

        var runs = await licenceListRepository.GetLicenceRunHistoryAsync(licenceNumber);

        var verifications = new List<LicenceSectionVerification>();

        foreach (var fileId in runs.Select(run => run.FileId).Distinct())
        {
            verifications.AddRange(
                await abstractionLicenceOutputService.GetLicenceSectionVerificationsAsync(
                    fileId,
                    licenceNumber));
        }

        return Ok(
            new LicenceDetailResponse
            {
                LicenceNumber = licenceNumber,
                Runs = runs,
                Verifications = verifications
                    .OrderByDescending(verification => verification.CreatedDateTimeUtc)
                    .ToList(),
                Successors = await GetSuccessorsAsync(licenceNumber),
                PossibleVariants = await GetPossibleVariantsAsync(licenceNumber)
            });
    }

    /// Deliberately not NormalizeLicenceNumber, which also drops zeros and so pairs
    /// 2/26/30/012 with 2/26/31/002: 73 groups across the estate against 4 that are real.
    private static string NormalizeForVariants(string licenceNumber) =>
        new(licenceNumber.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private async Task<string[]> GetPossibleVariantsAsync(string licenceNumber)
    {
        var estateLicenceNumbers = await memoryCache.GetOrCreateAsync(
            "licence-estate-licence-numbers",
            async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                return await licenceListRepository.GetLicenceEstateLicenceNumbersAsync();
            });

        var normalized = NormalizeForVariants(licenceNumber);

        return (estateLicenceNumbers ?? [])
            .Where(candidate =>
                !candidate.Equals(licenceNumber, StringComparison.OrdinalIgnoreCase) &&
                NormalizeForVariants(candidate) == normalized)
            .ToArray();
    }

    /// The visited set is a cycle guard, and it is load-bearing: six pairs of licences in NALD
    /// succeed each other, so a plain walk of 2/27/22/512 never returns.
    private async Task<string[]> GetSuccessorsAsync(string licenceNumber)
    {
        var history = await memoryCache.GetOrCreateAsync(
            "nald-licence-number-history",
            async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                return await abstractionLicenceCacheService.GetNaldLicenceNumberHistoryAsync();
            });

        if (history is null)
        {
            return [];
        }

        var successors = new List<string>();
        var visited = new HashSet<string> { licenceNumber.Trim().ToLowerInvariant() };
        var frontier = new Queue<string>(visited);

        while (frontier.Count > 0)
        {
            if (!history.TryGetValue(frontier.Dequeue(), out var entry))
            {
                continue;
            }

            foreach (var followOn in entry.FollowOnLicenceNumbers
                         .Where(followOn => visited.Add(followOn.ToLowerInvariant())))
            {
                successors.Add(followOn);
                frontier.Enqueue(followOn.ToLowerInvariant());
            }
        }

        return successors.ToArray();
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenceRunHistoryItem>>> GetLicenceRunHistory(
        [FromQuery] string licenceNumber)
    {
        if (string.IsNullOrWhiteSpace(licenceNumber))
        {
            return BadRequest("A licence number is required.");
        }

        return Ok(await licenceListRepository.GetLicenceRunHistoryAsync(licenceNumber));
    }

    [HttpGet]
    public async Task<ActionResult<LicenceEstateResponse>> GetLicenceEstate(
        [FromQuery] LicenceEstateQuery query)
    {
        var countTask = licenceListRepository.GetLicenceEstateCountAsync(query);
        var licencesTask = licenceListRepository.GetLicenceEstateAsync(query);
        var issuersTask = licenceListRepository.GetLicenceListIssuersAsync(query.ProcessRunId);
        var licenceSetIdsTask = licenceListRepository.GetLicenceListLicenceSetIdsAsync(query.ProcessRunId);
        var issueDatesTask = licenceListRepository.GetLicenceListIssueYearsAsync(query.ProcessRunId);
        var statusesTask = licenceListRepository.GetLicenceListStatusesAsync(query.ProcessRunId);
        var queueCountsTask = licenceListRepository.GetLicenceEstateQueueCountsAsync(query);
        var processRunIdsTask = licenceListRepository.GetLicenceListProcessRunIdsAsync();

        var records = licenceListItemModelService.ConvertToOutputListDataItems(
            await licencesTask);

        var thumbnailUrls = await thumbnailUrlService.GetThumbnailUrlsAsync(
            records.Select(record => record.fileId));

        foreach (var record in records)
        {
            record.thumbnailUrl = thumbnailUrls.GetValueOrDefault(record.fileId);
        }

        return Ok(
            new LicenceEstateResponse
            {
                TotalRecords = await countTask,
                Records = records,
                Issuers = (await issuersTask).ToArray(),
                LicenceSetIds = (await licenceSetIdsTask).ToArray(),
                IssueDates = (await issueDatesTask).ToArray(),
                Statuses = (await statusesTask).ToArray(),
                QueueCounts = await queueCountsTask,
                ProcessRunIds = (await processRunIdsTask).ToArray()
            });
    }
}
