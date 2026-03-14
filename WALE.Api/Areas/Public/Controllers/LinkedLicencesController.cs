using Microsoft.AspNetCore.Mvc;
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
}