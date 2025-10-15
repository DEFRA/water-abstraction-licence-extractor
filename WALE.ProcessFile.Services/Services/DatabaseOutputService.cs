using System.Text.Json;
using SkiaSharp;
using UglyToad.PdfPig.Graphics.Colors;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Services;

public class DatabaseOutputService(
    IDatabaseReadService databaseReadService,
    IDatabaseAddService databaseAddService) : IOutputService
{
    public Task SetupAsync()
    {
        // Nothing to do in this case
        return Task.CompletedTask;
    }

    public Task<string> GetPageScreenshotReferenceAsync(int pageNumber, string pdfServiceName,
        string pdfFilePath)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        return Task.FromResult($"Screenshot-{pdfFilename}-{pdfServiceName}-{pageNumber}");
    }
    
    public Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun)
    {
        return databaseAddService.AddProcessRunAsync(processRun);
    }

    public async Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath, int processRunId)
    {
        foreach (var licenceSet in licenceSets)
        {
            var licenceSetId = await databaseAddService.SaveLicenceSetAsync(
                licenceSet.LicenceSetId,
                licenceSet.ShortLicenceSetId,
                processRunId);   
            
            foreach (var licence in licenceSet.Licences)
            {
                await databaseAddService.SaveLicenceSetLicenceAsync(
                    licenceSetId,
                    licence.LicenceNumber,
                    licence.LicenceVersion.LicenceVersionId,
                    processRunId);   
            }

            foreach (var licenceSetType in licenceSet.LicenceSetTypes)
            {
                await databaseAddService.SaveLicenceSetTypeAsync(
                    licenceSetId,
                    (int)licenceSetType,
                    processRunId);   
            }

            if (licenceSet.AggregateSets == null)
            {
                continue;
            }
            
            foreach (var aggregateSet in licenceSet.AggregateSets)
            {
                await databaseAddService.SaveAggregateSetAsync(
                    licenceSetId,
                    aggregateSet.AggregateSetId,
                    JsonSerializer.Serialize(aggregateSet.Aggregates, JsonHelper.GetSerializerOptions()),
                    processRunId);
            }
        }
    }

    public Task SaveLicenceAsync(Licence licence, string pdfFilePath, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        var licenceStr = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions());
        
        return databaseAddService.SaveLicenceAsync(licenceStr, pdfFilename, processRunId);
    }

    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        var matchesResultStr = JsonSerializer.Serialize(matchesResult, JsonHelper.GetSerializerOptions());
        
        return databaseAddService.SaveMatchResultAsync(matchesResultStr, pdfFilename, processRunId);
    }
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        // Don't need to, as it can just get it on the fly
        return Task.CompletedTask;
    }

    public async Task SavePageScreenshotIfDoesntExistAsync(PdfDocument pdfDocument, int pageNumber, string noOcrServiceName,
        string pdfFilePath, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        var screenshot = await databaseReadService.GetPageScreenshotAsync(pageNumber, pdfFilename, noOcrServiceName);

        if (screenshot != null)
        {
            return;
        }
        
        using var memoryStream = pdfDocument.GetPageAsSkBitmap(pageNumber, RGBColor.White);
        var bytes = await GetAsJpegAsync(memoryStream);
        
        await databaseAddService.SavePageScreenshotIfDoesntExistAsync(
            pageNumber,
            noOcrServiceName,
            pdfFilename,
            bytes,
            processRunId);
    }

    public async Task SaveAllPagesTextIfDoesntExistAsync(List<DocumentLine> documentLines, string pdfFilePath, string noOcrServiceName, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        var data = await databaseReadService.GetAllPagesTextAsync(pdfFilename, noOcrServiceName);

        if (data != null)
        {
            return;
        }
        
        var documentLinesStr = JsonSerializer.Serialize(documentLines, JsonHelper.GetSerializerOptions());
        await databaseAddService.SaveAllPagesTextIfDoesntExistAsync(documentLinesStr, pdfFilename, noOcrServiceName, processRunId);
    }

    public Task FinishProcessRunAsync(ProcessRun processRun)
    {
        return databaseAddService.UpdateProcessRunAsync(processRun);
    }

    public Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        return databaseReadService.GetProcessRunsAsync();
    }

    public Task<Licence?> GetLicenceAsync(string filename)
    {
        return databaseReadService.GetLicenceAsync(filename);
    }
    
    public Task<MatchesResult> GetMatchesResult(string filename)
    {
        return databaseReadService.GetMatchesResult(filename);
    }
    
    public Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        return databaseReadService.GetLicencesAsync(processRunId);
    }

    public async Task<List<LicenceSet>> GetLicenceSetsAsync(int processRunId)
    {
        var licenceSetIds = await databaseReadService.GetLicenceSetIdsAsync(processRunId);
        var returnList = new List<LicenceSet>();
        
        foreach (var licenceSetId in licenceSetIds)
        {
            var licenceSet = new LicenceSet();
            
            var licenceSetLicenceIds =
                await databaseReadService.GetLicenceSetLicencesAsync(licenceSetId, processRunId);

            var licences = new List<Licence>();

            foreach (var licenceSetLicence in licenceSetLicenceIds)
            {
                var licence = new Licence
                {
                    LicenceNumber = licenceSetLicence.LicenceNumber
                };
                
                licence.LicenceVersion.SetExplicitLicenceVersionId(licenceSetLicence.LicenceVersionId!);
                licences.Add(licence);
            }

            licenceSet.Licences = licences.ToArray();
            licenceSet.LicenceSetTypes = await databaseReadService.GetLicenceSetTypes(licenceSetId);
            licenceSet.AggregateSets = await databaseReadService.GetAggregateSets(licenceSetId);;
            
            returnList.Add(licenceSet);
        }

        return returnList;
    }

    public async Task<List<LicenceSet>> GetLicenceSetsAsync(string filename)
    {
        var processRun = await databaseReadService.GetMostRecentProcessRunAsync(filename);
        return await GetLicenceSetsAsync(processRun!.ProcessRunId);
    }

    private static async Task<byte[]> GetAsJpegAsync(SKBitmap bitmap, int quality = 60)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        
        await using var stream = new MemoryStream();
        data.SaveTo(stream);
        
        await stream.FlushAsync();

        stream.Position = 0;
        var bytes = stream.ToArray();
        stream.Close();

        return bytes;
    }
}