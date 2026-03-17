using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Public.Controllers;

[ApiController]
[Area("Public")]
[Route("/[area]/[controller]/[action]")]
public class LinkedLicencesController(IOutputService outputService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] string permitNumber)
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
    public async Task<IActionResult> GetIncomingAsync([FromQuery] string permitNumber)
    {
        var linkedLicences =
            await outputService.GetLinkedLicencesAsync(permitNumber);

        if (linkedLicences == null)
        {
            return NotFound();
        }

        var filtered = linkedLicences
            .Where(ll => ll.ContainedIn!.Any(cc => cc.Direction == LinkedLicenceDirection.Incoming));
        
        return Ok(filtered);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetOutgoingAsync([FromQuery] string permitNumber)
    {
        var linkedLicences =
            await outputService.GetLinkedLicencesAsync(permitNumber);

        if (linkedLicences == null)
        {
            return NotFound();
        }
        
        var filtered = linkedLicences
            .Where(ll => ll.ContainedIn!.Any(cc => cc.Direction == LinkedLicenceDirection.Outgoing));
        
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
            .Where(ll => ll.LicenceType == LicenceType.Abstraction);
        
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