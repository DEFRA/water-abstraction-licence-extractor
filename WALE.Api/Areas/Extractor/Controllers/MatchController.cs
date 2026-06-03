using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class MatchController(IOutputService outputService) : Controller
{
    [HttpPost]
    public async Task<IActionResult> SaveAsync([FromBody] SaveMatchRequest matchRequest)
    {
        var labelGroupResult = JsonSerializer.Deserialize<LabelGroupResult>(
            matchRequest.data!,
            JsonHelper.GetSerializerOptions())!;
        
        await outputService.SaveMatchAsync(
            matchRequest.matchesResultId,
            matchRequest.labelName!,
            matchRequest.labelGroupName!,
            labelGroupResult);
        
        return Ok();
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveMultipleAsync([FromBody] SaveMatchesRequest matchesRequest)
    {
        await outputService.SaveMatchesAsync(matchesRequest.matches!);
        return Ok();
    }
}