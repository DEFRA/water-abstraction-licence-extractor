using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class LicenceController(
    IAbstractionLicenceOutputService abstractionLicenceOutputService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] int processRunId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue)
    {
        var licences = await abstractionLicenceOutputService.GetLicencesAsync(
            processRunId,
            skip,
            take);
        
        return Ok(licences);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetByFileIdAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int processRunId,
        [FromQuery] bool applyVerifications = false)
    {
        var licence = await abstractionLicenceOutputService.GetLicenceAsync(fileId, processRunId, applyVerifications);
        return Ok(licence);
    }

    [HttpGet]
    public async Task<IActionResult> GetByLicenceNumberAsync(
        [FromQuery] string licenceNumber,
        [FromQuery] int processRunId,
        [FromQuery] bool applyVerifications = false)
    {
        var licence = await abstractionLicenceOutputService.GetLicenceAsync(licenceNumber, processRunId, applyVerifications);
        return Ok(licence);
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveAsync(
        [FromBody] SaveLicenceRequest request)
    {
        var licence = JsonSerializer.Deserialize<Licence>(
            request.licence!,
            JsonHelper.GetSerializerOptions())!;
        
        var returnId = await abstractionLicenceOutputService.SaveLicenceAsync(
            licence,
            request.processRunId);

        return Ok(returnId);
    }
    
    [HttpPost]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] UpdateLicenceRequest request)
    {
        var licence = JsonSerializer.Deserialize<Licence>(
            request.licence!,
            JsonHelper.GetSerializerOptions())!;
        
        await abstractionLicenceOutputService.UpdateLicenceAsync(
            licence,
            request.licenceId,
            request.processRunId);

        return Ok();
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveLicenceSetAsync(
        [FromBody] SaveLicenceSetRequest request)
    {
        var licenceSet = JsonSerializer.Deserialize<LicenceSet>(
            request.licenceSet!,
            JsonHelper.GetSerializerOptions())!;

        foreach (var licence in licenceSet.Licences)
        {
            licence.NoneSchemaData = JsonHelper.MakeJsonElementDictionaryNative(
                licence.NoneSchemaData);
        }
        
        await abstractionLicenceOutputService.SaveLicenceSetAsync(
            licenceSet,
            request.fileId,
            request.processRunId);

        return Ok();
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveLicenceSetsAsync(
        [FromBody] SaveLicenceSetsRequest request)
    {
        var licenceSets = JsonSerializer.Deserialize<Dictionary<string, LicenceSet>>(
            request.licenceSets!,
            JsonHelper.GetSerializerOptions())!;

        foreach (var licenceSet in licenceSets)
        {
            foreach (var licence in licenceSet.Value.Licences)
            {
                licence.NoneSchemaData = JsonHelper.MakeJsonElementDictionaryNative(
                    licence.NoneSchemaData);
            }
        }
        
        await abstractionLicenceOutputService.SaveLicenceSetsAsync(
            licenceSets,
            request.fileId,
            request.processRunId);

        return Ok();
    }
}