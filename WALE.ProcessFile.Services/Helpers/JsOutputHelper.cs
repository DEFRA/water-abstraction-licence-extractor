using System.Text;
using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;

namespace WALE.ProcessFile.Services.Helpers;

public static class JsOutputHelper
{
    public static IntermediateOutputLicence ToOutputLine(
        Licence licence,
        DateTime dtStart,
        int completeNumber,
        int fileNumber,
        Dictionary<string, LicenceSet> licenceSetsFull)
    {
        var licenceHolder = GetValueOrDefault<string, string>(licence.NoneSchemaData, "issuedTo", null);
        var licenceHolderOcrConfidence = GetValueOrDefault<double, double?>(licence.NoneSchemaData, "issuedToConfidence", null);
        var ocr = GetValueOrDefault<string, string>(licence.NoneSchemaData,"ocr", "--");
        var serviceName = GetValueOrDefault<string[], string>(licence.NoneSchemaData,"servicesUsed", PdfDataExtractorService.Name);

        var durationInMSeconds = (int)(DateTime.Now - dtStart).TotalMilliseconds;

        var licenceNumber = licence.LicenceNumber;
        var licenceNumberOcrConfidence = GetValueOrDefault<double, double?>(licence.NoneSchemaData,"licenceNumberConfidence", null);

        var meansFound = licence.MeansOfAbstraction.Length > 0;
        var status = "Not Found";

        if (licence.LicenceFoundInList)
        {
            status = "Found";
        }

        if (licence.IsLiveLicence == true)
        {
            status = "Live";
        }

        if (licence.IsDeadLicence == true)
        {
            status = "Dead";
        }

        if (licence.IsImpoundmentLicence == true)
        {
            status = "Impound";
        }
        
        var issueDate = licence.LicenceVersion.IssueDate?.ToString("yyyy-MM-dd");
        var issuer = licence.LicenceVersion.Issuer;

        var licenceSets = licence.LicenceSets
            .Where(ls => licenceSetsFull.ContainsKey(ls.LicenceSetId!))
            .Select(ls => licenceSetsFull[ls.LicenceSetId!])
            .ToList();
        
        return new IntermediateOutputLicence
        {
            LineNumber = completeNumber,
            StartNumber = fileNumber,
            Filename = licence.Filename,
            LicenceHolder = licenceHolder,
            LicenceHolderOcrConfidence = licenceHolderOcrConfidence,
            Ocr = ocr,
            Purposes = licence.Purposes.Select(p => p.Description).ToArray(),
            Points = licence.Points.Select(p => p.Description).ToArray(),
            ServiceName = serviceName,
            Certainty = GetValueOrDefault<int, int>(licence.NoneSchemaData, "issuedToCertainty", -1),
            MatchType = GetValueOrDefault<string, string>(licence.NoneSchemaData, "issuedToMatchType", "N/A"),
            Duration = durationInMSeconds,
            MatchedLabelText = GetValueOrDefault<string, string>(licence.NoneSchemaData, "issuedToMatchedLabelText", null),
            MatchedLabelPosition = GetValueOrDefault<string, string>(licence.NoneSchemaData, "issuedToMatchLabelPosition", null),
            LicenceNumber = licenceNumber,
            NaldLicenceNumber = licence.NaldLicenceNumber,
            LicenceNumberOcrConfidence = licenceNumberOcrConfidence,
            LimitsCount = licence.AbstractionLimits.Individual?.Sum(x => x.Limits.Count) ?? 0,
            AggregatesCount = licence.AbstractionLimits.Aggregates?.Sum(x => x.Limits.Count) ?? 0,
            IssueDate = issueDate,
            Issuer = !string.IsNullOrEmpty(issuer) ? issuer : string.Empty,
            MeansFound = meansFound,
            Status = status,
            LinkedLicences = licence.LinkedLicences,
            LicenceSets = licenceSets,
            LicenceSetReferences = licence.LicenceSets
        };
    }
    
