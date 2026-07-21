using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class LicenceController(IOutputService outputService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] int processRunId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue)
    {
        var licences = await outputService.GetLicencesAsync(processRunId, skip, take);
        return Ok(licences);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetByFileIdAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int processRunId)
    {
        var licence = await outputService.GetLicenceAsync(fileId, processRunId);
        return Ok(licence);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetByLicenceNumberAsync(
        [FromQuery] string licenceNumber,
        [FromQuery] int processRunId)
    {
        var licence = await outputService.GetLicenceAsync(licenceNumber, processRunId);
        return Ok(licence);
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveAsync(
        [FromBody] SaveLicenceRequest request)
    {
        var licence = JsonSerializer.Deserialize<Licence>(
            request.licence!,
            JsonHelper.GetSerializerOptions())!;
        
        var returnId = await outputService.SaveLicenceAsync(
            licence,
            request.processRunId);

        return Ok(returnId);
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
        
        await outputService.SaveLicenceSetAsync(
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
        
        await outputService.SaveLicenceSetsAsync(
            licenceSets,
            request.fileId,
            request.processRunId);

        return Ok();
    }
}