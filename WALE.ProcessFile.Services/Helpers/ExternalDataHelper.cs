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

        foreach (var line in lines)
        {
            var lapsedDate = DateTime.TryParse(line.LapsedDate, out var ld) ? ld : (DateTime?)null;

            if (lapsedDate != null && DateTime.Today.AddYears(-1) > lapsedDate)
            {
                continue;
            }
            
            var lineCondition = new NaldDataAggregate
            {
                Type = "General",
                /*Condition = line.Condition,
                ConditionId = line.ConditionId,
                AnnualQty = line.LicenceWideAnnualQty,
                DailyQty = line.LicenceWideDailyQty,
                HourlyQty = line.LicenceWideHourlyQty,
                InstQty = line.LicenceWideInstQty*/
            };

            if (string.IsNullOrEmpty(lineCondition.Condition) || lineCondition.Condition == "-")
            {
                lineCondition = null;
            }

            var linePoint = new NaldDataPoint
            {
                /*PointId = line.PointId,
                PointName = line.PointName,
                Category = line.PointCategory,
                PrimaryType = line.PrimaryPointType,
                SecondaryType = line.SecondaryPointType,
                Ngr1Cartesian = !string.IsNullOrWhiteSpace(line.Ngr1Cartesian) ? line.Ngr1Cartesian : null,
                Ngr2Cartesian = !string.IsNullOrWhiteSpace(line.Ngr2Cartesian) ? line.Ngr2Cartesian : null,
                Ngr3Cartesian = !string.IsNullOrWhiteSpace(line.Ngr3Cartesian) ? line.Ngr3Cartesian : null,
                Ngr4Cartesian = !string.IsNullOrWhiteSpace(line.Ngr4Cartesian) ? line.Ngr4Cartesian : null,
                Ngr1 = !string.IsNullOrWhiteSpace(line.Ngr1) ? line.Ngr1 : null,
                Ngr2 = !string.IsNullOrWhiteSpace(line.Ngr2) ? line.Ngr2 : null,
                Ngr3 = !string.IsNullOrWhiteSpace(line.Ngr3) ? line.Ngr3 : null,
                Ngr4 = !string.IsNullOrWhiteSpace(line.Ngr4) ? line.Ngr4 : null*/
            };
            
            var linePurpose = new NaldDataPurpose
            {
                /*PurposeId = line.PurposeId,
                PurposeCode = line.PurposeCode,
                PurposeUseCode = line.PurposeUseCode,
                PurposeUseDescription = line.PurposeUseDescription*/
            };
            
            var stippedLicenceNumber = FormattingHelper.StripForComparison(line.LicenceNo, regionCode)!;
            var key = line.FgacRegionCode + "|" + line.Id;

            // Find an existing line
            if (returnList.TryGetValue(key, out var existingItem))
            {
                if (lineCondition != null && existingItem.AggregateConditions
                    .All(existingCondition => existingCondition.ToString() != lineCondition.ToString()))
                {
                    existingItem.AggregateConditions.Add(lineCondition);
                }

                if (existingItem.Points.All(existingPoint => existingPoint.ToString() != linePoint.ToString()))
                {
                    existingItem.Points.Add(linePoint);
                }
                
                if (existingItem.Purposes.All(existingPurpose => existingPurpose.ToString() != linePurpose.ToString()))
                {
                    existingItem.Purposes.Add(linePurpose);
                }
                
                continue;
            }

            var lineConditionsArray = lineCondition == null
                ? new List<NaldDataAggregate>()
                : [lineCondition];

            var naldData = new NaldData
            {
                Id = line.Id,
                ExpiryDate = line.ExpiryDate,
                //VersionStartDate = line.VersionStartDate,
                LicenceNumber = line.LicenceNo!,
                LicenceIdCharsAndDigitsOnly = stippedLicenceNumber,
                /*LicenceWideAnnualQty = line.LicenceWideAnnualQty,
                LicenceWideDailyQty = line.LicenceWideDailyQty,
                LicenceWideHourlyQty = line.LicenceWideHourlyQty,
                LicenceWideInstQty = line.LicenceWideInstQty,*/
                AggregateConditions = lineConditionsArray,
                Points = [linePoint],
                Periods = [],
                Purposes = [linePurpose],
                FgacRegionCode = line.FgacRegionCode
            };
            
            returnList.Add(key, naldData);
        }
        
        AddNaldAbstractionLicencePurposeData(
            naldAbsLicencePurposesDataPath,
            ref returnList);

        var returnList2 = new Dictionary<string, List<NaldData>>();
        
        foreach (var (_, naldData) in returnList)
        {
            var key = naldData.FgacRegionCode + "|" + naldData.LicenceIdCharsAndDigitsOnly;
            
            if (returnList2.ContainsKey(key))
            {
                returnList2[key].Add(naldData);
                continue;
            }
            
            returnList2.Add(key, [naldData]);
        }
        
        return returnList2;
    }

    private static void AddNaldAbstractionLicencePurposeData(
        string? naldDataReportPath,
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
            var licenceKey = $"{line.FgacRegionCode}|{line.InternalLicenceId}";
            var existingData = generalNaldData.GetValueOrDefault(licenceKey);

            if (existingData == null)
            {
                // Was likely lapsed so we didnt include it
                continue;
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