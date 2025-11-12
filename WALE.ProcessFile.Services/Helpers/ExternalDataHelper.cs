using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class ExternalDataHelper
{
    public static Dictionary<string, NaldData> GetNaldGeneralReportData(string? naldDataReportPath)
    {
        if (string.IsNullOrEmpty(naldDataReportPath))
        {
            throw new NullReferenceException(nameof(naldDataReportPath));
        }
        
        var processedLines = new Dictionary<string, NaldData>();
        
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
                PointName = line.PointName,
                Ngr1Cartesian = line.Ngr1Cartesian,
                Ngr1 = line.Ngr1,
                PointId = line.PointId
            };
            
            if (processedLines.TryGetValue(line.LicenceNo!, out var existingItem))
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
                
                continue;
            }

            var lineConditionsArray = lineCondition == null
                ? new List<NaldDataAggregate>()
                : [lineCondition];
            
            processedLines.Add(
                line.LicenceNo!,
                new NaldData
                {
                    ExpiryDate = line.ExpiryDate,
                    VersionStartDate = line.VersionStartDate,
                    LicenceNumber = line.LicenceNo!,
                    LicenceWideAnnualQty = line.LicenceWideAnnualQty,
                    LicenceWideDailyQty = line.LicenceWideDailyQty,
                    LicenceWideHourlyQty = line.LicenceWideHourlyQty,
                    LicenceWideInstQty = line.LicenceWideInstQty,
                    AggregateConditions = lineConditionsArray,
                    Points = [linePoint],
                });
        }
        
        return processedLines;
    }
    
    public static void AddNaldLimitReportData(
        string? naldDataReportPath,
        Dictionary<string, NaldData> generalNaldData)
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
            
            var existingData = generalNaldData[line.LicenceNo!];
            
            existingData.AggregateConditions.Add(new NaldDataAggregate
            {
                Type = "Limit",
                Condition = line.Condition,
                ConditionId = line.ConditionId
            });
        }
    }

    public static Dictionary<string, string> GetLicenceNumberMapping(
        string? licenceNumberFileMappingFilePath)
    {
        if (string.IsNullOrEmpty(licenceNumberFileMappingFilePath))
        {
            throw new NullReferenceException(nameof(licenceNumberFileMappingFilePath));
        }
        
        var returnMapping = new Dictionary<string, string>();

        var fileContents = File.Exists(licenceNumberFileMappingFilePath)
            ? File.ReadAllText(licenceNumberFileMappingFilePath)
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
            var licenceNumber = parts[1];
            var filename = parts[0].Split('/').Last();

            if (!returnMapping.TryAdd(licenceNumber, filename))
            {
                returnMapping[licenceNumber] = filename;
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
            returnList.Add(licenceNumber);
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
            returnList.Add(licenceNumber);
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

            returnList.Add(licenceNumber);
        }

        return returnList;
    }
}