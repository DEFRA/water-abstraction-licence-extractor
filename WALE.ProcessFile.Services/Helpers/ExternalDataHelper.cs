using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class ExternalDataHelper
{
    public static async Task<Dictionary<string, List<NaldData>>> GetNaldDataFromDatabaseAsync(
        IDatabaseReadService databaseReadService,
        Dictionary<string, DmsFileData> licenceNumbersWithFilenames,
        int regionCode)
    {
        // Fetch all data in parallel
        var licencesTask = databaseReadService.GetNaldAbsLicencesAsync((short)regionCode);
        var versionsTask = databaseReadService.GetNaldLicenceVersionsAsync((short)regionCode);
        var purposesTask = databaseReadService.GetNaldLicencePurposesAsync((short)regionCode);
        var pointsTask = databaseReadService.GetNaldLicencePointsAsync((short)regionCode);
        var quantitiesTask = databaseReadService.GetNaldLicenceQuantitiesAsync((short)regionCode);

        await Task.WhenAll(licencesTask, versionsTask, purposesTask, pointsTask, quantitiesTask);

        var licences = await licencesTask;
        var versions = await versionsTask;
        var purposes = await purposesTask;
        var points = await pointsTask;
        var quantities = await quantitiesTask;

        var returnList = new Dictionary<string, NaldData>();
        var internalLicenceIdsNotInDataset = new HashSet<string>();

        foreach (var line in licences)
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

        AddNaldAbstractionLicenceVersionData(versions, internalLicenceIdsNotInDataset, ref returnList);
        AddNaldAbstractionLicenceQuantitiesData(quantities, internalLicenceIdsNotInDataset, ref returnList);
        var purposeToLicenceMapping =
            AddNaldAbstractionLicencePurposeData(purposes, internalLicenceIdsNotInDataset, ref returnList);
        AddNaldAbstractionLicencePointsData(points, ref purposeToLicenceMapping);

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
        List<NaldLicenceVersionDataLine> naldCurrentVersionDataLines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        foreach (var versionDataLine in naldCurrentVersionDataLines
                     .Where(x => !licenceNumbersNotInDataset.Contains(x.LookupKey)))
        {
            if (!generalNaldData.TryGetValue(versionDataLine.LookupKey, out var naldData))
            {
                throw new KeyNotFoundException(versionDataLine.LookupKey);
            }

            naldData.AabvType = versionDataLine.AabvType;
            naldData.EffEndDate = RemoveNullWord(versionDataLine.EffEndDate);
            naldData.EffStDate = RemoveNullWord(versionDataLine.EffStDate);
            naldData.LicSigDate = RemoveNullWord(versionDataLine.LicSigDate);
            naldData.IncrNo = versionDataLine.IncrNo;
            naldData.AppNo = versionDataLine.AppNo;
            naldData.IssueNo = versionDataLine.IssueNo;
            naldData.Status = versionDataLine.Status;
            naldData.WaAltyCode = versionDataLine.WaAltyCode;
            naldData.AsrcCode = versionDataLine.AsrcCode;
        }
    }

    private static void AddNaldAbstractionLicenceQuantitiesData(
        List<NaldLicenceQuantitiesDataLine> naldLicenceQuantitiesDataLines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        foreach (var quantitiesDataLine in naldLicenceQuantitiesDataLines
                     .Where(x => !licenceNumbersNotInDataset.Contains(x.LookupKey)))
        {
            if (!generalNaldData.TryGetValue(quantitiesDataLine.LookupKey, out var naldData))
            {
                throw new KeyNotFoundException(quantitiesDataLine.LookupKey);
            }

            // Ignore non-current quantity data
            if (naldData.IncrNo != quantitiesDataLine.AabvIncrNo ||
                naldData.IssueNo != quantitiesDataLine.AabvIssueNo)
            {
                continue;
            }

            naldData.MaxAnnualQty = RemoveNullWord(quantitiesDataLine.MaxAnnualQty) != null
                ? double.Parse(quantitiesDataLine.MaxAnnualQty!)
                : null;

            naldData.MaxDailyQty = RemoveNullWord(quantitiesDataLine.MaxDailyQty) != null
                ? double.Parse(quantitiesDataLine.MaxDailyQty!)
                : null;

            naldData.QuantityAggregated = quantitiesDataLine.AggregatedInd;
            naldData.QuantityUserValid = quantitiesDataLine.UserValidInd;

            naldData.QuantityPurpPoints = quantitiesDataLine.PurpPointsInd switch
            {
                "1" => "Single Point / Single Purpose",
                "2" => "Single Point / Multiple Purposes",
                "3" => "Multiple Points / Single Purpose",
                "4" => "Multiple Points / Multiple Purposes",
                _ => "Unknown"
            };
        }
    }

    private static void AddNaldAbstractionLicencePointsData(
        List<NaldLicencePointDataLine> lines,
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
        List<NaldLicencePurposeDataLine> lines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        var returnDict = new Dictionary<string, NaldData>();

        foreach (var line in lines.Where(x =>
                     !licenceNumbersNotInDataset.Contains($"{x.FgacRegionCode}|{x.AabvAablId}")))
        {
            var key = $"{line.FgacRegionCode}|{line.AabvAablId}";

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