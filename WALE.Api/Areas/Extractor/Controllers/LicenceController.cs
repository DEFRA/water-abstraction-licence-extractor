using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class LicenceController(
    ICacheService cacheService,
    IOutputService outputService,
    IFileService fileService,
    IAbstractionLicenceOutputService abstractionLicenceOutputService,
    INoOcrDataExtractorService noOcrDataExtractorService,
    INoOcrPdfDocumentService noOcrPdfDocumentService,
    INoOcrAlternativePdfDocumentService noOcrAlternativePdfDocumentService) : Controller
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
    public async Task<IActionResult> GetByLicenceIdAsync(
        [FromQuery] int licenceId,
        [FromQuery] bool applyVerifications = false)
    {
        var licence = await abstractionLicenceOutputService.GetLicenceAsync(licenceId, applyVerifications);
        return Ok(licence);
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

    [HttpPost]
    public async Task<IActionResult> FilletDocumentAsync([FromBody] FilletDocumentRequest request)
    {
        if (request.pdfFileName == null)
        {
            return BadRequest($"{nameof(request.pdfFileName)} is null");
        }
        
        if (request.fileId == Guid.Empty)
        {
            return BadRequest($"{nameof(request.fileId)} is not set");
        }
        
        var pdfDocument = await noOcrDataExtractorService.GetPdfDocumentAsync(
            request.pdfFileName!,
            request.fileId,
            outputService,
            cacheService,
            noOcrPdfDocumentService,
            noOcrAlternativePdfDocumentService,
            GetMinimalLookupConfiguration(
                request.skipFileWhenMoreThenPages,
                request.regionId),
            request.processRunId,
            false);

        if (pdfDocument == null)
        {
            return BadRequest("Pdf document is not cached and could not be loaded to be filleted");
        }
        
        var rawMetadata = new MetadataCollection
        {
            AllDocumentLines = pdfDocument.AllDocumentLines!,
            ImageMetadata = pdfDocument.ImagesMetadata!,
            PagesMetadata = pdfDocument.PagesMetadata!,
            SizeBytes = pdfDocument.SizeBytes!.Value,
        };
        
        const string alreadyCached = "ALREADY_CACHED";
        const string filleted = "FILLETED";
        
        var responseObject = new FilletDocumentResponse
        {
            Result = pdfDocument.FromCache ? alreadyCached : filleted,
            Metadata = rawMetadata
        };
        
        return Ok(responseObject);
    }

    private LookupConfiguration GetMinimalLookupConfiguration(int skipFileWhenMoreThenPages, int regionId)
    {
        return new LookupConfiguration(
            [],
            [],
            fileService,
            cacheService,
            outputService,
            null!,
            null!,
            null!,
            null!,
            regionId,
            DateTime.Now,
            skipFileIfMoreThenPages: skipFileWhenMoreThenPages);
    }
}