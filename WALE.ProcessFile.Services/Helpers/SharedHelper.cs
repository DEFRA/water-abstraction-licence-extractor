using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Helpers;

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
        ICacheService cacheService)
    {
        var dtStart = DateTime.Now;
        ConsoleHelper.WriteLine("INFO - WALE.Cmd - Started getting NALD data");
        
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
        IOutputService outputService,
        ProcessRun processRun)
    {
        var savedLicenceNumbers = new Dictionary<string, int>();
        var savedLicenceFilenames = new Dictionary<string, int>();
        var savedNotFoundLicences = new Dictionary<string, int>();

        var savedLicenceSetIds = new HashSet<string>();

        foreach (var licenceSetGroup in licenceSetGroups)
        {
            if (licenceSetGroup.Count == 0)
            {
                // This shouldn't happen
                ConsoleHelper.WriteLine("WARNING - WALE.Cmd - Empty licence set group found");
                continue;
            }

            foreach (var licenceSetLoop in licenceSetGroup)
            {
                foreach (var licenceLoop in licenceSetLoop.Licences)
                {
                    var filename = licenceLoop.Filename;
                    var licenceFound = licenceLoop.Status == LicenceStatus.Ok;
                    var licenceNumber = licenceLoop.LicenceNumber?.Value;
                    var isLicenceNumberNull = string.IsNullOrEmpty(licenceNumber);
                    var existingLicenceId = -1;
                    
                    var previouslySavedAsNotFound = !isLicenceNumberNull
                        && savedNotFoundLicences.TryGetValue(licenceNumber!, out existingLicenceId);

                    var foundButPreviouslySavedAsNotFound = previouslySavedAsNotFound && licenceFound;
                    var licenceNumberPresentNotYetSaved = !isLicenceNumberNull
                        && !savedLicenceNumbers.TryGetValue(licenceNumber!, out _);

                    var notYetSavedByFilename = isLicenceNumberNull
                        && !string.IsNullOrEmpty(filename)
                        && !savedLicenceFilenames.TryGetValue(filename, out _);
                    
                    if (licenceNumberPresentNotYetSaved || foundButPreviouslySavedAsNotFound)
                    {
                        int loopLicenceId;

                        if (foundButPreviouslySavedAsNotFound)
                        {
                            // TODO this appears to never happen - look into it
                            
                            await outputService.UpdateLicenceAsync(
                                licenceLoop,
                                existingLicenceId,
                                processRun.ProcessRunId);

                            loopLicenceId = existingLicenceId;
                        }
                        else
                        {
                            loopLicenceId = await outputService.SaveLicenceAsync(
                                licenceLoop,
                                processRun.ProcessRunId);
                        }

                        savedLicenceNumbers.TryAdd(licenceNumber!, loopLicenceId);

                        if (!string.IsNullOrWhiteSpace(filename))
                        {
                            savedLicenceFilenames.TryAdd(filename, loopLicenceId);
                        }

                        if (!licenceFound)
                        {
                            savedNotFoundLicences.TryAdd(licenceNumber!, loopLicenceId);
                        }
                        else
                        {
                            savedNotFoundLicences.Remove(licenceNumber!);
                        }

                        licenceLoop.NoneSchemaData["licenceId"] = loopLicenceId;
                    }
                    else if (notYetSavedByFilename)
                    {
                        var loopLicenceId = await outputService.SaveLicenceAsync(
                            licenceLoop,
                            processRun.ProcessRunId);

                        savedLicenceFilenames.Add(filename!, loopLicenceId);
                        licenceLoop.NoneSchemaData.Add("licenceId", loopLicenceId);
                    }

                    var licenceSetsLoop = SharedHelper.GetLicenceSetsForLicenceSetIds(
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
}