using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> GetAllAsync([FromQuery] int processRunId)
    {
        var licences = await outputService.GetLicencesAsync(processRunId);
        return Ok(licences);
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
            request.fileId!,
            request.processRunId);

        return Ok();
    }
    
    public class SaveLicenceSetsRequest
    {
        public string? licenceSets { get; set; }
        
        public Guid fileId { get; set; }
        
        public int processRunId { get; set; }
    }
    
    public class SaveLicenceRequest
    {
        public Guid fileId { get; set; }
        public int processRunId { get; set; }
        public string? licence  { get; set; }
    }
}