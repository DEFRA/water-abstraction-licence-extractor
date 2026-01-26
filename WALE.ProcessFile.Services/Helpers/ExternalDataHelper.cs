using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class ExternalDataHelper
{
    public static Dictionary<string, List<NaldData>> GetNaldAbstractionLicencesData(
        string? naldDataReportPath,
        string? naldAbsLicencePurposesDataPath,
        string? naldAbsLicenceVersionsDataPath,
        string? naldAbsLicenceQuantitiesDataPath,
        int regionCode)
    {
        if (string.IsNullOrEmpty(naldDataReportPath))
        {
            throw new NullReferenceException(nameof(naldDataReportPath));
        }

        var returnList = new Dictionary<string, NaldData>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using var reader = new StreamReader(naldDataReportPath);
        using var csv = new CsvReader(reader, config);

        var lines = csv.GetRecords<NaldAbstractionLicenceCsvLine>().ToList();
        var lapsedIds = new List<string>();
        
        foreach (var line in lines)
        {
            if (line.FgacRegionCode != regionCode.ToString())
            {
                continue;
            }
            
            var lapsedDate = DateTime.TryParse(line.LapsedDate, out var ld) ? ld : (DateTime?)null;

            if (lapsedDate != null && DateTime.Today.AddYears(-1) > lapsedDate)
            {
                lapsedIds.Add(line.Id!);
                continue;
            }
            
            var stippedLicenceNumber = FormattingHelper.StripForComparison(line.LicenceNo, regionCode)!;
            var key = line.FgacRegionCode + "|" + line.Id;

            // Find an existing line
            if (returnList.TryGetValue(key, out _))
            {
                throw new Exception("Repeat row");
            }

            var naldData = new NaldData
            {
                Id = line.Id,
                ExpiryDate = RemoveNullWord(line.ExpiryDate),
                OrigEffDate = line.OrigEffectiveDate,
                OrigSigDate = RemoveNullWord(line.OrigSignatureDate),
                RevisionDate = RemoveNullWord(line.RevDate),
                LicenceNumber = line.LicenceNo!,
                LicenceIdCharsAndDigitsOnly = stippedLicenceNumber,
                FgacRegionCode = line.FgacRegionCode
            };
            
            returnList.Add(key, naldData);
        }

        AddNaldAbstractionLicenceVersionData(
            naldAbsLicenceVersionsDataPath,
            regionCode,
            lapsedIds,
            ref returnList);

        AddNaldAbstractionLicenceQuantitiesData(
            naldAbsLicenceQuantitiesDataPath,
            regionCode,
            lapsedIds,
            ref returnList);
        
        AddNaldAbstractionLicencePurposeData(
            naldAbsLicencePurposesDataPath,
            regionCode,
            lapsedIds,
            ref returnList);

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
        int regionCode,
        List<string> lapsedIds,
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
            if (line.FgacRegionCode != regionCode.ToString())
            {
                continue;
            }

            if (lapsedIds.Contains(line.AablId.ToString()!))
            {
                continue;
            }
            
            var licenceKey = $"{line.FgacRegionCode}|{line.AablId}";
            var existingData = generalNaldData.GetValueOrDefault(licenceKey);

            if (existingData == null)
            {
                throw new KeyNotFoundException(licenceKey);
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
        int regionCode,
        List<string> lapsedIds,
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
            if (line.FgacRegionCode != regionCode.ToString())
            {
                continue;
            }

            if (lapsedIds.Contains(line.AabvAablId.ToString()!))
            {
                continue;
            }
            
            var licenceKey = $"{line.FgacRegionCode}|{line.AabvAablId}";
            var existingData = generalNaldData.GetValueOrDefault(licenceKey);

            if (existingData == null)
            {
                throw new KeyNotFoundException(licenceKey);
            }
            
            existingData.MaxAnnualQty = RemoveNullWord(line.MaxAnnualQty) != null
                ? double.Parse(line.MaxAnnualQty!)
                : null;
            
            existingData.MaxDailyQty = RemoveNullWord(line.MaxDailyQty) != null
                ? double.Parse(line.MaxDailyQty!)
                : null;
        }
    }
    
    private static void AddNaldAbstractionLicencePurposeData(
        string? naldDataReportPath,
        int regionCode,
        List<string> lapsedIds,
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

        foreach (var line in lines)
        {
            if (line.FgacRegionCode != regionCode.ToString())
            {
                continue;
            }
            
            if (lapsedIds.Contains(line.InternalLicenceId!))
            {
                continue;
            }
            
            var licenceKey = $"{line.FgacRegionCode}|{line.InternalLicenceId}";
            var existingData = generalNaldData.GetValueOrDefault(licenceKey);
            
            if (existingData == null)
            {
                throw new KeyNotFoundException(licenceKey);
            }
            
            var naldDataPeriod = new NaldDataPeriod
            {
                PeriodStartDay = line.PeriodStartDay,
                PeriodStartMonth = line.PeriodStartMonth,
                PeriodEndDay = line.PeriodEndDay,
                PeriodEndMonth = line.PeriodEndMonth
            };

            if (existingData.Periods.All(p => p.ToString() != naldDataReportPath))
            {
                existingData.Periods.Add(naldDataPeriod);                
            }
            
            existingData.AggregateConditions.Add(new NaldDataAggregate
            {
                Type = "Limit",
                Condition = line.PurposeCode,
                ConditionId = line.PurposeCodeId,
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
    }

    public static Dictionary<string, string> GetLicenceNumberMappingFromFilenames(string? pdfFolderPath, int regionCode)
    {
        if (string.IsNullOrEmpty(pdfFolderPath))
        {
            throw new NullReferenceException(nameof(pdfFolderPath));
        }

        var returnMapping = new Dictionary<string, string>();
        var filenames = FileHelper.GetRelevantFilesInFolder(pdfFolderPath)
            .Keys
            .Select(filepath => filepath.Split('/').Last())
            .ToList();

        foreach (var filename in filenames)
        {
            var parts = filename.Split('_');
            var licenceNumber = parts[0];

            if (licenceNumber.Count(char.IsDigit) < 7)
            {
                continue;
            }
            
            var strippedLicenceNumber = FormattingHelper.StripForComparison(licenceNumber, regionCode)!;
            
            if (!returnMapping.TryAdd(strippedLicenceNumber, filename))
            {
                throw new Exception($"{filename} is a duplicate for {licenceNumber}");
            }
        }

        return returnMapping;
    }

    public static HashSet<string> GetLiveLicenceNumbers(
        string? liveLicencesReportPath,
        int regionCode)
    {
        if (string.IsNullOrEmpty(liveLicencesReportPath))
        {
            throw new NullReferenceException(nameof(liveLicencesReportPath));
        }

        var returnList = new HashSet<string>();

        var fileContents = File.Exists(liveLicencesReportPath)
            ? File.ReadAllText(liveLicencesReportPath)
                .Replace("\r", string.Empty)
                .Split('\n')
            : [];

        var count = 0;
        foreach (var line in fileContents)
        {
            if (count++ == 0)
            {
                continue;
            }

            var parts = line.Split(',');

            if (parts.Length < 3)
            {
                continue;
            }

            var licenceNumber = parts[2];

            var strippedLicenceNumber = FormattingHelper.StripForComparison(licenceNumber, regionCode)!;
            returnList.Add(strippedLicenceNumber);
        }

        return returnList;
    }

    public static HashSet<string> GetDeadLicenceNumbers(
        string? deadLicencesReportPath,
        int regionCode)
    {
        if (string.IsNullOrEmpty(deadLicencesReportPath))
        {
            throw new NullReferenceException(nameof(deadLicencesReportPath));
        }

        var returnList = new HashSet<string>();

        var fileContents = File.Exists(deadLicencesReportPath)
            ? File.ReadAllText(deadLicencesReportPath)
                .Replace("\r", string.Empty)
                .Split('\n')
            : [];

        var count = 0;
        foreach (var line in fileContents)
        {
            if (count++ == 0)
            {
                continue;
            }

            var parts = line.Split(',');

            if (parts.Length < 6)
            {
                continue;
            }

            var licenceNumber = parts[5];

            var strippedLicenceNumber = FormattingHelper.StripForComparison(licenceNumber, regionCode)!;
            returnList.Add(strippedLicenceNumber);
        }

        return returnList;
    }

    public static HashSet<string> GetImpoundmentLicenceNumbers(
        string? impoundmentLicencesReportPath,
        int regionCode)
    {
        if (string.IsNullOrEmpty(impoundmentLicencesReportPath))
        {
            throw new NullReferenceException(nameof(impoundmentLicencesReportPath));
        }

        var returnList = new HashSet<string>();

        var fileContents = File.Exists(impoundmentLicencesReportPath)
            ? File.ReadAllText(impoundmentLicencesReportPath)
                .Replace("\r", string.Empty)
                .Split('\n')
            : [];

        var count = 0;
        foreach (var line in fileContents)
        {
            if (count++ == 0)
            {
                continue;
            }

            var parts = line.Split(',');
            var licenceNumber = parts[0];
            
            var strippedLicenceNumber = FormattingHelper.StripForComparison(licenceNumber, regionCode)!;
            returnList.Add(strippedLicenceNumber);
        }

        return returnList;
    }
}