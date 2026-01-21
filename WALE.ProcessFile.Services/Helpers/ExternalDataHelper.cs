using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class ExternalDataHelper
{
    public static Dictionary<string, NaldData> GetNaldGeneralReportData(string? naldDataReportPath)
    {
        if (string.IsNullOrEmpty(naldDataReportPath))
        {
            throw new NullReferenceException(nameof(naldDataReportPath));
        }

        var returnList = new Dictionary<string, NaldData>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            ShouldSkipRecord = row =>
                string.IsNullOrEmpty(row.Row[0])
                || row.Row[0] == "Region"
                || row.Row[0] != "North East Region"
        };

        using var reader = new StreamReader(naldDataReportPath);
        using var csv = new CsvReader(reader, config);

        var lines = csv.GetRecords<NaldGeneralDataLine>().ToList();

        foreach (var line in lines)
        {
            var lineCondition = new NaldDataAggregate
            {
                Type = "General",
                Condition = line.Condition,
                ConditionId = line.ConditionId,
                AnnualQty = line.LicenceWideAnnualQty,
                DailyQty = line.LicenceWideDailyQty,
                HourlyQty = line.LicenceWideHourlyQty,
                InstQty = line.LicenceWideInstQty
            };

            if (string.IsNullOrEmpty(lineCondition.Condition) || lineCondition.Condition == "-")
            {
                lineCondition = null;
            }

            var linePoint = new NaldDataPoint
            {
                PointId = line.PointId,
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
                Ngr4 = !string.IsNullOrWhiteSpace(line.Ngr4) ? line.Ngr4 : null
            };
            
            var linePeriod = new NaldDataPeriod
            {
                PeriodStart = line.PeriodStart,
                PeriodEnd = line.PeriodEnd
            };
            
            var linePurpose = new NaldDataPurpose
            {
                PurposeId = line.PurposeId,
                PurposeCode = line.PurposeCode,
                PurposeUseCode = line.PurposeUseCode,
                PurposeUseDescription = line.PurposeUseDescription
            };
            
            var stippedLicenceNumber = FormattingHelper.StripForComparison(line.LicenceNo)!;

            // Find an existing line
            if (returnList.TryGetValue(stippedLicenceNumber, out var existingItem))
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
                
                if (existingItem.Periods.All(existingPeriod => existingPeriod.ToString() != linePeriod.ToString()))
                {
                    existingItem.Periods.Add(linePeriod);
                }
                
                continue;
            }

            var lineConditionsArray = lineCondition == null
                ? new List<NaldDataAggregate>()
                : [lineCondition];

            var naldData = new NaldData
            {
                ExpiryDate = line.ExpiryDate,
                VersionStartDate = line.VersionStartDate,
                LicenceNumber = line.LicenceNo!,
                LicenceIdCharsAndDigitsOnly = stippedLicenceNumber,
                LicenceWideAnnualQty = line.LicenceWideAnnualQty,
                LicenceWideDailyQty = line.LicenceWideDailyQty,
                LicenceWideHourlyQty = line.LicenceWideHourlyQty,
                LicenceWideInstQty = line.LicenceWideInstQty,
                AggregateConditions = lineConditionsArray,
                Points = [linePoint],
                Periods = [linePeriod],
                Purposes = [linePurpose]
            };
            
            returnList.Add(stippedLicenceNumber, naldData);
        }

        return returnList;
    }

    public static void AddNaldLimitReportData(
        string? naldDataReportPath,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        if (string.IsNullOrEmpty(naldDataReportPath))
        {
            throw new NullReferenceException(nameof(naldDataReportPath));
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            ShouldSkipRecord = row =>
                string.IsNullOrEmpty(row.Row[0])
                || row.Row[0] == "Licence No."
        };

        using var reader = new StreamReader(naldDataReportPath);
        using var csv = new CsvReader(reader, config);

        var lines = csv.GetRecords<NaldLimitDataLine>().ToList();

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line.Condition) || line.Condition == "-")
            {
                continue;
            }

            var strippedLicenceNumber = FormattingHelper.StripForComparison(line.LicenceNo)!;
            var existingData = generalNaldData[strippedLicenceNumber];

            existingData.AggregateConditions.Add(new NaldDataAggregate
            {
                Type = "Limit",
                Condition = line.Condition,
                ConditionId = line.ConditionId
            });
        }
    }

    public static Dictionary<string, string> GetLicenceNumberMappingFromFilenames(string? pdfFolderPath)
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
            
            var strippedLicenceNumber = FormattingHelper.StripForComparison(licenceNumber)!;
            
            if (!returnMapping.TryAdd(strippedLicenceNumber, filename))
            {
                throw new Exception($"{filename} is a duplicate for {licenceNumber}");
            }
        }

        return returnMapping;
    }

    public static HashSet<string> GetLiveLicenceNumbers(
        string? liveLicencesReportPath)
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

            var strippedLicenceNumber = FormattingHelper.StripForComparison(licenceNumber)!;
            returnList.Add(strippedLicenceNumber);
        }

        return returnList;
    }

    public static HashSet<string> GetDeadLicenceNumbers(
        string? deadLicencesReportPath)
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

            var strippedLicenceNumber = FormattingHelper.StripForComparison(licenceNumber)!;
            returnList.Add(strippedLicenceNumber);
        }

        return returnList;
    }

    public static HashSet<string> GetImpoundmentLicenceNumbers(
        string? impoundmentLicencesReportPath)
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
            
            var strippedLicenceNumber = FormattingHelper.StripForComparison(licenceNumber)!;
            returnList.Add(strippedLicenceNumber);
        }

        return returnList;
    }
}