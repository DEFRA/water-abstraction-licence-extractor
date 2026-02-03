using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class ExternalDataHelper
{
    public static Dictionary<string, List<NaldData>> GetNaldAbstractionLicencesData(
        Dictionary<string, DmsFileData> licenceNumbersWithFilenames,
        string? naldDataReportPath,
        string? naldAbsLicencePurposesDataPath,
        string? naldAbsLicencePointsDataPath,        
        string? naldAbsLicenceVersionsDataPath,
        string? naldAbsLicenceQuantitiesDataPath,
        int regionCode)
    {
        if (string.IsNullOrEmpty(naldDataReportPath))
        {
            return [];
        }

        var returnList = new Dictionary<string, NaldData>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using var reader = new StreamReader(naldDataReportPath);
        using var csv = new CsvReader(reader, config);

        var lines = csv.GetRecords<NaldAbstractionLicenceCsvLine>().ToList();
        var internalLicenceIdsNotInDataset = new HashSet<string>();
        
        foreach (var line in lines)
        {
            var stippedLicenceNumber = FormattingHelper.StripForComparison(line.LicenceNo, regionCode)!;
            var key = $"{line.FgacRegionCode}|{line.Id}";
            
            if (!licenceNumbersWithFilenames.ContainsKey(stippedLicenceNumber))
            {
                internalLicenceIdsNotInDataset.Add(key);
                continue;
            }

            // Find an existing line
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
            naldAbsLicenceVersionsDataPath,
            internalLicenceIdsNotInDataset,
            ref returnList);

        AddNaldAbstractionLicenceQuantitiesData(
            naldAbsLicenceQuantitiesDataPath,
            internalLicenceIdsNotInDataset,
            ref returnList);
        
        var purposeToLicenceMapping = AddNaldAbstractionLicencePurposeData(
            naldAbsLicencePurposesDataPath,
            internalLicenceIdsNotInDataset,
            ref returnList);
        
        AddNaldAbstractionLicencePointsData(
            naldAbsLicencePointsDataPath,
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
        string? naldDataReportPath,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        if (string.IsNullOrEmpty(naldDataReportPath))
        {
            throw new NullReferenceException(nameof(naldDataReportPath));
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using var reader = new StreamReader(naldDataReportPath);
        using var csv = new CsvReader(reader, config);

        var lines = csv.GetRecords<NaldLicenceVersionCsvLine>().ToList();

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
        string? naldDataReportPath,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        if (string.IsNullOrEmpty(naldDataReportPath))
        {
            throw new NullReferenceException(nameof(naldDataReportPath));
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using var reader = new StreamReader(naldDataReportPath);
        using var csv = new CsvReader(reader, config);

        var lines = csv.GetRecords<NaldLicenceQuantitiesCsvLine>().ToList();

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
        string? naldDataReportPath,
        ref Dictionary<string, NaldData> purposeToLicenceMapping)
    {
        if (string.IsNullOrEmpty(naldDataReportPath))
        {
            throw new NullReferenceException(nameof(naldDataReportPath));
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using var reader = new StreamReader(naldDataReportPath);
        using var csv = new CsvReader(reader, config);

        var lines = csv.GetRecords<NaldLicencePointCsvLine>().ToList();
        
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
                PointId = int.Parse(line.AaipId!), // TODO
                PointName = line.AmoaCode // TODO
            };

            existingData.Points.Add(naldDataPoint);
        }
    }

    private static Dictionary<string, NaldData> AddNaldAbstractionLicencePurposeData(
        string? naldDataReportPath,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        if (string.IsNullOrEmpty(naldDataReportPath))
        {
            throw new NullReferenceException(nameof(naldDataReportPath));
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using var reader = new StreamReader(naldDataReportPath);
        using var csv = new CsvReader(reader, config);

        var lines = csv.GetRecords<NaldLicencePurposeCsvLine>().ToList();
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