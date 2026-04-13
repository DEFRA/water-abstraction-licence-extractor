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
    public async Task<ActionResult<MatchesResult?>> MatchesResult([FromQuery] Guid fileId)
    {
        var result = await outputService.GetMatchesResult(fileId);
        return Ok(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<Licence?>> Licence([FromQuery] Guid fileId)
    {
        var result = await outputService.GetLicenceAsync(fileId);
        return Ok(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenceSet>>> LicenceSets([FromQuery] Guid fileId)
    {
        var results = await outputService.GetLicenceSetsAsync(fileId);
        return Ok(results);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenceSectionVerification>>> LicenceSectionVerifications([FromQuery] Guid licenceFileId, [FromQuery] int processRunId)
    {
        var results = await outputService.GetLicenceSectionVerificationsAsync(licenceFileId, processRunId);
        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult<int>> CreateLicenceSectionVerification([FromBody] LicenceSectionVerification verification)
    {
        var result = await outputService.SaveLicenceSectionVerificationAsync(verification);
        return Ok(result);
    }
}