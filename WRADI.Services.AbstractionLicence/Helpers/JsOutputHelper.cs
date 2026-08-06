using System.Text;
using System.Text.Json;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Strategies;
using WRADI.DocumentType.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

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
        var licenceHolderOcrConfidence =
            GetValueOrDefault<double, double?>(licence.NoneSchemaData, "issuedToConfidence", null);
        var ocr = GetValueOrDefault<string, string>(licence.NoneSchemaData, "ocr", "--");
        var serviceName = GetValueOrDefault<string[], string>(licence.NoneSchemaData, "servicesUsed",
            GeneralConstants.PdfPigDataExtractorServiceName);

        var durationInMSeconds = (int)(DateTime.Now - dtStart).TotalMilliseconds;

        var licenceNumber = licence.LicenceNumber;
        var licenceNumberOcrConfidence =
            GetValueOrDefault<double, double?>(licence.NoneSchemaData, "licenceNumberConfidence", null);

        var meansFound = licence.MeansOfAbstraction.Length > 0;
        var status = "Not Found";

        if (licence.NaldStatus == NaldLicenceStatus.Live)
        {
            status = "Live";
        }

        if (licence.NaldStatus is NaldLicenceStatus.Expired or NaldLicenceStatus.Revoked or NaldLicenceStatus.Lapsed)
        {
            status = "Dead";
        }

        if (licence.LicenceType == LicenceType.Impoundment)
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
            MatchedLabelText =
                GetValueOrDefault<string, string>(licence.NoneSchemaData, "issuedToMatchedLabelText", null),
            MatchedLabelPosition =
                GetValueOrDefault<string, string>(licence.NoneSchemaData, "issuedToMatchLabelPosition", null),
            LicenceNumber = licenceNumber?.Value,
            LicenceNumberOcrConfidence = licenceNumberOcrConfidence,
            LimitsCount = licence.AbstractionLimits.Individual?.Sum(x => x.Limits.Count) ?? 0,
            AggregatesCount = licence.AbstractionLimits.Aggregates?.Sum(x => x.Limits.Count) ?? 0,
            IssueDate = issueDate,
            Issuer = !string.IsNullOrEmpty(issuer) ? issuer : string.Empty,
            MeansFound = meansFound,
            Status = status,
            LinkedLicences = licence.LinkedLicences,
            LicenceSets = licenceSets,
            LicenceSetReferences = licence.LicenceSets,
            DmsFileId = licence.DmsFileId
        };
    }

    public static IReadOnlyList<OutputListDataItem> ToListData(List<IntermediateOutputLicence> outputLines,
        int processRunId,
        Dictionary<string, LicenceVerificationLookups> verificationLookups,
        Dictionary<Guid, string> fileIdToLicenceNumberMapping)
    {
        var listData = new List<OutputListDataItem>();

        var verificationOutputStrategies =
            new List<IVerificationOutputStrategy>
            {
                new LinkedLicencesVerificationOutputStrategy(),
                new AggregatesVerificationOutputStrategy()
            }.ToDictionary(s => s.SectionName);

        var verificationSectionNames = verificationLookups.Keys.ToList();

        var orderedOutputLines = outputLines
            .OrderBy(ol => ol.Filename)
            .ToList();

        foreach (var outputLine in orderedOutputLines)
        {
            var filenameNoExtension = FileHelper.GetFilenameWithoutExtension(outputLine.Filename!);

            var licenceSets = outputLine.LicenceSetReferences?
                .Select(lsr =>
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
                .ToArray() ?? [];

            var listRow = new OutputListDataItem
            {
                processRunId = processRunId,
                filename = filenameNoExtension,
                fileId = outputLine.DmsFileId!.Value,
                licenceNumber =
                    $"{outputLine.LicenceNumber}{ToPercent(outputLine.LicenceNumberOcrConfidence, outputLine.Ocr)}",
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
                linkedLicences = outputLine.LinkedLicences?.OrderBy(ll => ll.LicenceNumber).ToArray() ?? [],
                licenceSets = licenceSets
            };

            foreach (var sectionName in verificationSectionNames)
            {
                if (!verificationOutputStrategies.TryGetValue(sectionName, out var strategy)
                    || !verificationLookups.TryGetValue(sectionName, out var sectionVerificationLookups))
                {
                    continue;
                }

                strategy.HandleVerifications(listRow, sectionVerificationLookups, outputLine.DmsFileId!.Value,
                    outputLine.LicenceNumber!, fileIdToLicenceNumberMapping);
            }

            listData.Add(listRow);
        }

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
        Dictionary<string, object?> data,
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

            if (value is T2)
            {
                return (T2)value;
            }

            var targetType = typeof(T2);
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                targetType = Nullable.GetUnderlyingType(targetType);
            }

            try
            {
                return (T2)Convert.ChangeType(value, targetType!);
            }
            catch
            {
                // Fallback to original behavior if conversion fails
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