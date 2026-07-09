using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class FilesController(IFileService fileService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetAsync(
        [FromQuery] string filename,
        [FromQuery] int chunkIndex = 0,
        [FromQuery] int chunkSize = int.MaxValue)
    {
        var chunkSet = chunkSize != 0;
        var chunkSizeSet = chunkSize != int.MaxValue;
        
        if (chunkSet || chunkSizeSet)
        {
            var bytes = await fileService.GetFileAsBytesAsync(filename, chunkIndex, chunkSize);
            
            if (bytes == null)
            {
                throw new Exception("No data found");
            }
            
            return File(bytes, "application/octet-stream");
        }
        
        var data = await fileService.GetFileAsStreamAsync(filename);

        if (data == null)
        {
            throw new Exception("Stream is null");
        }
        
        return File(data, "application/pdf");
    }
    
    [HttpGet]
    public async Task<ActionResult> ExistsAsync([FromQuery] string filename)
    {
        var data = await fileService.ExistsAsync(filename);
        return Ok(data);
    }
}