    public static async Task<IReadOnlyList<OutputListDataItem>> SaveListDataAsync(
        List<IntermediateOutputLicence> outputLines,
        string outputFolder,
        IOutputService outputService,
        bool regenerateMappingJson,
        ProcessRun processRun,
        bool saveToFile)
    {
        var resultFileStringBuilder = new StringBuilder(
            "LineNumber,StartNumber,Filename,Text,OCR,ServiceName,Certainty,MatchType,Duration,MatchedLabelText," +
            "MatchedLabelPosition,LicenceNumber,LimitsFound,LinkedLicenceNumbers,LinkedLicenceNumbersExistInDataset");

        var mappingFileStringBuilder = new StringBuilder(
            "Filename,LicenceNumber");

        var filenameToLicenceNumberMap = new Dictionary<string, string>();
        var licenceNumberToFilenameMap = new Dictionary<string, string>();

        var nodeIndex = 1;
        var nodesDictionaries = new List<Dictionary<string, object>>();
        var linksDictionaries = new List<Dictionary<string, object>>();

        var listData = new List<OutputListDataItem>();

        foreach (var outputLine in outputLines.OrderBy(x => x.Filename))
        {
            var anyLinkedLicenceNumbers = outputLine.LinkedLicences?
                .Any(lln => outputLines.Count(ol => ol.LicenceNumber == lln.LicenceNumber) > 0);

            Log(
                $"\n{outputLine.LineNumber},{outputLine.StartNumber},{outputLine.Filename}," +
                $"\"{outputLine.LicenceHolder}\",{outputLine.Ocr},{outputLine.ServiceName},{outputLine.Certainty}," +
                $"{outputLine.MatchType},{outputLine.Duration},{outputLine.MatchedLabelText}," +
                $"{outputLine.MatchedLabelPosition},{outputLine.LicenceNumber},{outputLine.LimitsCount}," +
                $"{outputLine.LinkedLicences},{anyLinkedLicenceNumbers}",
                resultFileStringBuilder);

            var filename = FileHelper.GetFilenameWithoutExtension(outputLine.Filename!);

            var listRow = new OutputListDataItem
            {
                imagePath = $"{filename}/{PdfDataExtractorService.Name}/Images/page-1.jpg",
                filename = filename,
                licenceNumber =
                    $"{outputLine.LicenceNumber}{ToPercent(outputLine.LicenceNumberOcrConfidence, outputLine.Ocr)}<br><span style=\"color: silver\">{outputLine.NaldLicenceNumber}</span>",
                licenceHolder =
                    $"{outputLine.LicenceHolder?.Replace("\"", "\\\"")}{ToPercent(outputLine.LicenceHolderOcrConfidence, outputLine.Ocr)}",
                purposes = outputLine.Purposes,
                points = outputLine.Points,
                limitsCount = outputLine.LimitsCount,
                aggregatesCount = outputLine.AggregatesCount,
                ocr = outputLine.Ocr == "OCR",
                issueDate = outputLine.IssueDate,
                issuer = outputLine.Issuer,
                meansFound = outputLine.MeansFound,
                status = outputLine.Status,
                linkedLicences = outputLine.LinkedLicences?.OrderBy(x => x.LicenceNumber).ToArray() ?? [],
                licenceSets = outputLine.LicenceSetReferences?.Select(lsr =>
                {
                    var ls = outputLine.LicenceSets!.FirstOrDefault(ls1 => ls1.LicenceSetId == lsr.LicenceSetId);

                    if (ls == null)
                    {
                        return new OutputListDataItemLicenceSet
                        {
                            LicenceSetId = "ERROR",
                            ShortLicenceSetId = "ERROR"
                        };
                    }
                    
                    var licenceSetType = lsr.LicenceSetType;

                    return new OutputListDataItemLicenceSet
                    {
                        LicenceSetId = ls.LicenceSetId,
                        ShortLicenceSetId = ls.ShortLicenceSetId,
                        LicenceSetTypes = ls.LicenceSetTypes,
                        LicenceSetType = licenceSetType

                    };
                })
                .ToArray() ?? []
            };

            listData.Add(listRow);

            if (outputLine is { LicenceNumber: not null, Filename: not null })
            {
                filenameToLicenceNumberMap.TryAdd(outputLine.Filename!, outputLine.LicenceNumber);
                licenceNumberToFilenameMap.TryAdd(outputLine.LicenceNumber, outputLine.Filename!);
            }

            outputLine.NodeId = nodeIndex++;
            var nodeName = outputLine.Filename!;

            if (!string.IsNullOrEmpty(outputLine.LicenceNumber) && outputLine.LicenceNumber != string.Empty)
            {
                nodeName = outputLine.LicenceNumber;
            }

            nodesDictionaries.Add(new Dictionary<string, object>
            {
                { "id", outputLine.NodeId },
                { "name", nodeName }
            });

