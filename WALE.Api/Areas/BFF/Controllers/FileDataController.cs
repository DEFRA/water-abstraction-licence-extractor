using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class FileDataController(IOutputService outputService) : Controller
{
    [HttpGet]
    public async Task<ActionResult<MatchesResult?>> MatchesResult([FromQuery] string filename)
    {
        var result = await outputService.GetMatchesResult(filename);
        return Ok(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<Licence?>> Licence([FromQuery] string filename)
    {
        var result = await outputService.GetLicenceAsync(filename);
        return Ok(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenceSet>>> LicenceSets([FromQuery] string filename)
    {
        var results = await outputService.GetLicenceSetsAsync(filename);
        return Ok(results);
    }
}