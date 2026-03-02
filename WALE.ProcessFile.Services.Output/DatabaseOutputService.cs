using System.Text.Json;
using SkiaSharp;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Services.Output;

public class DatabaseOutputService(
    IDatabaseReadService databaseReadService,
    IDatabaseWriteService databaseWriteService) : IOutputService
{
    public string? OutputFolder { get; set; } = null;
    
    public Task SetupAsync()
    {
        // Nothing to do in this case
        return Task.CompletedTask;
    }

    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath)
    {
        return ImageReferenceHelper.GetPageScreenshotReferences(pageNumber, pdfServiceName, pdfFilePath);
    }

    public async Task<List<byte[]>> GetPageScreenshotDataAsync(int pageNumber, string pdfServiceName, string pdfFilePath)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath)!;
        
        var bytes1 = await databaseReadService.GetPageScreenshotAsync(
            pageNumber,
            pdfFilename,
            pdfServiceName);
        
        var bytes2 = await databaseReadService.GetPageScreenshotAsync(
            pageNumber,
            pdfFilename,
            GeneralConstants.DocnetExtractorServiceName);// TODO tidy this up
        
        return [
            bytes1!,
            bytes2!
        ];
    }

    public Task<ProcessRun> StartProcessRunAsync(ProcessRun processRun)
    {
        return databaseWriteService.AddProcessRunAsync(processRun);
    }

    public async Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, string pdfFilePath, int processRunId)
    {
        foreach (var licenceSetKvp in licenceSets)
        {
            var licenceSet = licenceSetKvp.Value;
            
            var licenceSetId = await databaseWriteService.SaveLicenceSetAsync(
                licenceSet.LicenceSetId,
                licenceSet.ShortLicenceSetId,
                processRunId);   
            
            foreach (var licence in licenceSet.Licences)
            {
                var licenceId = licence.NoneSchemaData.TryGetValue("licenceId", out var licenceIdOut)
                    ? (int?)licenceIdOut
                    : null;
                
                if (string.IsNullOrEmpty(licence.LicenceNumber?.Value) && licenceId == null)
                {
                    // TODO log
                    continue;
                }
                
                await databaseWriteService.InsertLicenceSetLicenceAsync(
                    licenceSetId,
                    licenceId,
                    licence.LicenceNumber?.Value,
                    licence.LicenceVersion.LicenceVersionId,
                    processRunId);   
            }

            foreach (var licenceSetType in licenceSet.LicenceSetTypes)
            {
                await databaseWriteService.SaveLicenceSetTypeAsync(
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
                await databaseWriteService.SaveAggregateSetAsync(
                    licenceSetId,
                    aggregateSet.AggregateSetId,
                    JsonSerializer.Serialize(aggregateSet.Aggregates, JsonHelper.GetSerializerOptions()),
                    processRunId);
            }
        }
    }

    public Task UpdateLicenceAsync(Licence licence, int licenceId, string? pdfFilePath, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        var licenceStr = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions());
        
        return databaseWriteService.UpdateLicenceAsync(licenceId, licenceStr, pdfFilename, processRunId);
    }
    
    public Task<int> SaveLicenceAsync(Licence licence, string? pdfFilePath, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        var licenceStr = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions());
        
        return databaseWriteService.SaveLicenceAsync(
            licence.LicenceNumber?.Value,
            licenceStr,
            pdfFilename,
            processRunId);
    }
    
    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)
    {
        var matchStr = JsonSerializer.Serialize(data, JsonHelper.GetSerializerOptions());
        return databaseWriteService.SaveMatchAsync(matchesResultId, labelName, labelGroupName, matchStr);
    }

    public Task<int> SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath)!;
        var matchesResultStr = JsonSerializer.Serialize(matchesResult, JsonHelper.GetSerializerOptions());
        
        return databaseWriteService.SaveMatchesResultAsync(matchesResultStr, pdfFilename, processRunId);
    }
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        // Don't need to, as it can just get it on the fly
        return Task.CompletedTask;
    }

    public async Task<int> SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        string pdfFilePath,
        int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath)!;
        var images = pdfDocument.GetPageAsSkBitmap(pageNumber, noOcrServiceName);

        foreach (var (providerName, bitmap) in images)
        {
            var bytes = await GetAsJpegAsync(bitmap);

            await SavePageScreenshotInternalAsync(
                pageNumber,
                noOcrServiceName,
                pdfFilename,
                bytes,
                processRunId);
        }
        
        return images.Sum(i => i.Bitmap.ByteCount);
    }

    public async Task SavePageScreenshotInternalAsync(
        int pageNumber,
        string noOcrServiceName,
        string pdfFilename,
        byte[] data,
        int processRunId)
    {
        await databaseWriteService.SavePageScreenshotAsync(
            pageNumber,
            noOcrServiceName,
            pdfFilename,
            data,
            processRunId);
    }

    public async Task SaveAllPagesTextAsync(List<DocumentLine> documentLines, string pdfFilePath, string noOcrServiceName, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath)!;
        
        var documentLinesStr = JsonSerializer.Serialize(documentLines, JsonHelper.GetSerializerOptions());
        await databaseWriteService.SaveAllPagesTextAsync(documentLinesStr, pdfFilename, noOcrServiceName, processRunId);
    }

    public async Task FinishProcessRunAsync(ProcessRun processRun, int regionId)
    {
        // Fix up missing LicenceIds in LicenceSetList
        var licenceSetLicences = await databaseReadService.GetLicenceSetLicencesAsync(
            processRun.ProcessRunId);

        var missingLicenceIds = licenceSetLicences.Where(lsl => lsl.LicenceId == null);

        foreach (var missingLicenceId in missingLicenceIds)
        {
            if (missingLicenceId.LicenceNumber == null)
            {
                continue;
            }

            var licenceTransformed = FormattingHelper.FormatLicenceNumber(missingLicenceId.LicenceNumber, regionId)!;

            var licence =
                await databaseReadService.GetLicenceAsync(licenceTransformed, processRun.ProcessRunId);

            if (licence == null)
            {
                // TODO log - shouldn't happen
                continue;
            }
            
            missingLicenceId.LicenceId = (int)licence.NoneSchemaData["licenceId"]!;
            await databaseWriteService.UpdateLicenceSetLicenceAsync(missingLicenceId);
        }
        
        await databaseWriteService.UpdateProcessRunAsync(processRun);
    }

    public Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        return databaseReadService.GetProcessRunsAsync();
    }

    public Task<Licence?> GetLicenceAsync(string filename)
    {
        return databaseReadService.GetLicenceAsync(filename);
    }
    
    public Task<MatchesResult?> GetMatchesResult(string filename)
    {
        return databaseReadService.GetMatchesResult(filename);
    }
    
    public async Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        var licences = await databaseReadService.GetLicencesAsync(processRunId);

        foreach (var licence in licences)
        {
            licence.NoneSchemaData = JsonHelper.MakeJsonElementDictionaryNative(
                licence.NoneSchemaData);
        }
        
        return licences;
    }

    public async Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(int processRunId, List<Licence> allLicences)
    {
        var licenceSets = await databaseReadService.GetLicenceSetsSimpleAsync(processRunId);
        var allLicenceSetLicences = await databaseReadService.GetLicenceSetLicencesAsync(processRunId);
        var allLicenceSetTypes = await databaseReadService.GetLicenceSetTypesForProcessRun(processRunId);
        var allAggregateSets = await databaseReadService.GetAggregateSetsForProcessRun(processRunId);
        
        var returnList = new Dictionary<string, LicenceSet>();
        
        foreach (var licenceSetSimple in licenceSets)
        {
            var licenceSet = new LicenceSet();
            
            var licenceSetLicenceIds = allLicenceSetLicences
                .Where(lsl => lsl.LicenceSetId == licenceSetSimple.LicenceSetId);
            
            var licences = new List<Licence>();

            foreach (var licenceSetLicence in licenceSetLicenceIds)
            {
                var licence = allLicences.FirstOrDefault(l =>
                {
                    var licenceId = (int)l.NoneSchemaData["licenceId"]!;
                    return licenceId == licenceSetLicence.LicenceId;
                });

                if (licence == null)
                {
                    continue;
                }
                
                licence.LicenceVersion.SetExplicitLicenceVersionId(licenceSetLicence.LicenceVersionId!);
                licences.Add(licence);
            }

            licenceSet.Licences = licences
                .ToArray();
            
            licenceSet.LicenceSetTypes = allLicenceSetTypes
                .Where(lst => lst.LicenceSetId == licenceSetSimple.LicenceSetId)
                .Select(lst => lst.Type)
                .ToArray();
            
            licenceSet.AggregateSets = allAggregateSets
                .Where(lst => lst.LicenceSetId == licenceSetSimple.LicenceSetId)
                .Select(lst => lst.AggregateSet)
                .ToArray();

            returnList.TryAdd(licenceSet.LicenceSetId, licenceSet);
        }

        return returnList;
    }

    public async Task<List<LicenceSet>> GetLicenceSetsAsync(string filename)
    {
        var processRun = (await databaseReadService.GetMostRecentProcessRunAsync(filename))!;
        
        var licenceSets = await databaseReadService.GetLicenceSetsSimpleAsync(
            filename,
            processRun.ProcessRunId);
        
        var returnList = new List<LicenceSet>();
        
        foreach (var licenceSetSimple in licenceSets)
        {
            var licenceSet = new LicenceSet();
            
            var licenceSetLicenceIds =
                await databaseReadService.GetLicenceSetLicencesAsync(licenceSetSimple.LicenceSetId, processRun.ProcessRunId);

            var licences = new List<Licence>();

            foreach (var licenceSetLicence in licenceSetLicenceIds)
            {
                var licence = new Licence
                {
                    LicenceNumber = !string.IsNullOrEmpty(licenceSetLicence.LicenceNumber) 
                        ? new ValueWithConfidence<string>(licenceSetLicence.LicenceNumber)
                        : null
                };
                
                licence.LicenceVersion.SetExplicitLicenceVersionId(licenceSetLicence.LicenceVersionId!);
                licences.Add(licence);
            }

            licenceSet.Licences = licences.ToArray();
            licenceSet.LicenceSetTypes = await databaseReadService.GetLicenceSetTypes(licenceSetSimple.LicenceSetId);
            licenceSet.AggregateSets = await databaseReadService.GetAggregateSets(licenceSetSimple.LicenceSetId);
            
            returnList.Add(licenceSet);
        }

        return returnList;
    }

    private static async Task<byte[]> GetAsJpegAsync(SKBitmap bitmap, int quality = 60)
    {
        using var image = SKImage.FromBitmap(bitmap);

        if (image == null)
        {
            throw new FileNotFoundException("Could not load image");
        }
        
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        if (data == null)
        {
            throw new FileNotFoundException("Could not encode image");
        }
        
        await using var stream = new MemoryStream();
        data.SaveTo(stream);
        
        await stream.FlushAsync();

        stream.Position = 0;
        var bytes = stream.ToArray();
        stream.Close();

        return bytes;
    }
}