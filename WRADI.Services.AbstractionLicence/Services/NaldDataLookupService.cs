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
        int regionCode,
        bool slashesRemoved = false)
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

        var naldData = await cacheService.GetNaldAbstractionLicenceAsync(licenceNumber, regionCode, slashesRemoved);
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
            Console.WriteLine($"ERROR no nald purposes found for {licenceNumber} ");
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
            await outputService.GetDocumentNaldPurposeMapAsync()); // TODO dont do this everytime - cache it
        
        // There is only one, so must be that
        if (groupedPurposes.Count == 1)
        {
            var unfilteredGroupedPurposesCount = naldPurposes
                .GroupBy(pu => pu.CombinedCode)
                .Count();
            
            const string onlyOne = "OnlyOne";
            const string onlyOneLeft = "OnlyOneLeft";
            var matchType = unfilteredGroupedPurposesCount > 1 ? onlyOneLeft : onlyOne;
            
            var singlePurposeArray = groupedPurposes[0].ToArray();
            
            if (saveMatches)
            {
                var contains = MappingContainsPurpose(
                    singlePurposeArray[0],
                    documentDescription,
                    documentToNaldPurposeMapping);
                
                if (contains != MatchExplicitness.ExactMatch)
                {
                    await outputService.AddDocumentNaldPurposeMapAsync(
                        documentDescription,
                        singlePurposeArray[0],
                        matchType);
                }

                await outputService.AddDocumentNaldPurposeMatchAsync(
                    licenceNumber,
                    documentDescription,
                    singlePurposeArray[0],
                    matchType);
            }

            return (singlePurposeArray, matchType);
        }
        
        foreach (var loopNaldPurposes in groupedPurposes)
        {
            if (excludeNaldPurposeIds.Contains(loopNaldPurposes.First().Id!))
            {
                continue;
            }

            var firstNaldPurpose = loopNaldPurposes.First();
            
            var contains = MappingContainsPurpose(
                firstNaldPurpose,
                documentDescription,
                documentToNaldPurposeMapping);
            
            if (contains != MatchExplicitness.NotMatched)
            {
                const string explicitMapping = "ExplicitMapping";

                if (saveMatches)
                {
                    await outputService.AddDocumentNaldPurposeMatchAsync(
                        licenceNumber,
                        documentDescription,
                        firstNaldPurpose,
                        explicitMapping);
                }

                return (loopNaldPurposes.ToArray(), explicitMapping);
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
    
    private static MatchExplicitness MappingContainsPurpose(
        NaldPurposeData naldPurposeData,
        string? documentDescription,
        Dictionary<string, List<NaldPurposeMap>> documentToNaldPurposeMapping)
    {
        if (string.IsNullOrEmpty(documentDescription))
        {
            return MatchExplicitness.NotMatched;
        }
        
        var documentDescriptionLower = documentDescription
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .ToLower();
        
        var documentPurposeIsMapped = documentToNaldPurposeMapping.ContainsKey(documentDescriptionLower);

        if (!documentPurposeIsMapped)
        {
            return MatchExplicitness.NotMatched;
        }

        var mappedNaldValues = documentToNaldPurposeMapping[documentDescriptionLower];

        var exactMatch = mappedNaldValues
            .Any(v => v.NaldPurposePrimaryCategoryDescription?
                .Equals(naldPurposeData.PrimaryCategoryDescription, StringComparison.OrdinalIgnoreCase) == true)
                && mappedNaldValues
            .Any(v => v.NaldPurposeSecondaryCategoryDescription?
                .Equals(naldPurposeData.SecondaryCategoryDescription, StringComparison.OrdinalIgnoreCase) == true)
                && mappedNaldValues
            .Any(v => v.NaldPurposeUseDescription?
                .Equals(naldPurposeData.UseDescription, StringComparison.OrdinalIgnoreCase) == true);

        if (exactMatch)
        {
            return MatchExplicitness.ExactMatch;
        }
        
        var partialMatch = mappedNaldValues
            .Any(mnv => mnv.NaldPurposeUseDescription?
                .Equals(naldPurposeData.UseDescription, StringComparison.OrdinalIgnoreCase) == true);

        if (partialMatch)
        {
            return MatchExplicitness.PartialMatch;
        }
        
        return MatchExplicitness.NotMatched;
    }

    public enum MatchExplicitness
    {
        NotMatched = 0,
        PartialMatch = 1,
        ExactMatch = 2,
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

            if (entry.DocumentPurpose.Equals("process of manufacture", StringComparison.OrdinalIgnoreCase))
            {
                
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