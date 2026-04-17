using System.Text;
using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class FilesController(IFileService fileService) : Controller
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> ListAllAsync()
    {
        var result = await fileService.GetAllFilesAsync();
        return Ok(result);
    }
    
    [HttpPut]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600, ValueLengthLimit = 20_971_520)] // 100Mb for all files, 20Mb per file
    public async Task<ActionResult<string>> UploadAsync()
    {
        if (!Request.Form.Files.Any())
        {
            return BadRequest();
        }

        var resultSb = new StringBuilder();
        
        foreach (var file in Request.Form.Files)
        {
            if (!file.ContentType.Equals("application/pdf", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }
         
            var fileExtension = Path.GetExtension(file.FileName);
            
            if (!fileExtension.Equals(".pdf", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }
            
            using MemoryStream stream = new();
            await file.CopyToAsync(stream);
            
            await fileService.UploadFileAsStreamAsync(file.FileName, stream);
            resultSb.AppendLine($"File {file.FileName} has been uploaded.");
        }

        if (resultSb.Length == 0)
        {
            resultSb.Append("No files were uploaded.");
        }
        
        return Ok(resultSb.ToString());
    }
}