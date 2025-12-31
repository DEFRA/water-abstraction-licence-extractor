using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;
using UglyToad.PdfPig.Graphics.Colors;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Services;

public class DatabaseOutputService(
    IDatabaseReadService databaseReadService,
    IDatabaseWriteService databaseWriteService) : IOutputService
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

    public Task<byte[]?> GetPageScreenshotDataAsync(int pageNumber, string pdfServiceName, string pdfFilePath)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath)!;
        return databaseReadService.GetPageScreenshotAsync(pageNumber, pdfFilename, pdfServiceName);
    }

    public Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun)
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
                
                if (string.IsNullOrEmpty(licence.LicenceNumber) && licenceId == null)
                {
                    // TODO log
                    continue;
                }
                
                await databaseWriteService.InsertLicenceSetLicenceAsync(
                    licenceSetId,
                    licenceId,
                    licence.LicenceNumber,
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
        
        return databaseWriteService.SaveLicenceAsync(licence.LicenceNumber, licenceStr, pdfFilename, processRunId);
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

    public async Task SavePageScreenshotIfDoesntExistAsync(PdfDocument pdfDocument, int pageNumber, string noOcrServiceName,
        string pdfFilePath, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath)!;
        var screenshot = await databaseReadService.GetPageScreenshotAsync(pageNumber, pdfFilename, noOcrServiceName);

        if (screenshot != null)
        {
            return;
        }
        
        using var memoryStream = pdfDocument.GetPageAsSkBitmap(pageNumber, RGBColor.White);
        var bytes = await GetAsJpegAsync(memoryStream);
        
        await databaseWriteService.SavePageScreenshotIfDoesntExistAsync(
            pageNumber,
            noOcrServiceName,
            pdfFilename,
            bytes,
            processRunId);
    }

    public async Task SaveAllPagesTextIfDoesntExistAsync(List<DocumentLine> documentLines, string pdfFilePath, string noOcrServiceName, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath)!;
        var data = await databaseReadService.GetAllPagesTextAsync(pdfFilename, noOcrServiceName);

        if (data != null)
        {
            return;
        }
        
        var documentLinesStr = JsonSerializer.Serialize(documentLines, JsonHelper.GetSerializerOptions());
        await databaseWriteService.SaveAllPagesTextIfDoesntExistAsync(documentLinesStr, pdfFilename, noOcrServiceName, processRunId);
    }

    public async Task FinishProcessRunAsync(ProcessRun processRun)
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

            var licenceTransformed = FormattingHelper.FormatLicenceNumber(missingLicenceId.LicenceNumber)!;

            var licence =
                await databaseReadService.GetLicenceAsync(licenceTransformed, processRun.ProcessRunId);

            if (licence == null)
            {
                // TODO log - shouldn't happen
                continue;
            }
            
            missingLicenceId.LicenceId = (int)licence.NoneSchemaData["licenceId"];
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
            var newNoneSchemaData = new Dictionary<string, object>();
            
            foreach (var kvp in licence.NoneSchemaData)
            {
                object? value;
                
                if (kvp.Value is JsonElement jsonElement)
                {
                    value = jsonElement.ValueKind switch
                    {
                        JsonValueKind.Array => jsonElement.EnumerateArray().ToList(),
                        JsonValueKind.Number => jsonElement.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String => jsonElement.GetString(),
                        JsonValueKind.Object => jsonElement.GetRawText(),
                        _ => throw new Exception($"Unexpected JSON value type {jsonElement.ValueKind}")
                    };
                }
                else if (kvp.Value is int intValue)
                {
                    value = intValue;
                }
                else if (kvp.Value is string strValue)
                {
                    value = strValue;
                }
                else
                {
                    throw new Exception($"Unknown type - {kvp.Value.GetType().Name}");
                }
                
                newNoneSchemaData.Add(kvp.Key, value!);
            }

            licence.NoneSchemaData = newNoneSchemaData;
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
            
            try
            {
                var licences = new List<Licence>();

                foreach (var licenceSetLicence in licenceSetLicenceIds)
                {
                    var licence = allLicences.FirstOrDefault(l =>
                    {
                        var licenceId = (int)l.NoneSchemaData["licenceId"];
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
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
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
                    LicenceNumber = licenceSetLicence.LicenceNumber
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