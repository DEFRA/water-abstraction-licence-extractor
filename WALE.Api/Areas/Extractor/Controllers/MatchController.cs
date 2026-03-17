using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> SaveAsync([FromBody] SaveRequest request)
    {
        var labelGroupResult = JsonSerializer.Deserialize<LabelGroupResult>(
            request.data!,
            JsonHelper.GetSerializerOptions())!;
        
        await outputService.SaveMatchAsync(
            request.matchesResultId,
            request.labelName!,
            request.labelGroupName!,
            labelGroupResult);
        
        return Ok();
    }

    public class SaveRequest
    {
        public int matchesResultId { get; set; }
        public string? labelName { get; set; }
        public string? labelGroupName { get; set; }
        public string? data { get; set; }
    }
}