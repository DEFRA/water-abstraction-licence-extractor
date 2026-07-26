using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.Api.Areas.Public.Controllers;

[ApiController]
[Area("Public")]
[Route("/[area]/[controller]/[action]")]
public class LinkedLicencesController(IOutputService outputService) : Controller
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LinkedLicence>>> GetAsync([FromQuery] string permitNumber)
    {
        var linkedLicences =
            await outputService.GetLinkedLicencesAsync(permitNumber);

        if (linkedLicences == null)
        {
            return NotFound();
        }

        return Ok(linkedLicences);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LinkedLicence>>> GetIncomingAsync([FromQuery] string permitNumber)
    {
        var linkedLicences =
            await outputService.GetLinkedLicencesAsync(permitNumber);

        if (linkedLicences == null)
        {
            return NotFound();
        }

        var filtered = linkedLicences
            .Where(ll => ll.ContainedIn!.Any(cc => cc.Direction == InformationDirection.Incoming));

        return Ok(filtered);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LinkedLicence>>> GetOutgoingAsync([FromQuery] string permitNumber,
        [FromQuery] bool filterContainedIn = false)
    {
        var linkedLicences =
            await outputService.GetLinkedLicencesAsync(permitNumber);

        if (linkedLicences == null)
        {
            return NotFound();
        }

        var filtered = linkedLicences
            .Where(ll => ll.ContainedIn?.Any(cc => cc.Direction == InformationDirection.Outgoing) == true);

        if (filterContainedIn)
        {
            // Only return the outgoing links even if there are also incoming links for the same licence
            foreach (var licence in filtered)
            {
                licence.ContainedIn = licence.ContainedIn!
                    .Where(c => c.Direction == InformationDirection.Outgoing)
                    .ToArray();
            }
        }

        return Ok(filtered);
    }

    [HttpGet]
    public async Task<IActionResult> GetAbstractionAsync([FromQuery] string permitNumber)
    {
        var linkedLicences =
            await outputService.GetLinkedLicencesAsync(permitNumber);

        if (linkedLicences == null)
        {
            return NotFound();
        }

        var filtered = linkedLicences
            .Where(ll => ll.LicenceType is LicenceType.SurfaceWaterAbstraction
                or LicenceType.GroundWaterAbstraction
                or LicenceType.Abstraction);

        return Ok(filtered);
    }

    [HttpGet]
    public async Task<IActionResult> GetImpoundmentAsync([FromQuery] string permitNumber)
    {
        var linkedLicences =
            await outputService.GetLinkedLicencesAsync(permitNumber);

        if (linkedLicences == null)
        {
            return NotFound();
        }

        var filtered = linkedLicences
            .Where(ll => ll.LicenceType == LicenceType.Impoundment);

        return Ok(filtered);
    }
}