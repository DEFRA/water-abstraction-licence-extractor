using Microsoft.AspNetCore.Mvc;
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
    IThumbnailUrlService thumbnailUrlService) : Controller
{
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
                Statuses = (await statusesTask).ToArray()
            });
    }
}
