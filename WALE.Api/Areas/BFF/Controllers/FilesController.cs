using System.Text;
using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class FilesController(IFileService fileService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetAsync(
        [FromQuery] string filename,
        [FromQuery] string? folder = null)
    {
        var isAssets = "assets".Equals(folder, StringComparison.OrdinalIgnoreCase);
        
        var presignedUrl = await fileService.GetPresignedUrlAsync(
            filename,
            isAssets ? StorageFolder.Assets : StorageFolder.Ingress);
        
        return Redirect(presignedUrl);
    }

    // GetAsync's redirect target for IFileService implementations with no real presigned-URL
    // concept (e.g. LocalFileService, for local-filesystem-backed dev/testing) - streams the
    // file's own bytes directly instead.
    [HttpGet]
    public async Task<ActionResult> GetRawAsync([FromQuery] string filename)
    {
        var stream = await fileService.GetFileAsStreamAsync(filename);
        if (stream == null) return NotFound();

        return File(stream, "application/pdf", filename);
    }

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
    
    /// <summary>
    /// Only PDFs reach the ingress bucket, matching UploadChunkAsync. A file this refuses is
    /// reported as a refusal rather than skipped: the caller sends one file per request and reads
    /// nothing but the status code, so a 200 on a file that was never stored cannot be told apart
    /// from a successful upload.
    /// </summary>
    [HttpPut]
    [DisableRequestSizeLimit]
    [RequestFormLimits(
        MultipartBodyLengthLimit = 1_048_576_000,
        ValueLengthLimit = 83_886_080)] // 1Gb for all files, 80Mb per file
    public async Task<ActionResult<string>> UploadAsync()
    {
        if (!Request.Form.Files.Any())
        {
            return BadRequest("No file in request.");
        }

        var uploaded = new List<string>();
        var alreadyPresent = new List<string>();
        var refused = new List<string>();

        foreach (var file in Request.Form.Files)
        {
            var lowercaseFileName = file.FileName.ToLowerInvariant();
            var fileExtension = Path.GetExtension(lowercaseFileName);

            if (!fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                || !file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                refused.Add(file.FileName);
                continue;
            }

            if (await fileService.ExistsAsync(lowercaseFileName, StorageFolder.Ingress))
            {
                alreadyPresent.Add(file.FileName);
                continue;
            }

            using MemoryStream stream = new();
            await file.CopyToAsync(stream);

            await fileService.UploadFileAsStreamAsync(
                lowercaseFileName,
                stream,
                "application/pdf",
                StorageFolder.Ingress);

            uploaded.Add(file.FileName);
        }

        // Nothing stored and something refused is a failed request, so the caller's status check
        // catches it. A file already in the bucket is not a refusal - re-uploading is a no-op, not
        // an error.
        if (uploaded.Count == 0 && refused.Count > 0)
        {
            return BadRequest(Describe(uploaded, alreadyPresent, refused));
        }

        return Ok(Describe(uploaded, alreadyPresent, refused));
    }

    /// <summary>
    /// What became of each file. Names the refused ones, since a count alone does not say which
    /// file to go and convert.
    /// </summary>
    private static string Describe(
        IReadOnlyCollection<string> uploaded,
        IReadOnlyCollection<string> alreadyPresent,
        IReadOnlyCollection<string> refused)
    {
        var description = new StringBuilder();

        description.Append($"{uploaded.Count} uploaded");

        if (alreadyPresent.Count > 0)
        {
            description.Append($", {alreadyPresent.Count} already present");
        }

        if (refused.Count > 0)
        {
            description.Append($", {refused.Count} not a PDF and refused: ")
                .Append(string.Join(", ", refused));
        }

        return description.Append('.').ToString();
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