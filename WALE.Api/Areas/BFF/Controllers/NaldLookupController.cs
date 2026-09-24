using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.BFF.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class NaldLookupController(
    IAbstractionLicenceCacheService abstractionLicenceCacheService) : Controller
{
    [HttpGet]
    public async Task<ActionResult<LicenceNaldData>> GetLicenceNaldDataAsync([FromQuery] string licenceNumber)
    {
        var trimmedLicenceNumber = licenceNumber.Trim();

        var abstractionData = await abstractionLicenceCacheService.GetNaldAbstractionLicenceAsync(
            trimmedLicenceNumber,
            0, // Region isn't used by the database lookup
            slashesRemoved: false,
            includeDetail: false);

        if (abstractionData != null)
        {
            var (naldStatus, licenceType) = NaldHelper.GetLicenceStatusAndType(abstractionData);

            return Ok(new LicenceNaldData
            {
                LicenceNumber = abstractionData.LicenceNumber,
                NaldStatus = naldStatus,
                LicenceType = licenceType,
                RegionId = abstractionData.FgacRegionCode
            });
        }

        var impoundmentData = await abstractionLicenceCacheService.GetNaldImpoundmentLicenceAsync(
            trimmedLicenceNumber,
            0); // Region isn't used by the database lookup

        if (impoundmentData != null)
        {
            return Ok(new LicenceNaldData
            {
                LicenceNumber = impoundmentData.LicenceNumber,
                NaldStatus = NaldHelper.GetImpoundmentLicenceStatus(impoundmentData),
                LicenceType = LicenceType.Impoundment,
                RegionId = impoundmentData.FgacRegionCode
            });
        }

        return Ok(new LicenceNaldData
        {
            LicenceNumber = trimmedLicenceNumber,
            NaldStatus = NaldLicenceStatus.Unknown,
            LicenceType = LicenceType.Unknown
        });
    }
}
