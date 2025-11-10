using System.Text.Json;
using System.Xml;
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

    public Task<byte[]?> GetPageScreenshotDataAsync(int pageNumber, string pdfServiceName, string pdfFilePath)
    {
        return databaseReadService.GetPageScreenshotAsync(pageNumber, pdfFilePath, pdfServiceName);
    }

    public Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun)
    {
        return databaseAddService.AddProcessRunAsync(processRun);
    }

    public async Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, string pdfFilePath, int processRunId)
    {
        foreach (var licenceSetKvp in licenceSets)
        {
            var licenceSet = licenceSetKvp.Value;
            
            var licenceSetId = await databaseAddService.SaveLicenceSetAsync(
                licenceSet.LicenceSetId,
                licenceSet.ShortLicenceSetId,
                processRunId);   
            
            foreach (var licence in licenceSet.Licences)
            {
                var licenceId = licence.NoneSchemaData.TryGetValue("licenceId", out var licenceIdOut)
                    ? (int?)licenceIdOut
                    : null;
                
                await databaseAddService.InsertLicenceSetLicenceAsync(
                    licenceSetId,
                    licenceId,
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

    public Task<int> SaveLicenceAsync(Licence licence, string pdfFilePath, int processRunId)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        var licenceStr = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions());
        
        return databaseAddService.SaveLicenceAsync(licence.LicenceNumber, licenceStr, pdfFilename, processRunId);
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

            var licenceTransformed = FormattingHelper.PadLicenceNumber(missingLicenceId.LicenceNumber)!;

            var licence =
                await databaseReadService.GetLicenceAsync(licenceTransformed, processRun.ProcessRunId);

            if (licence == null)
            {
                // TODO log - shouldnt happen
                continue;
            }
            
            missingLicenceId.LicenceId = (int)licence.NoneSchemaData["licenceId"];
            await databaseAddService.UpdateLicenceSetLicenceAsync(missingLicenceId);
        }
        
        await databaseAddService.UpdateProcessRunAsync(processRun);
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
    
    public Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        return databaseReadService.GetLicencesAsync(processRunId);
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