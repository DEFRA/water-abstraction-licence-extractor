using System.Text.Json;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class SharedHelper
{
    /// <summary>
    /// (everything before first underscore)
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static string? ExtractPermitNumberFromFilename(string filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return null;
        }

        // Remove file extension first
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

        // Find first underscore and extract everything before it
        var underscoreIndex = nameWithoutExtension.IndexOf('_');

        if (underscoreIndex > 0)
        {
            return nameWithoutExtension[..underscoreIndex].Replace(" ", string.Empty);
        }

        // If no underscore found, return the whole filename without extension
        return nameWithoutExtension.Replace(" ", string.Empty);
    }
    
    public static Dictionary<string, LicenceSet> GetLicenceSetsForLicenceSetIds(
        IReadOnlyList<LicenceSetReference> licenceSetIds,
        IReadOnlyList<LicenceSet> licenceSets)
    {
        var returnDict = new Dictionary<string, LicenceSet>();

        foreach (var licenceSet in licenceSets)
        {
            if (licenceSetIds.All(lsi => lsi.LicenceSetId != licenceSet.LicenceSetId))
            {
                continue;
            }

            returnDict.TryAdd(licenceSet.LicenceSetId, licenceSet);
        }

        return returnDict;
    }
    
    public static async Task<NaldDataCollection> GetNaldDataAsync(
        short? regionCode,
        IAbstractionLicenceCacheService cacheService)
    {
        var dtStart = DateTime.Now;
        ConsoleHelper.WriteLine($"INFO - {nameof(SharedHelper)} - Started getting NALD data");
        
        const int take = 10_000;
        var allNaldData = new NaldDataCollection
        {
            AbstractionAndImpoundmentLicences = [],
            AbstractionLicencePoints = [],
            AbstractionLicencePurposes = [],
            AbstractionLicenceQuantities = [],
            AbstractionLicences = [],
            AbstractionLicenceVersions = []
        };

        var allNaldDataPartial = new NaldDataCollection();
        var loopIdx = 0;

        while (loopIdx == 0
               || allNaldDataPartial.AbstractionAndImpoundmentLicences!.Count == take
               || allNaldDataPartial.AbstractionLicencePoints!.Count == take
               || allNaldDataPartial.AbstractionLicencePurposes!.Count == take
               || allNaldDataPartial.AbstractionLicenceQuantities!.Count == take
               || allNaldDataPartial.AbstractionLicences!.Count == take
               || allNaldDataPartial.AbstractionLicenceVersions!.Count == take)
        {
            var skip = take * loopIdx++;
                
            allNaldDataPartial = await cacheService.GetNaldDataAsync(regionCode, false, skip, take);
            allNaldData.AbstractionAndImpoundmentLicences!.AddRange(allNaldDataPartial.AbstractionAndImpoundmentLicences!);
            allNaldData.AbstractionLicencePoints!.AddRange(allNaldDataPartial.AbstractionLicencePoints!);
            allNaldData.AbstractionLicencePurposes!.AddRange(allNaldDataPartial.AbstractionLicencePurposes!);
            allNaldData.AbstractionLicenceQuantities!.AddRange(allNaldDataPartial.AbstractionLicenceQuantities!);
            allNaldData.AbstractionLicences!.AddRange(allNaldDataPartial.AbstractionLicences!);
            allNaldData.AbstractionLicenceVersions!.AddRange(allNaldDataPartial.AbstractionLicenceVersions!);
        }
        
        var saveDuration = (DateTime.Now - dtStart).TotalMilliseconds;
        ConsoleHelper.WriteLine($"INFO - {nameof(SharedHelper)} - Finished getting NALD data in {saveDuration}ms");
        
        return allNaldData;
    }

    public static async Task UpdateAndSaveLicenceSetsAsync(
        List<IReadOnlyList<LicenceSet>> licenceSetGroups,
        List<LicenceSet> allLicenceSets,
        IAbstractionLicenceOutputService outputService,
        ProcessRun processRun)
    {
        var savedLicenceSetIds = new HashSet<string>();

        foreach (var licenceSetGroup in licenceSetGroups)
        {
            if (licenceSetGroup.Count == 0)
            {
                // This shouldn't happen
                ConsoleHelper.WriteLine($"WARNING - {nameof(SharedHelper)} - Empty licence set group found");
                continue;
            }

            foreach (var licenceSetLoop in licenceSetGroup)
            {
                foreach (var licenceLoop in licenceSetLoop.Licences)
                {
                    var licenceNumber = licenceLoop.LicenceNumber?.Value;
                    var hasLicenceNumber = !string.IsNullOrEmpty(licenceNumber);

                    Licence? existingLicence = null;
                    
                    if (hasLicenceNumber)
                    {
                        existingLicence = await outputService.GetLicenceAsync(
                            licenceNumber!,
                            processRun.ProcessRunId);
                    }
                    
                    var existingLicenceId = existingLicence?.NoneSchemaData.ContainsKey("licenceId") == true
                        ? GetInt32(existingLicence.NoneSchemaData["licenceId"]!)
                        : (int?)null;
                    
                    var previouslySavedForLicenceNumber = existingLicenceId != null;
                    
                    if (existingLicence != null && !previouslySavedForLicenceNumber)
                    {
                        ConsoleHelper.WriteLine($"WARNING - {nameof(SharedHelper)} - Licence exists (by filename) but doesnt have the none schema data set");
                    }

                    var hasFileId = licenceLoop.DmsFileId.HasValue;
                    var previouslySavedForFileIdOnly = false;
                    
                    if (existingLicence == null && !hasLicenceNumber && hasFileId)
                    {
                        ConsoleHelper.WriteLine($"INFO - {nameof(SharedHelper)} - No licence number, looking up by fileid ({licenceLoop.DmsFileId})");
                        
                        existingLicence = await outputService.GetLicenceAsync(
                            licenceLoop.DmsFileId!.Value,
                            processRun.ProcessRunId);
                        
                        existingLicenceId = existingLicence?.NoneSchemaData.ContainsKey("licenceId") == true
                            ? GetInt32(existingLicence.NoneSchemaData["licenceId"]!)
                            : null;
                        
                        previouslySavedForFileIdOnly = existingLicenceId != null;
                        
                        if (existingLicence != null && !previouslySavedForFileIdOnly)
                        {
                            ConsoleHelper.WriteLine($"WARNING - {nameof(SharedHelper)} - Licence exists (by file id) but doesnt have the none schema data set");
                        }
                    }
                    
                    if (previouslySavedForLicenceNumber || previouslySavedForFileIdOnly)
                    {
                        var licenceFoundLocally = licenceLoop.Status == ScrapeStatus.Ok;
                        var previouslySavedAsNotFound = existingLicence!.Status == ScrapeStatus.NotFound;
                        
                        if (licenceFoundLocally && previouslySavedAsNotFound)
                        {
                            await outputService.UpdateLicenceAsync(
                                licenceLoop,
                                existingLicenceId!.Value,
                                processRun.ProcessRunId);
                        }
                    }
                    else
                    {
                        var licenceId = await outputService.SaveLicenceAsync(
                            licenceLoop,
                            processRun.ProcessRunId);
                        
                        licenceLoop.NoneSchemaData["licenceId"] = licenceId;
                    }
                    
                    var licenceSetsLoop = GetLicenceSetsForLicenceSetIds(
                        licenceLoop.LicenceSets,
                        allLicenceSets);

                    var newLicenceSetsLoop = new Dictionary<string, LicenceSet>();

                    foreach (var kvp in licenceSetsLoop.Where(kvp => !savedLicenceSetIds.Contains(kvp.Key)))
                    {
                        newLicenceSetsLoop.Add(kvp.Key, kvp.Value);
                        savedLicenceSetIds.Add(kvp.Key);
                    }

                    foreach (var licenceSet in newLicenceSetsLoop)
                    {
                        // Not batched as we get a 413 on the server
                        await outputService.SaveLicenceSetAsync(
                            licenceSet.Value,
                            licenceLoop.DmsFileId,
                            processRun.ProcessRunId);   
                    }
                }
            }
        }
    }

    private static int GetInt32(object jsonObject)
    {
        if (jsonObject is JsonElement jsonElement)
        {
            return jsonElement.GetInt32();
        }
        
        return (int)jsonObject;
    }

    public static Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync(
        IAbstractionLicenceCacheService cacheService)
    {
        return cacheService.GetNaldImpoundmentAndAbstractionLicencesAsync();
    }
}