            Log($"\n{outputLine.Filename},{outputLine.LicenceNumber}", mappingFileStringBuilder);
        }

        foreach (var outputLine in outputLines)
        {
            if (outputLine.LinkedLicences == null)
            {
                continue;
            }
            
            foreach (var linkedLicence in outputLine.LinkedLicences)
            {
                var linkedOutputLine = outputLines.FirstOrDefault(x => x.LicenceNumber == linkedLicence.LicenceNumber);

                if (linkedOutputLine != null)
                {
                    linksDictionaries.Add(new Dictionary<string, object>
                    {
                        { "source", outputLine.NodeId },
                        { "target", linkedOutputLine.NodeId }
                    });
                }
            }
        }

        if (!saveToFile)
        {
            return listData;
        }
        
        Directory.CreateDirectory($"{outputFolder}Additional");

        var resultFile = $"{outputFolder}Additional/{DateTime.Today:yyyyMMdd}-result.csv";
        await File.WriteAllTextAsync(resultFile, resultFileStringBuilder.ToString());

        if (regenerateMappingJson)
        {
            var licenceFilenameMapFile = $"{outputFolder}Additional/licence-number-filename-map.csv";
            await File.WriteAllTextAsync(licenceFilenameMapFile, mappingFileStringBuilder.ToString());

            var licenceFilenameMapJsonFile = $"{outputFolder}Additional/licence-number-filename-map.jsonp";
            var licenceFilenameMapDictionary = new Dictionary<string, object>
            {
                { "filenameToLicenceNumber", filenameToLicenceNumberMap },
                { "licenceNumberToFilename", licenceNumberToFilenameMap }
            };

            await File.WriteAllTextAsync(licenceFilenameMapJsonFile,
                $"var mapData = {JsonSerializer.Serialize(licenceFilenameMapDictionary, JsonHelper.GetSerializerOptions())};");
        }

        await outputService.SaveListDataAsync(listData, processRun.ProcessRunId);

        var nodeGraphData = new Dictionary<string, List<Dictionary<string, object>>>
        {
            {
                "nodes",
                nodesDictionaries
            },
            {
                "links",
                linksDictionaries
            }
        };

        var nodeGraphDataFile = $"{outputFolder}Additional/node-graph-data.jsonp";
        await File.WriteAllTextAsync(nodeGraphDataFile,
            $"var data = {JsonSerializer.Serialize(nodeGraphData, JsonHelper.GetSerializerOptions())};");
        
        return listData;
    }
    
    private static string ToPercent(double? value, string? ocr)
    {
        if (ocr != "OCR")
        {
            return string.Empty;
        }
    
        if (value == null)
        {
            return string.Empty;
        }

        return " (" + Math.Round(value.Value, 1) + "%)";
    }
    
    private static void Log(string message, StringBuilder outputStringBuilder)
    {
        //Console.WriteLine(message);
        outputStringBuilder.Append(message);
    }

    private static object? GetValue(JsonElement? jsonElement, Type desiredType)
    {
        if (jsonElement == null)
        {
            return null;
        }

        switch (jsonElement.Value.ValueKind)
        {
            case JsonValueKind.String:
                return jsonElement.Value.GetString();
            case JsonValueKind.Number:
                if (desiredType == typeof(int))
                {
                    return jsonElement.Value.GetInt32();
                }

                return jsonElement.Value.GetDouble();
        }
        
        throw new NotImplementedException();
    }
    
    private static T2? GetValueOrDefault<T, T2>(
        Dictionary<string, object> data,
        string fieldName,
        T2? defaultValue)
    {
        data.TryGetValue(fieldName, out var value);

        if (value == null)
        {
            return defaultValue;
        }

        if (value is not JsonElement element)
        {
            if (value.GetType() == typeof(JsonElement[]))
            {
                var ary = (JsonElement[])value;
                var firstElement = ary.Length >= 1 ? ary.FirstOrDefault() : (JsonElement?)null;
                
                return (T2?)(GetValue(firstElement, typeof(T2)) ?? defaultValue);
            }
            
            if (value.GetType() == typeof(string[]))
            {
                var ary = (string[])value;
                return (T2?)(ary.FirstOrDefault() ?? (object?)defaultValue);
            }
            
            if (value.GetType() == typeof(List<JsonElement>))
            {
                var list = (List<JsonElement>)value;
                var firstElement = list.Count >= 1 ? list.FirstOrDefault() : (JsonElement?)null;
                
                return (T2?)(GetValue(firstElement, typeof(T2)) ?? (object?)defaultValue);
            }
            
            if (value.GetType() == typeof(List<string>))
            {
                var list = (List<string>)value;
                return (T2?)(list.FirstOrDefault() ?? (object?)defaultValue);
            }

            if (typeof(T2) == typeof(int) && value is double dblVal)
            {
                return (T2)(object)(int)dblVal;
            }
            
            return (T2)value;
        }
        
        if (typeof(T) == typeof(string))
        {
            return (T2)(object)element.GetString()!;
        }
        if (typeof(T) == typeof(double))
        {
            return (T2)(object)element.GetDouble();
        }
        if (typeof(T) == typeof(int))
        {
            return (T2)(object)element.GetInt32();
        }
        if (typeof(T) == typeof(string[]))
        {
            var v = element.GetRawText();
            var ary = JsonSerializer.Deserialize<string[]>(v)!;

            return (T2)(object)ary.FirstOrDefault()!;
        }
        
        throw new NotSupportedException($"{typeof(T).Name} is not supported");
    }
}