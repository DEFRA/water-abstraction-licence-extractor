using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

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
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FileMetadata>>> ListAllWithMetadataAsync(
        [FromQuery] string? startAfter,
        [FromQuery] int take)
    {
        var result = await fileService.GetAllFilesWithMetadataAsync(
            startAfter ?? string.Empty,
            take);
        
        return Ok(result);
    }
    
    [HttpDelete]
    public async Task<ActionResult<string>> DeleteAsync(string filename)
    {
        await fileService.DeleteAsync(filename);
        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult<string>> RenameAsync(
        [FromBody] RenameRequest request)
    {
        await fileService.RenameAsync(request.originalFilename!, request.newFilename!);
        return Ok();
    }
    
    [HttpPut]
    [DisableRequestSizeLimit]
    [RequestFormLimits(
        MultipartBodyLengthLimit = 1_048_576_000,
        ValueLengthLimit = 83_886_080)] // 1Gb for all files, 80Mb per file
    public async Task<ActionResult<string>> UploadAsync()
    {
        if (!Request.Form.Files.Any())
        {
            return BadRequest();
        }

        var resultSb = new StringBuilder();
        
        foreach (var file in Request.Form.Files)
        {
            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
         
            var lowercaseFileName = file.FileName.ToLowerInvariant();
            var fileExtension = Path.GetExtension(lowercaseFileName);
            
            if (!fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            if (await fileService.ExistsAsync(lowercaseFileName))
            {
                continue;
            }
            
            using MemoryStream stream = new();
            await file.CopyToAsync(stream);
            
            await fileService.UploadFileAsStreamAsync(lowercaseFileName, stream);
            resultSb.AppendLine($"File {lowercaseFileName} has been uploaded.");
        }

        if (resultSb.Length == 0)
        {
            resultSb.Append("No files were uploaded.");
        }
        
        return Ok(resultSb.ToString());
    }

    [HttpPut]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<string>> UploadChunkAsync(
        [FromForm] string filename,
        [FromForm] int chunkIndex,
        [FromForm] int totalChunks,
        [FromForm] string? uploadId = null)
    {
        if (!Request.Form.Files.Any())
        {
            return BadRequest("No file in request.");
        }

        var file = Request.Form.Files[0];

        if (!file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Expects binary stream.");
        }

        var fileExtension = Path.GetExtension(filename);
        if (!fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only PDF files are allowed.");
        }

        using MemoryStream stream = new();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        var resultUploadId = await fileService.UploadFileChunkAsync(filename, stream, chunkIndex, totalChunks, uploadId);

        return Ok(resultUploadId ?? $"Chunk {chunkIndex + 1}/{totalChunks} of {filename} has been uploaded.");
    }
}