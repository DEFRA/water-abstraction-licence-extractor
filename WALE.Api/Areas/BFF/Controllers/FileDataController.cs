using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class FileDataController(
    IOutputService outputService,
    IAbstractionLicenceOutputService abstractionLicenceOutputService) : Controller
{
    [HttpGet]
    public async Task<ActionResult<MatchesResult?>> MatchesResult([FromQuery] Guid fileId)
    {
        var result = await outputService.GetMatchesResultAsync(fileId);
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
    
    [HttpGet]
    public async Task<ActionResult<Licence?>> Licence([FromQuery] Guid fileId, [FromQuery] int processRunId)
    {
        var result = await abstractionLicenceOutputService.GetLicenceAsync(fileId, processRunId);
        return Ok(result);
    }
    
    // This version of the method just here so the generated TS client doesn't mangle some properties
    [HttpGet]
    public async Task<ActionResult<string?>> LicenceStringAsync([FromQuery] Guid fileId, [FromQuery] int processRunId)
    {
        var result = await abstractionLicenceOutputService.GetLicenceAsync(fileId, processRunId);
        return Ok(JsonSerializer.Serialize(result, JsonHelper.GetSerializerOptions()));
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenceSet>>> LicenceSets([FromQuery] Guid fileId)
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
    public async Task<ActionResult<int>> CreateLicenceSectionVerification(
        [FromBody] LicenceSectionVerification verification)
    {
        var result = await abstractionLicenceOutputService.SaveLicenceSectionVerificationAsync(verification);
        return Ok(result);
    }
}