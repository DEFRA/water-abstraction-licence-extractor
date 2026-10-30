using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class MatchResultController(
    IOutputService outputService,
    IDocumentTextService documentTextService) : Controller
{
    [HttpPost]
    public async Task<IActionResult> SaveAsync([FromBody] SaveMatchResultRequest request)
    {
        var matchResultId = await outputService.SaveMatchResultAsync(
            request.matches!,
            request.fileId,
            request.processRunId,
            request.isUpdate);

        await PopulateDocumentTextAsync(request.fileId);

        return Ok(matchResultId);
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveStubAsync([FromBody] SaveStubMatchResultRequest request)
    {
        var matchResultId = await outputService.SaveStubMatchesResultAsync(
            request.filename!,
            request.fileId,
            request.processRunId);
        
        return Ok(matchResultId);
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveErrorAsync([FromBody] SaveErrorMatchResultRequest request)
    {
        var matchResultId = await outputService.SaveErrorMatchesResultAsync(
            request.filename!,
            request.fileId,
            request.processRunId,
            request.error,
            request.isUpdate);
        
        return Ok(matchResultId);
    }

    private async Task PopulateDocumentTextAsync(Guid fileId)
    {
        try
        {
            await documentTextService.StoreDocumentTextAsync(fileId);
        }
        catch (Exception exception)
        {
            ConsoleHelper.WriteLine(
                $"ERROR - {nameof(MatchResultController)} - could not store document text for " +
                $"{fileId} - {exception.Message}");
        }
    }
}
