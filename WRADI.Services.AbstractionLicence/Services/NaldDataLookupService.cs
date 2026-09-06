using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Interfaces;

namespace WRADI.DocumentType.AbstractionLicence.Services;

public class NaldDataLookupService(
    IAbstractionLicenceCacheService cacheService,
    IAbstractionLicenceOutputService outputService) : INaldDataLookupService
{
    private readonly ConcurrentDictionary<string, NaldAbstractionData?> _naldAbstractionDataCache = new();
    private readonly ConcurrentDictionary<string, NaldImpoundmentData?> _naldImpoundmentDataCache = new();
    
    public async Task<NaldAbstractionData?> GetNaldAbstractionDataLineAsync(
        string? licenceNumber,
        int regionCode)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return null;
        }
        
        var key = $"{regionCode}|{licenceNumber}";

        if (_naldAbstractionDataCache.TryGetValue(key, out var cachedData))
        {
            return cachedData;
        }

        var naldData = await cacheService.GetNaldAbstractionLicenceAsync(licenceNumber, regionCode);
        _naldAbstractionDataCache.TryAdd(key, naldData);
        
        return naldData;
    }

    public async Task<NaldImpoundmentData?> GetNaldImpoundmentDataLineAsync(string? licenceNumber, int regionCode)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return null;
        }
        
        var key = $"{regionCode}|{licenceNumber}";

        if (_naldImpoundmentDataCache.TryGetValue(key, out var cachedData))
        {
            return cachedData;
        }

        var naldData = await cacheService.GetNaldImpoundmentLicenceAsync(licenceNumber, regionCode);
        _naldImpoundmentDataCache.TryAdd(key, naldData);
        
        return naldData;
    }

    public async Task<(NaldPurposeData[] Purposes, string? MatchType)>
        GetRelevantNaldPurposesAsync(
            List<NaldPurposeData> naldPurposes,
            string? documentDescription,
            List<string> excludeNaldPurposeIds,
            string licenceNumber,
            bool saveMatches)
    {
        var filterPurposes = naldPurposes
            .Where(p => !excludeNaldPurposeIds.Contains(p.Id!))
            .ToList();

        if (filterPurposes.Count == 0)
        {
            return ([], null);
        }
        
        var groupedPurposes = filterPurposes
            .GroupBy(pu => pu.CombinedCode)
            .ToList();
     
        var descriptionSuggestsTransfer =
            documentDescription?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true
            || documentDescription?.Contains("subsequent", StringComparison.OrdinalIgnoreCase) == true;

        documentDescription = FormattingHelper.TrimFormatting(
            documentDescription,
            true,
            true);

        if (string.IsNullOrWhiteSpace(documentDescription))
        {
            throw new Exception("Document description is empty");
        }
        
        var documentToNaldPurposeMapping = ToDict(
            await outputService.GetDocumentNaldPurposeMapAsync());
        
        // There is only one, so must be that
        if (groupedPurposes.Count == 1)
        {
            const string onlyOne = "OnlyOne";
            var singlePurposeArray = groupedPurposes[0].ToArray();
            
            if (saveMatches)
            {
                if (!MappingContainsPurpose(singlePurposeArray[0], documentDescription, documentToNaldPurposeMapping))
                {
                    await outputService.AddDocumentNaldPurposeMapAsync(documentDescription, singlePurposeArray[0],
                        onlyOne);
                }

                await outputService.AddDocumentNaldPurposeMatchAsync(
                    licenceNumber,
                    documentDescription,
                    singlePurposeArray[0],
                    onlyOne);
            }

            return (singlePurposeArray, onlyOne);
        }
        
        foreach (var loopNaldPurposes in groupedPurposes)
        {
            if (excludeNaldPurposeIds.Contains(loopNaldPurposes.First().Id!))
            {
                continue;
            }

            var firstNaldPurpose = loopNaldPurposes.First();
            
            if (MappingContainsPurpose(firstNaldPurpose, documentDescription, documentToNaldPurposeMapping))
            {
                return (loopNaldPurposes.ToArray(), "ExplicitMapping");
            }
            
            continue;
            
            if (documentDescription.Equals(firstNaldPurpose.PrimaryCategoryDescription, StringComparison.OrdinalIgnoreCase)
                || documentDescription.Equals(firstNaldPurpose.SecondaryCategoryDescription, StringComparison.OrdinalIgnoreCase)
                || documentDescription.Equals(firstNaldPurpose.UseDescription, StringComparison.OrdinalIgnoreCase))
            {
                return (loopNaldPurposes.ToArray(), "DescriptionMatchesDescription");
            }
            
            if (descriptionSuggestsTransfer)
            {
                var naldSuggestsTransfer =
                    firstNaldPurpose.UseDescription?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.PrimaryCategoryDescription?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.SecondaryCategoryDescription?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.UseDescription?.Contains("subsequent", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.PrimaryCategoryDescription?.Contains("subsequent", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.SecondaryCategoryDescription?.Contains("subsequent", StringComparison.OrdinalIgnoreCase) == true;

                if (naldSuggestsTransfer)
                {
                    return (loopNaldPurposes.ToArray(), "DescriptionSuggestsTransfer");
                }
            }

            if (firstNaldPurpose.UseDescription?.Contains(documentDescription, StringComparison.OrdinalIgnoreCase) == true
                || firstNaldPurpose.PrimaryCategoryDescription?.Contains(documentDescription, StringComparison.OrdinalIgnoreCase) == true
                || firstNaldPurpose.SecondaryCategoryDescription?.Contains(documentDescription, StringComparison.OrdinalIgnoreCase) == true)
            {
                return (loopNaldPurposes.ToArray(), "DescriptionContainsDescription");
            }
        }

        return ([], null);
    }

    public static List<NaldPurposeData> ToNaldPurposeData(List<NaldDataPurpose>? purposes)
    {
        return purposes?
            .Select(purpose => new NaldPurposeData
            {
                Id = purpose.Id.ToString(),
                PrimaryCategoryDescription = purpose.CategoryUse.PrimaryCategoryDescription,
                SecondaryCategoryDescription = purpose.CategoryUse.SecondaryCategoryDescription,
                UseDescription = purpose.CategoryUse.UseDescription,
                PrimaryCategoryCode = purpose.CategoryUse.PrimaryCategoryCode.ToString(),
                SecondaryCategoryCode = purpose.CategoryUse.SecondaryCategoryCode.ToString(),
                UseCode = purpose.CategoryUse.UseCode,
                CombinedCode = purpose.CategoryUse.Code,
                QuantityIdentifier = $"{purpose.Quantity.AnnualQty}_{purpose.Quantity.DailyQty}" +
                    $"_{purpose.Quantity.HourlyQty}_{purpose.Quantity.InstQty}"
            })
            .ToList() ?? [];
    }
    
    private static bool MappingContainsPurpose(
        NaldPurposeData naldPurposeData,
        string? documentDescription,
        Dictionary<string, List<NaldPurposeMap>> documentToNaldPurposeMapping)
    {
        if (string.IsNullOrEmpty(documentDescription))
        {
            return false;
        }
        
        var documentDescriptionLower = documentDescription.ToLower();
        var documentPurposeIsMapped = documentToNaldPurposeMapping.ContainsKey(documentDescriptionLower);

        if (!documentPurposeIsMapped)
        {
            return false;
        }

        var mappedNaldValues = documentToNaldPurposeMapping[documentDescriptionLower];

        return mappedNaldValues.Any(v => v.NaldPurposePrimaryCategoryDescription?.Equals(naldPurposeData.PrimaryCategoryDescription, StringComparison.OrdinalIgnoreCase) == true)
            && mappedNaldValues.Any(v => v.NaldPurposeSecondaryCategoryDescription?.Equals(naldPurposeData.SecondaryCategoryDescription, StringComparison.OrdinalIgnoreCase) == true)
            && mappedNaldValues.Any(v => v.NaldPurposeUseDescription?.Equals(naldPurposeData.UseDescription, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static Dictionary<string, List<NaldPurposeMap>> ToDict(
        List<DocumentNaldPurposeMap> mapEntries)
    {
        var returnDict = new Dictionary<string, List<NaldPurposeMap>>();

        foreach (var entry in mapEntries)
        {
            if (string.IsNullOrEmpty(entry.DocumentPurpose))
            {
                continue;
            }

            var key = entry.DocumentPurpose.ToLower();
            var value = (NaldPurposeMap)entry;
            
            if (returnDict.TryGetValue(key, out var list))
            {
                list.Add(value);
                continue;
            }

            returnDict.Add(key, [value]);
        }
        
        return returnDict;
    }
}