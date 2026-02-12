using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class ExternalDataHelper
{
    public static async Task<Dictionary<string, List<NaldData>>> GetNaldDataAsync(
        ICacheService cacheService,
        Dictionary<string, DmsFileData> licenceNumbersWithFilenames,
        int regionCode)
    {
        var data = await cacheService.GetNaldDataAsync((short)regionCode);
        
        var returnList = new Dictionary<string, NaldData>();
        var internalLicenceIdsNotInDataset = new HashSet<string>();
        
        foreach (var line in data.Licences!)
        {
            var stippedLicenceNumber = FormattingHelper.StripForComparison(line.LicenceNo, regionCode)!;
            var key = $"{line.FgacRegionCode}|{line.Id}";

            if (!licenceNumbersWithFilenames.ContainsKey(stippedLicenceNumber))
            {
                internalLicenceIdsNotInDataset.Add(key);
                continue;
            }

            if (returnList.TryGetValue(key, out _))
            {
                throw new Exception("Repeat row");
            }

            var naldData = new NaldData
            {
                Id = line.Id,
                ExpiryDate = RemoveNullWord(line.ExpiryDate),
                OrigEffDate = RemoveNullWord(line.OrigEffectiveDate),
                OrigSigDate = RemoveNullWord(line.OrigSignatureDate),
                RevocationDate = RemoveNullWord(line.RevDate),
                LicenceNumber = line.LicenceNo!,
                LicenceIdCharsAndDigitsOnly = stippedLicenceNumber,
                FgacRegionCode = line.FgacRegionCode
            };

            returnList.Add(key, naldData);
        }

        AddNaldAbstractionLicenceVersionData(
            data.LicenceVersions!,
            internalLicenceIdsNotInDataset,
            ref returnList);

        AddNaldAbstractionLicenceQuantitiesData(
            data.LicenceQuantities!,
            internalLicenceIdsNotInDataset,
            ref returnList);

        var purposeToLicenceMapping = AddNaldAbstractionLicencePurposeData(
            data.LicencePurposes!,
            internalLicenceIdsNotInDataset,
            ref returnList);

        AddNaldAbstractionLicencePointsData(
            data.LicencePoints!,
            ref purposeToLicenceMapping);

        var changedKeyList = new Dictionary<string, List<NaldData>>();

        foreach (var (_, naldData) in returnList)
        {
            var key = naldData.FgacRegionCode + "|" + naldData.LicenceIdCharsAndDigitsOnly;

            if (changedKeyList.ContainsKey(key))
            {
                changedKeyList[key].Add(naldData);
                continue;
            }

            changedKeyList.Add(key, [naldData]);
        }

        return changedKeyList;
    }

    private static string? RemoveNullWord(string? value)
    {
        return value == "null" ? null : value;
    }

    private static void AddNaldAbstractionLicenceVersionData(
        List<NaldLicenceVersionCsvLine> lines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        foreach (var line in lines)
        {
            var key = $"{line.FgacRegionCode}|{line.AablId}";

            if (licenceNumbersNotInDataset.Contains(key))
            {
                continue;
            }

            var existingData = generalNaldData.GetValueOrDefault(key);

            if (existingData == null)
            {
                throw new KeyNotFoundException(key);
            }

            if (line.Status != "CURR")
            {
                continue;
            }

            existingData.AabvType = line.AabvType;
            existingData.EffEndDate = RemoveNullWord(line.EffEndDate);
            existingData.EffStDate = RemoveNullWord(line.EffStDate);
            existingData.LicSigDate = RemoveNullWord(line.LicSigDate);
            existingData.IncrNo = line.IncrNo;
            existingData.IssueNo = line.IssueNo;
            existingData.Status = line.Status;
        }
    }

    private static void AddNaldAbstractionLicenceQuantitiesData(
        List<NaldLicenceQuantitiesCsvLine> lines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        foreach (var line in lines)
        {
            var key = $"{line.FgacRegionCode}|{line.AabvAablId}";

            if (licenceNumbersNotInDataset.Contains(key))
            {
                continue;
            }

            var existingData = generalNaldData.GetValueOrDefault(key);

            if (existingData == null)
            {
                throw new KeyNotFoundException(key);
            }

            existingData.MaxAnnualQty = RemoveNullWord(line.MaxAnnualQty) != null
                ? double.Parse(line.MaxAnnualQty!)
                : null;

            existingData.MaxDailyQty = RemoveNullWord(line.MaxDailyQty) != null
                ? double.Parse(line.MaxDailyQty!)
                : null;
        }
    }

    private static void AddNaldAbstractionLicencePointsData(
        List<NaldLicencePointCsvLine> lines,
        ref Dictionary<string, NaldData> purposeToLicenceMapping)
    {
        foreach (var line in lines)
        {
            var key = $"{line.FgacRegionCode}|{line.AabpId}";
            var existingData = purposeToLicenceMapping.GetValueOrDefault(key);

            if (existingData == null)
            {
                continue;
            }

            var naldDataPoint = new NaldDataPoint
            {
                PointId = int.Parse(line.AaipId!),
                PointName = line.AmoaCode
            };

            existingData.Points.Add(naldDataPoint);
        }
    }

    private static Dictionary<string, NaldData> AddNaldAbstractionLicencePurposeData(
        List<NaldLicencePurposeCsvLine> lines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        var returnDict = new Dictionary<string, NaldData>();

        foreach (var line in lines)
        {
            var key = $"{line.FgacRegionCode}|{line.AabvAablId}";

            if (licenceNumbersNotInDataset.Contains(key))
            {
                continue;
            }

            var existingData = generalNaldData.GetValueOrDefault(key);

            if (existingData == null)
            {
                throw new KeyNotFoundException(key);
            }

            var naldDataPeriod = new NaldDataPeriod
            {
                PeriodStartDay = line.PeriodStartDay,
                PeriodStartMonth = line.PeriodStartMonth,
                PeriodEndDay = line.PeriodEndDay,
                PeriodEndMonth = line.PeriodEndMonth
            };

            if (existingData.Periods.All(p => p.ToString() != naldDataPeriod.ToString()))
            {
                existingData.Periods.Add(naldDataPeriod);
            }

            var naldDataPurpose = new NaldDataPurpose
            {
                Id = int.Parse(line.Id!),
                PurposeId = line.ApurApusCode!.Value
            };

            if (existingData.Purposes.All(p => p.ToString() != naldDataPurpose.ToString()))
            {
                var purposeKey = $"{line.FgacRegionCode}|{line.Id}";

                returnDict.Add(purposeKey, existingData);
                existingData.Purposes.Add(naldDataPurpose);
            }

            existingData.AggregateConditions.Add(new NaldDataAggregate
            {
                Type = "Limit",
                Condition = line.ApurApseCode,
                ConditionId = line.ApurApusCode,
                AnnualQty = double.TryParse(line.AnnualQty, out var annualQty) ? annualQty : null,
                AnnualQtyUnits = line.AnnualQtyUnits,
                DailyQty = double.TryParse(line.DailyQty, out var dailyQty) ? dailyQty : null,
                DailyQtyUnits = line.DailyQtyUnits,
                HourlyQty = double.TryParse(line.HourlyQty, out var hourlyQtt) ? hourlyQtt : null,
                HourlyQtyUnits = line.HourlyQtyUnits,
                InstQty = double.TryParse(line.InstQty, out var instQty) ? instQty : null,
                InstQtyUnits = line.InstQtyUnits
            });
        }

        return returnDict;
    }
}