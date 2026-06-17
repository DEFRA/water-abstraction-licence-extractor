using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Helpers;
using Date = WALE.ProcessFile.Services.Formats.Date;

namespace WALE.ProcessFile.Services.Converters;

public static partial class WalSchemaConverter
{
    private static async Task<Licence> ToLicenceAsync(
        MatchesResult matchesResult,
        NaldLicenceStatusData naldLicenceStatusData,
        DmsFileData? dmsFileData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        Dictionary<string, List<NaldData>> naldData,
        NaldLinkedLicenceHelper? naldLinkedLicenceHelper,
        LookupConfiguration? lookupConfiguration,
        int processRunId)
    {
        var dmsFileIdInfo = await RecordFileIdAsync(
            dmsFileData,
            lookupConfiguration,
            processRunId);
        
        var matches = matchesResult.Matches;
        var regionCode = matchesResult.RegionCode;

        if (matches == null)
        {
            throw new Exception("No match object exists to convert");
        }

        var noneSchemaData = new Dictionary<string, object?>();

        if (matchesResult.AdditionalInformation != null)
        {
            foreach (var kvp in matchesResult.AdditionalInformation)
            {
                noneSchemaData.Add(kvp.Key, kvp.Value);
            }
        }

        var hasMultipleScheduleOfConditions = matches
            .Any(result => result.LabelGroupName == "ScheduleOfConditionsB");

        noneSchemaData.TryAdd(TemplateFeatures.MultipleScheduleOfConditions, hasMultipleScheduleOfConditions);

        var (licenceNumber, scrapedLicenceNumber, confidence, ocrConfidence) =
            GetLicenceNumber(matchesResult, noneSchemaData);

        var licenceNumberWithConfidence = !string.IsNullOrEmpty(licenceNumber)
            ? new ValueWithConfidence<string>(
                licenceNumber,
                ocrConfidence,
                confidence)
            : null;

        if (dmsFileData != null)
        {
            licenceNumberWithConfidence ??= new ValueWithConfidence<string>(
                dmsFileData.NaldLicenceRef,
                null,
                100.0);
        }

        var naldDataLine = GetNaldDataLine(naldData, licenceNumber, regionCode);
        var licenceVersion = GetLicenceVersion(matches, naldDataLine, noneSchemaData, dmsFileIdInfo);

        var means = GetMeansOfAbstraction(
            matches,
            ref noneSchemaData);

        var points = GetPoints(
            matches,
            naldDataLine,
            ref noneSchemaData);

        var purposes = GetPurposes(
            matches,
            naldDataLine,
            ref noneSchemaData);

        var periods = GetPeriods(
            matches,
            naldDataLine,
            ref noneSchemaData);

        var (aggregates, individual) = GetAbstractionLimits(
            matches,
            licenceNumber,
            licenceVersion.LicenceVersionId,
            points,
            purposes,
            naldDataLine,
            licenceNumbersMapping,
            naldLicenceStatusData,
            naldData,
            matchesResult.RegionCode,
            ref noneSchemaData);

        var companyNameMatch = matchesResult.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "Company");

        if (companyNameMatch != null)
        {
            var companyNameMatchType = companyNameMatch.MatchedPosition;
            noneSchemaData.Add("issuedToMatchType", companyNameMatchType.ToString());

            var issuedTo = companyNameMatch
                .Text?
                .FirstOrDefault()?
                .Text;

            if (!string.IsNullOrEmpty(issuedTo))
            {
                noneSchemaData.Add("issuedTo", issuedTo);
            }

            noneSchemaData.Add("issuedToConfidence", companyNameMatch.Confidence);

            var issuedToMatchedLabelText = companyNameMatch.MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text ?? string.Empty;
            noneSchemaData.Add("issuedToMatchedLabelText", issuedToMatchedLabelText);

            var issuedToMatchLabelPosition = companyNameMatch.MatchedLabel?.Position.ToString() ?? "--";
            noneSchemaData.Add("issuedToMatchLabelPosition", issuedToMatchLabelPosition);

            var issuedToCertainty = (int)companyNameMatchType / 100;
            noneSchemaData.Add("issuedToCertainty", issuedToCertainty);
        }

        var ocr = matchesResult.ScannedFile ? "OCR" : "NoOCR";
        noneSchemaData.Add("ocr", ocr);

        noneSchemaData.Add("servicesUsed", matchesResult.ServicesUsed.ToArray());

        var (naldStatus, licenceType) = GetLicenceStatusAndType(
            licenceNumber,
            naldLicenceStatusData,
            naldDataLine,
            regionCode);

        var linkedLicences = new List<LinkedLicence>();
        
        if (naldLinkedLicenceHelper != null)
        {
            var naldLinkedLicences =
                naldLinkedLicenceHelper.GetLinkedLicences(licenceNumber);

            foreach (var naldLinkedLicence in naldLinkedLicences)
            {
                FormattingHelper.GetDmsFileData(
                    naldLinkedLicence.NaldLicence.LicenceNumber,
                    naldLinkedLicence.NaldLicence.RegionCode,
                    licenceNumbersMapping,
                    out var thisDmsFileData);

                var outputLicenceType = LicenceType.Unknown;

                if (naldLinkedLicence.NaldLicence.Type == Core.Enums.LicenceType.Impoundment)
                {
                    outputLicenceType = LicenceType.Impoundment;
                }
                else if (naldLinkedLicence.NaldLicence.Type == Core.Enums.LicenceType.SurfaceWaterAbstraction)
                {
                    outputLicenceType = LicenceType.SurfaceWaterAbstraction;
                }
                else if (naldLinkedLicence.NaldLicence.Type == Core.Enums.LicenceType.GroundWaterAbstraction)
                {
                    outputLicenceType = LicenceType.GroundWaterAbstraction;
                }
                else if (naldLinkedLicence.NaldLicence.Type == Core.Enums.LicenceType.Abstraction)
                {
                    outputLicenceType = LicenceType.Abstraction;
                }

                linkedLicences.Add(new LinkedLicence
                {
                    LicenceNumber = naldLinkedLicence.NaldLicence.LicenceNumber,
                    RegionId = naldLinkedLicence.NaldLicence.RegionCode,
                    PermitNumber = thisDmsFileData?.PermitNumber,
                    DmsPath = thisDmsFileData?.DmsPath,
                    LicenceType = outputLicenceType,
                    ContainedIn =
                    [
                        new LinkedLicenceSection
                        {
                            Source = LinkedLicenceSource.Nald,
                            Direction = naldLinkedLicence.LinkType == NaldLinkedLicenceType.Incoming
                                ? LinkedLicenceDirection.Incoming
                                : LinkedLicenceDirection.Outgoing,
                            LinkReason = GetLinkReason(
                                naldLinkedLicence.FromFieldText,
                                naldLinkedLicence.LinkType == NaldLinkedLicenceType.Incoming
                                    ? licenceNumber
                                    : naldLinkedLicence.NaldLicence.LicenceNumber),
                            SectionName = naldLinkedLicence.FromField
                        }
                    ]
                });
            }
        }

        linkedLicences.AddRange(aggregates
            .Where(x => x.LinkedLicences?.Length >= 1)
            .SelectMany(x => x.LinkedLicences!));

        linkedLicences.AddRange(GetRecordsLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData));
        
        linkedLicences.AddRange(GetFurtherConditionsLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData));
        
        linkedLicences.AddRange(GetFurtherProvisionsLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData));
        
        linkedLicences.AddRange(GetAdditionalInformationLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData));
        
        linkedLicences.AddRange(GetPurposesLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData));
        
        linkedLicences.AddRange(GetPointsLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData));
        
        linkedLicences.AddRange(GetReasonsForConditionsLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData));
        
        linkedLicences.AddRange(GetOtherConditionsLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData));

        var licenceHistory = GetLicenceHistoryLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData);
        
        // NOTE - We don't want to include licence history licences in our output, we just want to check against them

        linkedLicences = ConsolidateLinkedLicences(
            linkedLicences,
            licenceNumber!,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping);

        var anywhereInDocumentLinkedLicences = GetAnywhereInDocumentLinkedLicences(
            matches,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping,
            matchesResult.RegionCode,
            noneSchemaData);

        var additionalLinkedLicenceCount = 1;

        foreach (var anywhereInDocumentLinkedLicence in anywhereInDocumentLinkedLicences)
        {
            var paddedAllDocumentLinkedLicenceNumber =
                FormattingHelper.FormatLicenceNumber(anywhereInDocumentLinkedLicence.LicenceNumber, regionCode);

            if (LicenceNumberContainsOther(licenceNumber, paddedAllDocumentLinkedLicenceNumber, regionCode))
            {
                continue;
            }

            var found = linkedLicences
                .Any(linkedLicence => LicenceNumberContainsOther(
                    paddedAllDocumentLinkedLicenceNumber,
                    linkedLicence.LicenceNumber,
                    regionCode));

            if (!found && !string.IsNullOrEmpty(scrapedLicenceNumber))
            {
                found = anywhereInDocumentLinkedLicence.LicenceNumber == scrapedLicenceNumber;
            }

            if (!found && licenceHistory.Count > 0)
            {
                // TODO this needs updating so it only excludes them if there from the licnce history line number

                found = licenceHistory
                    .Any(lhLinkedLicence =>
                    {
                        var paddedLinkedLicenceNumber =
                            FormattingHelper.FormatLicenceNumber(lhLinkedLicence.LicenceNumber, regionCode);

                        var lhLineNumber = lhLinkedLicence.ContainedIn!
                            .First(ci => ci.SectionName == "LicenceHistory").LineNumber;
                        var lhPageNumber = lhLinkedLicence.ContainedIn!
                            .First(ci => ci.SectionName == "LicenceHistory").PageNumber;

                        var onlyInLicenceHistory = anywhereInDocumentLinkedLicence.ContainedIn?
                            .All(aci =>  aci.PageNumber == lhPageNumber
                                && IsPlusOrMinusACoupleOfLines(aci.LineNumber, lhLineNumber)) == true;

                        return onlyInLicenceHistory && LicenceNumberContainsOther(
                            paddedAllDocumentLinkedLicenceNumber,
                            paddedLinkedLicenceNumber,
                            regionCode);
                    });
            }

            var stripedLinkedLicenceNumber = FormattingHelper.StripForComparison(
                anywhereInDocumentLinkedLicence.LicenceNumber,
                regionCode)!;
            
            if (stripedLinkedLicenceNumber.Length < 4)
            {
                found = true;
            }

            if (linkedLicences.Any(linkedLicence2 => LicenceNumberContainsOther(
                    linkedLicence2.LicenceNumber,
                    anywhereInDocumentLinkedLicence.LicenceNumber,
                    regionCode)))
            {
                found = true;
            }

            if (!found)
            {
                linkedLicences.Add(anywhereInDocumentLinkedLicence); // search this line

                noneSchemaData.Add(
                    $"AdditionalLinkedLicence:{additionalLinkedLicenceCount++}",
                    anywhereInDocumentLinkedLicence);
            }
        }
        
        // Limit to valid ones
        linkedLicences = linkedLicences
            .Where(linkedLicence =>
                FormattingHelper.IsValidLicenceNumber(linkedLicence.LicenceNumber!, regionCode) != false)
            .ToList();
        
        if (aggregates.Length == 0)
        {
            aggregates = null;
        }

        if (individual.Length == 0)
        {
            individual = null;
        }

        var limits = new AbstractionLimits
        {
            Aggregates = aggregates,
            Individual = individual
        };
        
        if (!string.IsNullOrEmpty(naldDataLine?.ArepEiucCode))
        {
            noneSchemaData.Add("ArepEuicCode", naldDataLine.ArepEiucCode);
        }
        
        return new Licence
        {
            Filename = matchesResult.Filename,
            DmsPath = dmsFileData?.DmsPath,
            LicenceNumber = licenceNumberWithConfidence,
            DmsPermitNumber = dmsFileData?.PermitNumber,
            DmsFileId = dmsFileData?.FileId,
            LicenceVersion = licenceVersion,
            MeansOfAbstraction = means,
            Points = points,
            Purposes = purposes,
            PeriodsOfAbstraction = periods,
            DefinitionOfYear = GetDefinitionOfYear(matches),
            AbstractionLimits = limits,
            LinkedLicences = linkedLicences.ToArray(),
            NoneSchemaData = noneSchemaData,
            NaldStatus = naldStatus,
            LicenceType = licenceType,
            RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode
        };
    }

    private static List<LinkedLicence> ConsolidateLinkedLicences(
        List<LinkedLicence> linkedLicences,
        string? licenceNumber,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping)
    {
        var tempLinkedLicences = linkedLicences
            .GroupBy(linkedLicence => (
                FormattingHelper.StripForComparison(
                    linkedLicence.LicenceNumber,
                    linkedLicence.RegionId!.Value),
                linkedLicence.RegionId!.Value))
            .Select(linkedLicencesGroup =>
            {
                var containedIn = new List<LinkedLicenceSection>();

                foreach (var linkedLicence in linkedLicencesGroup)
                {
                    if (linkedLicence.ContainedIn == null)
                    {
                        continue;
                    }

                    var sectionItems = linkedLicence.ContainedIn;

                    foreach (var sectionItem in sectionItems)
                    {
                        if (containedIn.Any(fs => fs.SectionName == sectionItem.SectionName
                            && fs.Direction == sectionItem.Direction))
                        {
                            continue;
                        }

                        // Use case for this is Additional and ReasonsForConditions sometimes being the same thing
                        // in documents
                        if (containedIn.Any(fs => 
                            sectionItem.Source != LinkedLicenceSource.Nald
                                && fs.LineNumber == sectionItem.LineNumber
                                && fs.PageNumber == sectionItem.PageNumber
                                && fs.Direction == sectionItem.Direction))
                        {
                            continue;
                        }

                        containedIn.Add(sectionItem);
                    }
                }

                var licenceNumberStr = linkedLicencesGroup
                    .FirstOrDefault(ll => !string.IsNullOrEmpty(ll.LicenceNumber))?
                    .LicenceNumber;
                
                var regionId = linkedLicencesGroup.Key.Item2;
                
                var linkedLicenceNumber = FormattingHelper.FormatLicenceNumber(
                    licenceNumberStr,
                    regionId);
                
                return (ToLinkedLicence(
                    linkedLicenceNumber,
                    linkedLicencesGroup
                        .FirstOrDefault(ll => !string.IsNullOrEmpty(ll.RawScrapedLicenceNumber))?
                        .RawScrapedLicenceNumber,
                    linkedLicencesGroup
                        .FirstOrDefault(ll => !string.IsNullOrEmpty(ll.PermitNumber))?
                        .PermitNumber,
                    linkedLicencesGroup
                        .FirstOrDefault(ll => !string.IsNullOrEmpty(ll.Filename))?
                        .Filename,
                    linkedLicencesGroup
                        .FirstOrDefault(ll => ll.Condition != null)?
                        .Condition,
                    containedIn.ToArray(),
                    naldLicenceStatusData,
                    naldData,
                    licenceNumbersMapping,
                    regionId), regionId);
            })
            .Where(linkedLicence => !LicenceNumberContainsOther(
                licenceNumber,
                linkedLicence.Item1.LicenceNumber,
                linkedLicence.regionId))
            .ToList();

        var newLinkedLicences = new List<LinkedLicence>();

        foreach (var linkedLicence in tempLinkedLicences)
        {
            if (newLinkedLicences.Any(linkedLicence2 =>
                LicenceNumberContainsOther(
                    linkedLicence2.LicenceNumber,
                    linkedLicence.Item1.LicenceNumber,
                    linkedLicence.Item1.RegionId!.Value)))
            {
                continue;
            }

            newLinkedLicences.Add(linkedLicence.Item1);
        }

        return newLinkedLicences;
    }

    // This is to workaround an outstanding issue where line numbers are sometimes out by one (it can be removed when that is confirmed fixed)
    private static bool IsPlusOrMinusACoupleOfLines(int? document1LineNumber, int? document2LineNumber)
    {
        return document1LineNumber >= document2LineNumber - 2
            && document1LineNumber <= document2LineNumber + 2;            
    }
    
    private static string? GetDateFormatConsistent(
        List<LabelGroupResult> matches,
        string labelName,
        bool setConfidence,
        Dictionary<string, object?>? noneSchemaData = null)
    {
        var text = DataHelper.GetTextFromFirstMatchByLabelGroup(matches, labelName, out var matchedLabel);

        if (setConfidence)
        {
            noneSchemaData?.Add($"Confidence:{labelName}", matchedLabel?.Confidence);
        }

        return Date.DateFormatConsistent(text);
    }

    private static LicenceVersion GetLicenceVersion(
        List<LabelGroupResult> matches,
        NaldData? naldDataLine,
        Dictionary<string, object?> noneSchemaData,
        DmsFileIdInformation? dmsFileIdInformation)
    {
        return new LicenceVersion
        {
            DmsFileIdStatus = dmsFileIdInformation?.Status,
            DmsFileIdStatusDateUtc = dmsFileIdInformation?.StatusDateUtc,
            NaldIssueNumber = naldDataLine?.IssueNo,
            NaldIncrementNumber = naldDataLine?.IncrNo,
            NaldUpdateReason = naldDataLine?.AabvType,
            NaldStatus = naldDataLine?.Status,
            NaldRevocationDate = naldDataLine?.RevocationDate,
            NaldOrigEffectiveDate = naldDataLine?.OrigEffDate,
            NaldOrigSignatureDate = naldDataLine?.OrigSigDate,
            NaldSignatureDate = naldDataLine?.LicSigDate,
            NaldEffectiveStartDate = naldDataLine?.EffStDate,
            NaldEffectiveEndDate = naldDataLine?.EffEndDate,
            EffectiveDate = Date.GetDateOrNull(
                GetDateFormatConsistent(matches,
                    "DateEffective",
                    true,
                    noneSchemaData)),
            ExpiryDate = Date.GetDateOrNull(
                GetDateFormatConsistent(matches,
                    "DateOfExpiry",
                    true,
                    noneSchemaData)),
            NaldExpiryDate = naldDataLine?.ExpiryDate,
            IssueDate = Date.GetDateOrNull(
                GetDateFormatConsistent(matches,
                    "DateOfIssue",
                    true,
                    noneSchemaData)),
            Issuer = GetTextAndSetConfidence(matches,
                "Issuer", noneSchemaData),
            OriginalIssueDate = Date.GetDateOrNull(
                GetDateFormatConsistent(matches,
                    "DateOfOriginalIssue",
                    true,
                    noneSchemaData)),
        };
    }

    private static string? GetTextAndSetConfidence(
        List<LabelGroupResult> matches,
        string labelName,
        Dictionary<string, object?> noneSchemaData)
    {
        var text = DataHelper.GetTextFromFirstMatchByLabelGroup(matches, labelName, out var matchedLabel);
        noneSchemaData.Add($"Confidence:{labelName}", matchedLabel?.Confidence);

        return text;
    }

    private static (NaldLicenceStatus status, LicenceType licenceType) GetLicenceStatusAndType(
        string? licenceNumberInNaldFormat,
        NaldLicenceStatusData naldLicenceStatusData,
        NaldData? naldData,
        int regionCode)
    {
        if (naldLicenceStatusData.LiveLicences.Count == 0
            && naldLicenceStatusData.ExpiredLicences.Count == 0
            && naldLicenceStatusData.RevokedLicences.Count == 0
            && naldLicenceStatusData.LapsedLicences.Count == 0
            && naldLicenceStatusData.ImpoundmentLicences.Count == 0)
        {
            return (NaldLicenceStatus.Unknown, LicenceType.Unknown);
        }
        
        var strippedLicenceNumbers = FormattingHelper.StripForComparisonMultipleOptions(
            licenceNumberInNaldFormat,
            regionCode);

        if (strippedLicenceNumbers.Count == 0)
        {
            return (NaldLicenceStatus.Unknown, LicenceType.Unknown);
        }

        foreach (var strippedLicenceNumber in strippedLicenceNumbers)
        {
            var isLiveLicence = naldLicenceStatusData.LiveLicences.Contains(strippedLicenceNumber);
            var isExpired = naldLicenceStatusData.ExpiredLicences.Contains(strippedLicenceNumber);
            var isRevoked = naldLicenceStatusData.RevokedLicences.Contains(strippedLicenceNumber);
            var isLapsed = naldLicenceStatusData.LapsedLicences.Contains(strippedLicenceNumber);
            var isImpoundmentLicence = naldLicenceStatusData.ImpoundmentLicences.Contains(strippedLicenceNumber);

            var status = NaldLicenceStatus.Unknown;
                
            if (isLiveLicence) status = NaldLicenceStatus.Live;
            else if (isExpired) status = NaldLicenceStatus.Expired;
            else if (isRevoked) status = NaldLicenceStatus.Revoked;
            else if (isLapsed) status = NaldLicenceStatus.Lapsed;
            else if (!isImpoundmentLicence)
            {
                continue;
            }

            LicenceType type;

            if (isImpoundmentLicence)
            {
                type = LicenceType.Impoundment;
            }
            else switch (naldData?.AsrcCode)
            {
                case "G":
                    type = LicenceType.GroundWaterAbstraction;
                    break;
                case "S":
                    type = LicenceType.SurfaceWaterAbstraction;
                    break;
                default:
                    type = LicenceType.Abstraction;
                    break;
            }
            
            return (status, type);
        }
        
        return (NaldLicenceStatus.Unknown, LicenceType.Unknown);
    }

    private static bool LicenceNumberContainsOther(string? licenceNumber1, string? licenceNumber2, int regionId)
    {
        var licenceNumberStripped1 = FormattingHelper.StripForComparison(licenceNumber1, regionId);

        if (string.IsNullOrWhiteSpace(licenceNumberStripped1))
        {
            return false;
        }

        var licenceNumberStripped2 = FormattingHelper.StripForComparison(licenceNumber2, regionId);

        if (string.IsNullOrWhiteSpace(licenceNumberStripped2))
        {
            return false;
        }

        const string r1 = "R1";
        const string r01 = "R01";

        var firstContainsR1 = licenceNumberStripped1.Contains(r1) || licenceNumberStripped1.Contains(r01);
        var secondContainsR1 = licenceNumberStripped2.Contains(r1) || licenceNumberStripped2.Contains(r01);

        var only1HasR1 = (firstContainsR1 && !secondContainsR1)
                         || (!firstContainsR1 && secondContainsR1);

        return !only1HasR1
               && (licenceNumberStripped1.Contains(licenceNumberStripped2)
                   || licenceNumberStripped2.Contains(licenceNumberStripped1));
    }

    private static LinkedLicence ToLinkedLicence(
        string? linkedLicenceNumber,
        string? scrapedLinkedLicenceNumber,
        string? linkedLicencePermitNumber,
        string? filename,
        Condition? condition,
        LinkedLicenceSection[] containedIn,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> dmsLicenceNumbersMapping,
        int? regionId)
    {
        var permitOrLicenceNumber = linkedLicencePermitNumber;
        if (string.IsNullOrWhiteSpace(permitOrLicenceNumber))
        {
            permitOrLicenceNumber = linkedLicenceNumber;
        }

        if (regionId == null)
        {
            throw new Exception("regionId is null");
        }
        
        var naldDataLine = GetNaldDataLine(naldData, permitOrLicenceNumber, regionId.Value);
        
        FormattingHelper.GetDmsFileData(
            linkedLicenceNumber,
            regionId.Value,
            dmsLicenceNumbersMapping,
            out var dmsFileData);
        
        var (naldStatus, licenceType) = GetLicenceStatusAndType(
            permitOrLicenceNumber,
            naldLicenceStatusData,
            naldDataLine,
            regionId.Value);

        return new LinkedLicence
        {
            LicenceNumber = linkedLicenceNumber,
            RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionId,
            RawScrapedLicenceNumber = scrapedLinkedLicenceNumber,
            PermitNumber = dmsFileData?.PermitNumber,
            Filename = filename,
            Condition = condition,
            ContainedIn = containedIn,
            NaldStatus = naldStatus,
            LicenceType = licenceType,
            DmsPath = dmsFileData?.DmsPath
        };
    }

    public static async Task<List<LicenceSet>> ToLicenceSetsAsync(
        MatchesResult matchesResult,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        IPdfDataExtractorService pdfDataExtractorService,
        int processRunId,
        LookupConfiguration lookupConfiguration,
        DmsFileData? dmsDataForFile = null)
    {
        var returnList = new List<LicenceSet>();

        var primaryLicence = await ToLicenceAsync(
            matchesResult,
            naldLicenceStatusData,
            dmsDataForFile,
            lookupConfiguration.AllDmsData,
            naldData,
            (NaldLinkedLicenceHelper?)lookupConfiguration.NaldLinkedLicenceHelper,
            lookupConfiguration,
            processRunId);

        var previouslyParsedPaths = new List<string> { matchesResult.Filename! };

        var linkedLicences = await GetLinkedLicencesAsync(
            matchesResult,
            primaryLicence,
            naldLicenceStatusData,
            naldData,
            pdfDataExtractorService,
            previouslyParsedPaths,
            processRunId,
            lookupConfiguration);
        
        var allLicences = new List<Licence>(linkedLicences);
        allLicences.Insert(0, primaryLicence);

        var singleLicenceOnlySet = new LicenceSet
        {
            LicenceSetTypes = [LicenceSetType.SingleLicenceOnly],
            Licences = [primaryLicence],
            AggregateSets = GetAggregateSets([primaryLicence], allLicences)
        };

        returnList.Add(singleLicenceOnlySet);

        var hasExplicitlyReferencedLicenceSet = allLicences.Count > 1
            || allLicences[0].LicenceNumber?.Value != primaryLicence.LicenceNumber?.Value;

        var explicitlyReferencedLicenceSet = hasExplicitlyReferencedLicenceSet
            ? new LicenceSet
            {
                LicenceSetTypes = [LicenceSetType.AllLicencesExplicitlyReferencedAnywhere],
                Licences = allLicences.ToArray(),
                AggregateSets = GetAggregateSets(allLicences, allLicences, true)
            }
            : null;

        if (explicitlyReferencedLicenceSet != null)
        {
            returnList.Add(explicitlyReferencedLicenceSet);
        }

        var licencesReferencedInLimits = primaryLicence.LinkedLicences
            .Where(linkedLicence =>
                linkedLicence.ContainedIn?.Any(ci =>
                    ci.SectionName == LinkedLicenceSectionNames.AbstractionLimits
                    && ci.Direction == LinkedLicenceDirection.Outgoing) == true)
            .Select(ll => ll.LicenceNumber)
            .Select(ln => allLicences.FirstOrDefault(l => l.LicenceNumber?.Value == ln))
            .Where(ln => ln != null)
            .Select(ln => ln!)
            .ToList();

        var licencesExplicitlyMentionedInLimits = licencesReferencedInLimits.Any();

        if (licencesExplicitlyMentionedInLimits)
        {
            licencesReferencedInLimits.Insert(0, primaryLicence);
        }

        var explicitlyReferencedLimitsLicenceSet = licencesExplicitlyMentionedInLimits
            ? new LicenceSet
            {
                LicenceSetTypes = [LicenceSetType.AllLicencesExplicitlyReferencedInLimits],
                Licences = licencesReferencedInLimits.ToArray(),
                AggregateSets = GetAggregateSets(licencesReferencedInLimits, allLicences)
            }
            : null;

        if (explicitlyReferencedLimitsLicenceSet != null)
        {
            if (explicitlyReferencedLimitsLicenceSet.LicenceSetId == explicitlyReferencedLicenceSet?.LicenceSetId)
            {
                var oldSet = explicitlyReferencedLicenceSet;
                var newSet = explicitlyReferencedLimitsLicenceSet;

                foreach (var newSetLicence in newSet.Licences)
                {
                    if (!oldSet.Licences
                        .Select(l => l.LicenceNumber?.Value)
                        .Contains(newSetLicence.LicenceNumber?.Value))
                    {
                        var updatedLicences = oldSet.Licences.ToList();
                        updatedLicences.Add(newSetLicence);

                        oldSet.Licences = updatedLicences.ToArray();
                    }
                }

                if (newSet.AggregateSets != null)
                {
                    foreach (var newSetAggregateSet in newSet.AggregateSets!)
                    {
                        if (oldSet.AggregateSets?
                                .Select(a => a.AggregateSetId)
                                .Contains(newSetAggregateSet.AggregateSetId) != true)
                        {
                            var updatedAggregateSets = oldSet.AggregateSets?.ToList() ?? [];
                            updatedAggregateSets.Add(newSetAggregateSet);

                            oldSet.AggregateSets = updatedAggregateSets.ToArray();
                        }
                    }
                }

                var updatedTypes = oldSet.LicenceSetTypes.ToList();
                updatedTypes.AddRange(newSet.LicenceSetTypes);
                oldSet.LicenceSetTypes = updatedTypes.ToArray();
            }
            else
            {
                returnList.Add(explicitlyReferencedLimitsLicenceSet);
            }
        }

        foreach (var licence in allLicences)
        {
            if (licence.AbstractionLimits.Aggregates != null)
            {
                PopulateAggregateSetIds(licence.AbstractionLimits.Aggregates, allLicences);
            }

            AddIncomingLinks(
                [[explicitlyReferencedLicenceSet ?? singleLicenceOnlySet]],
                false,
                naldLicenceStatusData,
                naldData,
                lookupConfiguration.AllDmsData);

            var newLicenceSetIds = new List<LicenceSetReference>
            {
                new()
                {
                    LicenceSetId = singleLicenceOnlySet.LicenceSetId,
                    LicenceSetType = singleLicenceOnlySet.LicenceSetTypes[0]
                }
            };

            if (explicitlyReferencedLicenceSet != null)
            {
                newLicenceSetIds.Add(new()
                {
                    LicenceSetId = explicitlyReferencedLicenceSet.LicenceSetId,
                    LicenceSetType = explicitlyReferencedLicenceSet.LicenceSetTypes[0]
                });
            }

            if (explicitlyReferencedLimitsLicenceSet != null)
            {
                newLicenceSetIds.Add(new()
                {
                    LicenceSetId = explicitlyReferencedLimitsLicenceSet.LicenceSetId,
                    LicenceSetType = explicitlyReferencedLimitsLicenceSet.LicenceSetTypes[0]
                });
            }

            // Add LicenceSetIds to licence
            licence.LicenceSets = newLicenceSetIds.ToArray();
        }

        return returnList;
    }

    private static async Task<DmsFileIdInformation?> RecordFileIdAsync(
        DmsFileData? dmsDataForFile,
        LookupConfiguration? lookupConfig,
        int processRunId)
    {
        if (dmsDataForFile == null
            || dmsDataForFile.FileId == Guid.Empty
            || lookupConfig == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(dmsDataForFile.DmsPath))
        {
            throw new Exception("DMS file path is null - shouldn't happen");
        }

        var beforeRecordList = lookupConfig.DmsFileIds.GetValueOrDefault(dmsDataForFile.FileId);

        var outputDmsFileIdInformation = new DmsFileIdInformation
        {
            FileId = dmsDataForFile.FileId,
            DmsFilePath = dmsDataForFile.DmsPath,
            ProcessRunId = processRunId,
            StatusDateUtc = DateTime.UtcNow
        };

        if (beforeRecordList == null)
        {
            outputDmsFileIdInformation.Status = "FirstSeen";
            
            await lookupConfig.CacheService.AddDmsFileIdInformationAsync(outputDmsFileIdInformation);
            lookupConfig.DmsFileIds.TryAdd(outputDmsFileIdInformation.FileId, [outputDmsFileIdInformation]);
        }
        else
        {
            var lastRecord = beforeRecordList
                .OrderByDescending(r => r.StatusDateUtc)
                .First();

            var noChange = lastRecord.DmsFilePath == dmsDataForFile.DmsPath;

            if (noChange)
            {
                return lastRecord;
            }
            
            var lastRecordFilenameOnly = lastRecord.DmsFilePath![(lastRecord.DmsFilePath!.LastIndexOf('/') + 1)..];
            var filenameOnly = dmsDataForFile.DmsPath![(dmsDataForFile.DmsPath.LastIndexOf('/') + 1)..];
            
            var isFilenameSame = lastRecordFilenameOnly == filenameOnly;
            outputDmsFileIdInformation.Status = isFilenameSame ? "Moved" : "Renamed";

            await lookupConfig.CacheService.AddDmsFileIdInformationAsync(outputDmsFileIdInformation);
            lookupConfig.DmsFileIds[outputDmsFileIdInformation.FileId].Add(outputDmsFileIdInformation);
        }

        return outputDmsFileIdInformation;
    }
    
    private static List<LicenceSet> AddIncomingLinks(
        IReadOnlyList<IReadOnlyList<LicenceSet>> licenceSetGroups,
        bool addImplicitLicenceSet,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping)
    {
        var returnList = new List<LicenceSet>();

        var allLicencesInSets = licenceSetGroups
            .SelectMany(ls => ls)
            .SelectMany(ls => ls.Licences)
            .GroupBy(l => l.LicenceNumber?.Value)
            .Select(lg => lg.First())
            .ToList();

        foreach (var licenceSetGroup in licenceSetGroups)
        {
            foreach (var licenceSet in licenceSetGroup)
            {
                foreach (var licence in licenceSet.Licences)
                {
                    if (licence.Status == LicenceStatus.NotFound)
                    {
                        continue;
                    }

                    var incomingLinks = GetLicencesReferencingLicenceInDocument(
                        allLicencesInSets,
                        licence.LicenceNumber?.Value!);

                    var outgoingLinks = licence.LinkedLicences.Select(lll => lll.LicenceNumber!).ToList();

                    var incomingAndOutgoingLinks = new List<string>(incomingLinks.Select(l => l.LicenceNumber));
                    incomingAndOutgoingLinks.AddRange(outgoingLinks);

                    foreach (var incomingLink in incomingLinks)
                    {
                        // If already output, don't add again
                        if (licence.LinkedLicences.Any(ll =>
                            ll.LicenceNumber == incomingLink.LicenceNumber &&
                            ll.ContainedIn?.Any(ci => ci.Direction == LinkedLicenceDirection.Incoming) == true))
                        {
                            continue;
                        }

                        var newSections = incomingLink.Sections
                            .Select(section => new LinkedLicenceSection
                            {
                                Source = LinkedLicenceSource.OtherDocument,
                                Direction = LinkedLicenceDirection.Incoming,
                                SectionName = section.SectionName,
                                LinkReason = section.LinkReason,
                                LineNumber = section.LineNumber,
                                PageNumber = section.PageNumber
                            })
                            .ToArray();

                        var incomingLinkedLicence = ToLinkedLicence(
                            incomingLink.LicenceNumber,
                            incomingLink.ScrapedLicenceNumber,
                            null,
                            incomingLink.Filename,
                            null,
                            newSections,
                            naldLicenceStatusData,
                            naldData,
                            licenceNumbersMapping,
                            licence.RegionId);

                        licence.LinkedLicences = new List<LinkedLicence>(licence.LinkedLicences)
                        {
                            incomingLinkedLicence 
                        }.ToArray();

                        if (!addImplicitLicenceSet)
                        {
                            continue;
                        }

                        var implicitGroupExists = licenceSetGroup.Any(lsg =>
                            lsg.LicenceSetTypes[0] == LicenceSetType.AllLicencesIncludingImplicitlyReferenced);

                        if (implicitGroupExists)
                        {
                            continue;
                        }

                        var implicitLicences = new List<Licence>
                        {
                            licence
                        };

                        implicitLicences.AddRange(
                            GetLicencesFromStrings(allLicencesInSets, incomingAndOutgoingLinks));

                        var implicitLicenceSet = new LicenceSet
                        {
                            LicenceSetTypes = [LicenceSetType.AllLicencesIncludingImplicitlyReferenced],
                            Licences = implicitLicences.ToArray(),
                            AggregateSets = GetAggregateSets(implicitLicences, allLicencesInSets)
                        };

                        returnList.Add(implicitLicenceSet);

                        var newLicenceSetIds = new List<LicenceSetReference>(licence.LicenceSets)
                        {
                            new()
                            {
                                LicenceSetId = implicitLicenceSet.LicenceSetId,
                                LicenceSetType = implicitLicenceSet.LicenceSetTypes[0]
                            }
                        };

                        licence.LicenceSets = newLicenceSetIds.ToArray();
                    }

                    licence.LinkedLicences = ConsolidateLinkedLicences(
                        licence.LinkedLicences.ToList(),
                        licence.LicenceNumber?.Value,
                        naldLicenceStatusData,
                        naldData,
                        licenceNumbersMapping).ToArray();
                }
            }
        }

        returnList = returnList
            .GroupBy(i => i.LicenceSetId)
            .Select(g => g.First())
            .ToList();

        return returnList;
    }
    
    private static Licence[] GetLicencesFromStrings(
        IEnumerable<Licence> licences,
        IReadOnlyList<string> licenceNumbers)
    {
        var returnList = new List<Licence>();

        foreach (var licence in licences)
        {
            if (!licenceNumbers.Contains(licence.LicenceNumber?.Value))
            {
                continue;
            }

            returnList.Add(licence);
        }

        return returnList.ToArray();
    }

    private static List<(
        string LicenceNumber,
        string ScrapedLicenceNumber,
        string? Filename,
        List<LinkedLicenceSection> Sections)>
        GetLicencesReferencingLicenceInDocument(IEnumerable<Licence> licences, string licenceNumber)
    {
        var returnList = new List<(string, string, string?, List<LinkedLicenceSection>)>();

        foreach (var licence in licences)
        {
            if (licence.LicenceNumber?.Value == licenceNumber)
            {
                continue;
            }
            
            var outgoingLinkedLicences = licence.LinkedLicences
                .Where(
                    lll => lll.LicenceNumber == licenceNumber
                        && lll.ContainedIn!.Any(ci => ci is {
                            Source: LinkedLicenceSource.Document,
                            Direction: LinkedLicenceDirection.Outgoing
                        })
                    )
                .ToList();

            if (!outgoingLinkedLicences.Any())
            {
                continue;
            }

            var sections = outgoingLinkedLicences
                .Where(oll => oll.ContainedIn != null)
                .SelectMany(oll => oll.ContainedIn!)
                .Where(ci => ci is
                    {
                        Source: LinkedLicenceSource.Document,
                        Direction: LinkedLicenceDirection.Outgoing
                    })
                .ToList();
            
            returnList.Add((
                licence.LicenceNumber!.Value!,
                licence.LicenceNumber!.Value!,
                licence.Filename,
                sections));
        }

        return returnList;
    }

    private static void PopulateAggregateSetIds(Aggregate[] licenceAggregates, IReadOnlyList<Licence> allLicences)
    {
        licenceAggregates
            .Where(aggregate => aggregate.AggregateSetId == PositionConstants.ReplacementMarker)
            .ToList()
            .ForEach(aggregate =>
            {
                var aggregateSet = new AggregateSet
                {
                    Aggregates = [aggregate]
                };

                aggregateSet.SetAggregateSetId(allLicences);
                aggregate.AggregateSetId = aggregateSet.AggregateSetId;
            });
    }

    private static AggregateSet[]? GetAggregateSets(
        IReadOnlyList<Licence> licences,
        IReadOnlyList<Licence> allLicences,
        bool excludeAnyLinksNotInSet = false)
    {
        var aggregates = new List<Aggregate>();

        foreach (var licence in licences)
        {
            if (licence.AbstractionLimits.Aggregates == null)
            {
                continue;
            }

            var relevantAggregates = licence.AbstractionLimits.Aggregates;
            if (excludeAnyLinksNotInSet)
            {
                relevantAggregates = relevantAggregates
                    .Where(agg => agg.LinkedLicences == null
                        || agg.LinkedLicences.Length == 0
                        || agg.LinkedLicences.All(
                            ll => licences.Any(l => l.LicenceNumber?.Value == ll.LicenceNumber)))
                    .ToArray();
            }

            aggregates.AddRange(relevantAggregates);
        }

        var aggregatesGroupedByLicencesList = aggregates
            .GroupBy(aggregate => string.Join(',', (aggregate.LinkedLicences ?? []).OrderBy(y => y.LicenceNumber)))
            .ToList();

        var aggregateSets = new List<AggregateSet>();

        foreach (var aggregatesGroupedByLicences in aggregatesGroupedByLicencesList)
        {
            var aggregateSet = new AggregateSet
            {
                Aggregates = aggregatesGroupedByLicences.ToArray()
            };

            aggregateSet.SetAggregateSetId(allLicences);
            aggregateSets.Add(aggregateSet);
        }

        return aggregateSets.Count == 0 ? null : aggregateSets.ToArray();
    }

    private static async Task<List<Licence>> GetLinkedLicencesAsync(
        MatchesResult matchesResult,
        Licence primaryLicence,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        IPdfDataExtractorService pdfDataExtractorService,
        List<string> previouslyParsedFiles,
        int processRunId,
        LookupConfiguration lookupConfiguration)
    {
        var returnLicences = new List<Licence>();

        var abstractionLimits = matchesResult.Matches?
            .FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        var abstractionLimitsPoints = abstractionLimits?.SubResults;

        if (abstractionLimitsPoints != null)
        {
            foreach (var abstractionLimitsPoint in abstractionLimitsPoints)
            {
                var abstractionLimitPointSubs = abstractionLimitsPoint.SubResults;

                foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
                {
                    var linkedLicencesData = abstractionLimitPointSub.SubResults
                        .Where(subResult =>
                            subResult.MatchedLabel!.Format == Formats.LinkedLicence.Constant)
                        .ToList();

                    foreach (var linkedLicenceData in linkedLicencesData)
                    {
                        var matches = ToMatchesResult(linkedLicenceData);
                        var (licenceNumber, _, _, _) = GetLicenceNumber(matches);
                        
                        FormattingHelper.GetDmsFileData(
                            licenceNumber,
                            matchesResult.RegionCode,
                            lookupConfiguration.AllDmsData,
                            out var dmsFileData);
                        
                        var linkedLicence = await ToLicenceAsync(
                            matches,
                            naldLicenceStatusData,
                            dmsFileData,
                            lookupConfiguration.AllDmsData,
                            naldData,
                            (NaldLinkedLicenceHelper?)lookupConfiguration.NaldLinkedLicenceHelper,
                            lookupConfiguration,
                            processRunId);

                        returnLicences.Add(linkedLicence);
                    }

                    var linkedLicenceNumbers = abstractionLimitPointSub.SubResults
                        .Where(subResult =>
                            subResult.MatchedLabel!.Name == "LinkedLicenceNumber")
                        .ToList();

                    foreach (var linkedLicencesNumberResult in linkedLicenceNumbers)
                    {
                        var licenceNumber = linkedLicencesNumberResult.Text?.FirstOrDefault()?.Text;

                        var licenceNumberTransformed =
                            FormattingHelper.FormatLicenceNumber(licenceNumber, matchesResult.RegionCode);

                        // Don't process ones we've already found
                        if (licenceNumberTransformed == primaryLicence.LicenceNumber?.Value
                            || returnLicences.Any(licence => licence.LicenceNumber?.Value == licenceNumberTransformed))
                        {
                            continue;
                        }

                        var foundDmsData = FormattingHelper.GetDmsFileData(
                            licenceNumber,
                            matchesResult.RegionCode,
                            lookupConfiguration.AllDmsData,
                            out var dmsFileData);
                        
                        if (!foundDmsData)
                        {
                            returnLicences.Add(new Licence
                            {
                                LicenceNumber = !string.IsNullOrEmpty(licenceNumber) 
                                    ? new ValueWithConfidence<string>(licenceNumber, -1, -1) // TODO
                                    : null,
                                Status = LicenceStatus.NotFound,
                                RegionId = matchesResult.RegionCode
                            });

                            continue;
                        }

                        var destinationFileName = dmsFileData!.DestinationFileName!;

                        var clonedConfig = lookupConfiguration.Clone();
                        clonedConfig.AllDmsData = lookupConfiguration.AllDmsData;
                        clonedConfig.RegionId = matchesResult.RegionCode;
                        
                        FormattingHelper.GetDmsFileData(
                            licenceNumber,
                            matchesResult.RegionCode,
                            lookupConfiguration.AllDmsData,
                            out var linkedDmsFileData);
                        
                        if (linkedDmsFileData == null)
                        {
                            ConsoleHelper.WriteLine(
                                $"INFO - {nameof(WalSchemaConverter)} - ProcessLinkedLicenceAsync - excluding file as doesn't have file id set");
                
                            break;
                        }
                        
                        var relatedFileMatches = await pdfDataExtractorService.GetMatchesAsync(
                            destinationFileName,
                            linkedDmsFileData,
                            clonedConfig,
                            previouslyParsedFiles,
                            processRunId);

                        var licence = await ToLicenceAsync(
                            relatedFileMatches,
                            naldLicenceStatusData,
                            dmsFileData,
                            lookupConfiguration.AllDmsData,
                            naldData,
                            (NaldLinkedLicenceHelper?)lookupConfiguration.NaldLinkedLicenceHelper,
                            lookupConfiguration,
                            processRunId);

                        returnLicences.Add(licence);
                    }
                }
            }
        }

        foreach (var linkedLicence in primaryLicence.LinkedLicences)
        {
            var strippedLlNumbers = FormattingHelper.StripForComparisonMultipleOptions(
                linkedLicence.LicenceNumber,
                linkedLicence.RegionId!.Value);

            if (strippedLlNumbers.Count == 0)
            {
                continue;
            }
            
            var continueOuter = false;
            
            foreach (var strippedLlNumber in strippedLlNumbers)
            {
                // Already found it
                if (returnLicences.Any(returnLicence =>
                    FormattingHelper.StripForComparison(
                        returnLicence.LicenceNumber?.Value, returnLicence.RegionId!.Value) == strippedLlNumber))
                {
                    continueOuter = true;
                    break;
                }
            }

            if (continueOuter)
            {
                continue;
            }

            var found = FormattingHelper.GetDmsFileData(
                linkedLicence.LicenceNumber,
                matchesResult.RegionCode,
                lookupConfiguration.AllDmsData,
                out var dmsFileData);
            
            if (!found)
            {
                returnLicences.Add(new Licence
                {
                    LicenceNumber = new ValueWithConfidence<string>(linkedLicence.LicenceNumber, -1, -1),
                    Status = LicenceStatus.NotFound,
                    RegionId = matchesResult.RegionCode
                });

                continue;
            }

            var destinationFileName = dmsFileData!.DestinationFileName!;
            if (string.IsNullOrEmpty(destinationFileName))
            {
                returnLicences.Add(new Licence
                {
                    LicenceNumber = new ValueWithConfidence<string>(linkedLicence.LicenceNumber, -1, -1),
                    Status = LicenceStatus.PathMissing,
                    RegionId = matchesResult.RegionCode
                });

                continue;
            }
            
            var destinationFileId = dmsFileData.FileId;
            if (destinationFileId == Guid.Empty)
            {
                returnLicences.Add(new Licence
                {
                    LicenceNumber = new ValueWithConfidence<string>(linkedLicence.LicenceNumber, -1, -1),
                    Status = LicenceStatus.FileIdMissing,
                    RegionId = matchesResult.RegionCode
                });

                continue;
            }

            var clonedConfig = lookupConfiguration.Clone();
            clonedConfig.RegionId = matchesResult.RegionCode;
            
            var relatedFileMatches = await pdfDataExtractorService.GetMatchesAsync(
                destinationFileName,
                dmsFileData,
                clonedConfig,
                previouslyParsedFiles,
                processRunId);

            var licence = await ToLicenceAsync(
                relatedFileMatches,
                naldLicenceStatusData,
                dmsFileData,
                lookupConfiguration.AllDmsData,
                naldData,
                (NaldLinkedLicenceHelper?)lookupConfiguration.NaldLinkedLicenceHelper,
                lookupConfiguration,
                processRunId);

            returnLicences.Add(licence);
        }

        returnLicences = returnLicences
            .Where(linkedLicence =>
                FormattingHelper.IsValidLicenceNumber(
                    linkedLicence.LicenceNumber!.Value!,
                    lookupConfiguration.RegionId) != false)
            .ToList();
        
        return returnLicences;
    }

    private static TimeCutoff? GetTimeCutoff(LabelGroupResult? match)
    {
        if (match == null)
        {
            return null;
        }

        var value = match.Text?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var isFrom = value.Contains("From ", StringComparison.InvariantCultureIgnoreCase);
        var isUntil = value.Contains("Until ", StringComparison.InvariantCultureIgnoreCase);
        
        if (!isFrom && !isUntil)
        {
            return null;
        }

        var parts = value
            .Replace("From", "~", StringComparison.InvariantCultureIgnoreCase)
            .Replace("Until", "~", StringComparison.InvariantCultureIgnoreCase)
            .Split('~');

        var datePart = parts.Length >= 2 ? parts[1] : null;

        if (datePart == null)
        {
            return null;
        }
        
        return new TimeCutoff
        {
            CutoffType = isFrom ? CutoffType.From : CutoffType.Upto,
            Date = datePart.Trim()
        };
    }
    
    private static TimePeriod? GetTimePeriod(LabelGroupResult? match)
    {
        if (match == null)
        {
            return null;
        }

        var value = match.Text?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Replace(" and ending on ", " to ");
        
        var isFrom = value.Contains("From ", StringComparison.InvariantCultureIgnoreCase);
        var isUntil = value.Contains("Until ", StringComparison.InvariantCultureIgnoreCase);
        
        if (isFrom || isUntil)
        {
            return null;
        }
        
        var hasTo = value.Contains(" to ", StringComparison.InvariantCultureIgnoreCase);
        var hasBeginningOn = value.Contains("beginning on ", StringComparison.InvariantCultureIgnoreCase);

        if (!hasTo && !hasBeginningOn)
        {
            return null;
        }
        
        var parts = value
            .Replace("beginning on ", string.Empty)
            .Split(" to ");

        if (parts.Length == 1)
        {
            return null;
        }
        
        return new TimePeriod
        {
            StartDate = parts[0],
            EndDate = parts.Length > 1 ? parts[1] : null,
            PeriodType = AbstractionPeriodType.SetPeriod,
            Inclusive = true
        };
    }

    private static List<LinkedLicence> GetAdditionalInformationLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        var additional = matches
            .FirstOrDefault(result => result.LabelGroupName == "Additional");

        if (additional == null)
        {
            return [];
        }

        var count = 0;

        return additional
            .SubResults
            .SelectMany(point => point.SubResults)
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "AdditionalLinkedLicenceNumber")
            .Select(linkedLicenceNumber =>
            {
                var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;

                var naldLicenceNumber =
                    (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ?? null;

                var naldDataLine = GetNaldDataLine(naldData, licenceNumber, regionCode);
                
                var (naldStatus, licenceType) = GetLicenceStatusAndType(
                    naldLicenceNumber,
                    naldLicenceStatusData,
                    naldDataLine,
                    regionCode);

                FormattingHelper.GetDmsFileData(
                    licenceNumber,
                    regionCode,
                    licenceNumbersMapping,
                    out var dmsFileData);

                noneSchemaData.Add($"Confidence:LinkedLicence_AdditionalInformation_{count++}", linkedLicenceNumber.Confidence);
                
                return new LinkedLicence
                {
                    LicenceNumber = licenceNumber,
                    RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                    RawScrapedLicenceNumber = licenceNumber,
                    PermitNumber = dmsFileData?.PermitNumber,
                    Filename = dmsFileData?.DestinationFileName,
                    DmsPath = dmsFileData?.DmsPath,
                    NaldStatus = naldStatus,
                    LicenceType = licenceType,
                    ContainedIn =
                    [
                        new LinkedLicenceSection
                        {
                            Source = LinkedLicenceSource.Document,
                            SectionName = LinkedLicenceSectionNames.AdditionalInformation,
                            LinkReason = GetLinkReason(
                                [GetParent(additional, linkedLicenceNumber)],
                                linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                            LineNumber = linkedLicenceNumber.LineNumber,
                            PageNumber = linkedLicenceNumber.PageNumber
                        }
                    ]
                };
            })
            .ToList();
    }

    private static List<LinkedLicence> GetReasonsForConditionsLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        var reasonsForConditions = matches
            .FirstOrDefault(result => result.LabelGroupName == "ReasonsForConditions");

        if (reasonsForConditions == null)
        {
            return [];
        }

        var count = 0;
        
        return reasonsForConditions
            .SubResults
            .SelectMany(point => point.SubResults)
            .Where(linkedLicenceNumber =>
                linkedLicenceNumber.MatchedLabel?.Name == "ReasonsForConditionsLinkedLicenceNumber")
            .Select(linkedLicenceNumber =>
            {
                var naldLicenceNumber =
                    (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ?? null;

                var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;
                var naldDataLine = GetNaldDataLine(naldData, licenceNumber, regionCode);
                
                var (naldStatus, licenceType) = GetLicenceStatusAndType(
                    naldLicenceNumber,
                    naldLicenceStatusData,
                    naldDataLine,
                    regionCode);

                FormattingHelper.GetDmsFileData(
                    licenceNumber,
                    regionCode,
                    licenceNumbersMapping,
                    out var dmsFileData);

                noneSchemaData.Add($"Confidence:LinkedLicence_ReasonsForConditions_{count++}", linkedLicenceNumber.Confidence);
                
                return new LinkedLicence
                {
                    LicenceNumber = licenceNumber,
                    RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                    RawScrapedLicenceNumber = licenceNumber,
                    PermitNumber = dmsFileData?.PermitNumber,
                    Filename = dmsFileData?.DestinationFileName,
                    DmsPath = dmsFileData?.DmsPath,
                    NaldStatus = naldStatus,
                    LicenceType = licenceType,
                    ContainedIn =
                    [
                        new LinkedLicenceSection
                        {
                            Source = LinkedLicenceSource.Document,
                            SectionName = LinkedLicenceSectionNames.ReasonsForConditions,
                            LinkReason = GetLinkReason(
                                [GetParent(reasonsForConditions, linkedLicenceNumber)],
                                linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                            LineNumber = linkedLicenceNumber.LineNumber,
                            PageNumber = linkedLicenceNumber.PageNumber
                        }
                    ]
                };
            })
            .ToList();
    }
    
    private static List<LinkedLicence> GetOtherConditionsLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        var otherConditions = matches
            .FirstOrDefault(result => result.LabelGroupName == "OtherConditions");

        if (otherConditions == null)
        {
            return [];
        }

        var count = 0;
        
        return otherConditions
            .SubResults
            .SelectMany(point => point.SubResults)
            .Where(linkedLicenceNumber =>
                linkedLicenceNumber.MatchedLabel?.Name == "OtherConditionsLinkedLicenceNumber")
            .Select(linkedLicenceNumber =>
            {
                var naldLicenceNumber =
                    (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ?? null;

                var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;
                var naldDataLine = GetNaldDataLine(naldData, licenceNumber, regionCode);
                
                var (naldStatus, licenceType) = GetLicenceStatusAndType(
                    naldLicenceNumber,
                    naldLicenceStatusData,
                    naldDataLine,
                    regionCode);

                FormattingHelper.GetDmsFileData(
                    licenceNumber,
                    regionCode,
                    licenceNumbersMapping,
                    out var dmsFileData);

                noneSchemaData.Add($"Confidence:LinkedLicence_OtherConditions_{count++}", linkedLicenceNumber.Confidence);
                
                return new LinkedLicence
                {
                    LicenceNumber = licenceNumber,
                    RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                    RawScrapedLicenceNumber = licenceNumber,
                    PermitNumber = dmsFileData?.PermitNumber,
                    Filename = dmsFileData?.DestinationFileName,
                    DmsPath = dmsFileData?.DmsPath,
                    NaldStatus = naldStatus,
                    LicenceType = licenceType,
                    ContainedIn =
                    [
                        new LinkedLicenceSection
                        {
                            Source = LinkedLicenceSource.Document,
                            SectionName = LinkedLicenceSectionNames.OtherConditions,
                            LinkReason = GetLinkReason(
                                [GetParent(otherConditions, linkedLicenceNumber)],
                                linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                            LineNumber = linkedLicenceNumber.LineNumber,
                            PageNumber = linkedLicenceNumber.PageNumber
                        }
                    ]
                };
            })
            .ToList();
    }

    private static LabelGroupResult GetParent(LabelGroupResult root, LabelGroupResult child)
    {
        foreach (var item1 in root.SubResults)
        {
            if (item1.SubResults.Any(item2 => item2 == child))
            {
                return item1;
            }
        }

        throw new Exception("Cannot find parent");
    }

    private static List<LinkedLicence> GetAnywhereInDocumentLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        // TODO make these repeated methods more generic
        
        var generalLinkedLicenceNumbers = matches
            .Where(result => result.LabelGroupName == "LinkedLicenceNumber")
            .ToList();

        if (generalLinkedLicenceNumbers.Count == 0)
        {
            return [];
        }

        var count = 0;
        var returnList = new List<LinkedLicence>();

        foreach (var generalLinkedLicenceNumber in generalLinkedLicenceNumbers)
        {
            // Ignore matches near the top of the first page
            if (generalLinkedLicenceNumber is { PageNumber: 1, LineNumber: <= 3 })
            {
                continue;
            }

            var linkedLicenceNumber = generalLinkedLicenceNumber.Text?.FirstOrDefault()?.Text;

            var naldLinkedLicenceNumber =
                (string?)generalLinkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ??
                null;

            var naldDataLine = GetNaldDataLine(naldData, linkedLicenceNumber, regionCode);
            
            var (naldStatus, licenceType) = GetLicenceStatusAndType(
                naldLinkedLicenceNumber,
                naldLicenceStatusData,
                naldDataLine,
                regionCode);

            FormattingHelper.GetDmsFileData(
                linkedLicenceNumber,
                regionCode,
                licenceNumbersMapping,
                out var dmsFileData);

            noneSchemaData.Add($"Confidence:LinkedLicence_SomewhereInDocument_{count++}", generalLinkedLicenceNumber.Confidence);
            
            returnList.Add(new LinkedLicence
            {
                LicenceNumber = linkedLicenceNumber,
                RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                RawScrapedLicenceNumber = linkedLicenceNumber,
                PermitNumber = dmsFileData?.PermitNumber,
                Filename = dmsFileData?.DestinationFileName,
                DmsPath = dmsFileData?.DmsPath,
                NaldStatus = naldStatus,
                LicenceType = licenceType,
                ContainedIn =
                [
                    new LinkedLicenceSection
                    {
                        Source = LinkedLicenceSource.Document,
                        SectionName = GetUnknownSectionName(generalLinkedLicenceNumber.PageNumber),
                        LinkReason = GetLinkReason([generalLinkedLicenceNumber], linkedLicenceNumber),
                        LineNumber = generalLinkedLicenceNumber.LineNumber,
                        PageNumber = generalLinkedLicenceNumber.PageNumber
                    }
                ]
            });
        }

        return returnList;
    }

    private static string GetUnknownSectionName(int pageNumber)
    {
        return pageNumber switch
        {
            1 => LinkedLicenceSectionNames.UnknownPage1,
            2 => LinkedLicenceSectionNames.UnknownPage2,
            3 => LinkedLicenceSectionNames.UnknownPage3,
            4 => LinkedLicenceSectionNames.UnknownPage4,
            5 => LinkedLicenceSectionNames.UnknownPage5,
            6 => LinkedLicenceSectionNames.UnknownPage6,
            7 => LinkedLicenceSectionNames.UnknownPage7,
            8 => LinkedLicenceSectionNames.UnknownPage8,
            9 => LinkedLicenceSectionNames.UnknownPage9,
            _ => LinkedLicenceSectionNames.Unknown
        };
    }

    private static List<LinkedLicence> GetLicenceHistoryLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        var licenceHistorySection = matches
            .FirstOrDefault(result => result.LabelGroupName == "LicenceHistory");

        if (licenceHistorySection == null)
        {
            return [];
        }
        
        var count = 0;

        var returnList = licenceHistorySection
            .SubResults
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "LicenceHistoryLinkedLicenceNumber")
            .Select(linkedLicenceNumber =>
            {
                var lln = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;

                var naldLicenceNumber =
                    (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ?? null;

                var naldDataLine = GetNaldDataLine(naldData, lln, regionCode);
                
                var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;

                var (naldStatus, licenceType) = GetLicenceStatusAndType(
                    naldLicenceNumber,
                    naldLicenceStatusData,
                    naldDataLine,
                    regionCode);

                FormattingHelper.GetDmsFileData(
                    licenceNumber,
                    regionCode,
                    licenceNumbersMapping,
                    out var dmsFileData);
                
                noneSchemaData.Add($"Confidence:LinkedLicence_LicenceHistory_{count++}", linkedLicenceNumber.Confidence);
                
                return new LinkedLicence
                {
                    LicenceNumber = lln,
                    RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                    RawScrapedLicenceNumber = lln,
                    PermitNumber = dmsFileData?.PermitNumber,
                    Filename = dmsFileData?.DestinationFileName,
                    DmsPath = dmsFileData?.DmsPath,
                    NaldStatus = naldStatus,
                    LicenceType = licenceType,
                    ContainedIn =
                    [
                        new LinkedLicenceSection
                        {
                            Source = LinkedLicenceSource.Document,
                            SectionName = LinkedLicenceSectionNames.LicenceHistory,
                            LinkReason =
                                GetLinkReason([licenceHistorySection],
                                    lln), // We haven't split licence history into sections like the others
                            LineNumber = linkedLicenceNumber.LineNumber,
                            PageNumber = linkedLicenceNumber.PageNumber
                        }
                    ]
                };
            })
            .ToList();

        return returnList;
    }

    private static List<LinkedLicence> GetPurposesLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        var purposeSection = matches
            .FirstOrDefault(result => result.LabelGroupName == "Purpose");

        if (purposeSection == null)
        {
            return [];
        }

        var count = 0;
        var returnList = new List<LinkedLicence>();

        foreach (var purposePointGroup in purposeSection.SubResults)
        {
            var purposes = purposePointGroup.SubResults
                .Where(x => x.MatchedLabel!.Name == "Purpose")
                .ToList();

            foreach (var purpose in purposes)
            {
                returnList.AddRange(purpose.SubResults
                    .Where(linkedLicenceNumber =>
                        linkedLicenceNumber.MatchedLabel?.Name == "PurposeLinkedLicenceNumber")
                    .Select(linkedLicenceNumber =>
                    {
                        var naldLicenceNumber =
                            (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ??
                            null;
                        
                        var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;
                        var naldDataLine = GetNaldDataLine(naldData, licenceNumber, regionCode);
                        
                        var (naldStatus, licenceType) = GetLicenceStatusAndType(
                            naldLicenceNumber,
                            naldLicenceStatusData,
                            naldDataLine,
                            regionCode);

                        FormattingHelper.GetDmsFileData(
                            licenceNumber,
                            regionCode,
                            licenceNumbersMapping,
                            out var dmsFileData);

                        noneSchemaData.Add($"Confidence:LinkedLicence_Purposes_{count++}", linkedLicenceNumber.Confidence);
                        
                        return new LinkedLicence
                        {
                            PermitNumber = dmsFileData?.PermitNumber,
                            RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                            RawScrapedLicenceNumber = licenceNumber,
                            LicenceNumber = licenceNumber,
                            Filename = dmsFileData?.DestinationFileName,
                            DmsPath = dmsFileData?.DmsPath,
                            NaldStatus = naldStatus,
                            LicenceType = licenceType,
                            ContainedIn =
                            [
                                new LinkedLicenceSection
                                {
                                    Source = LinkedLicenceSource.Document,
                                    SectionName = LinkedLicenceSectionNames.Purposes,
                                    LinkReason = GetLinkReason(
                                        [GetParent(purposePointGroup, linkedLicenceNumber)],
                                        linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                                    LineNumber = linkedLicenceNumber.LineNumber,
                                    PageNumber = linkedLicenceNumber.PageNumber
                                }
                            ]
                        };
                    })
                    .ToList());
            }
        }

        return returnList;
    }

    private static List<LinkedLicence> GetPointsLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        var pointsSection = matches
            .FirstOrDefault(result => result.LabelGroupName == "Points");

        if (pointsSection == null)
        {
            return [];
        }
        
        var count = 0;
        var returnList = new List<LinkedLicence>();

        foreach (var pointPurposeGroup in pointsSection.SubResults)
        {
            var points = pointPurposeGroup.SubResults
                .Where(x => x.MatchedLabel!.Name == "Point")
                .ToList();

            foreach (var point in points)
            {
                returnList.AddRange(point.SubResults
                    .Where(linkedLicenceNumber =>
                        linkedLicenceNumber.MatchedLabel?.Name == "LinkedLicenceNumber")
                    .Select(linkedLicenceNumber =>
                    {
                        var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;

                        var naldDataLine = GetNaldDataLine(naldData, licenceNumber, regionCode);
                        
                        var naldLicenceNumber =
                            (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ??
                            null;

                        var (naldStatus, licenceType) = GetLicenceStatusAndType(
                            naldLicenceNumber,
                            naldLicenceStatusData,
                            naldDataLine,
                            regionCode);

                        FormattingHelper.GetDmsFileData(
                            licenceNumber,
                            regionCode,
                            licenceNumbersMapping,
                            out var dmsFileData);

                        noneSchemaData.Add($"Confidence:LinkedLicence_Points_{count++}", linkedLicenceNumber.Confidence);
                        
                        return new LinkedLicence
                        {
                            LicenceNumber = licenceNumber,
                            RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                            RawScrapedLicenceNumber = licenceNumber,
                            PermitNumber = dmsFileData?.PermitNumber,
                            Filename = dmsFileData?.DestinationFileName,
                            DmsPath = dmsFileData?.DmsPath,
                            NaldStatus = naldStatus,
                            LicenceType = licenceType,
                            ContainedIn =
                            [
                                new LinkedLicenceSection
                                {
                                    Source = LinkedLicenceSource.Document,
                                    SectionName = LinkedLicenceSectionNames.Points,
                                    LinkReason = GetLinkReason(
                                        [GetParent(pointPurposeGroup, linkedLicenceNumber)],
                                        linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                                    LineNumber = linkedLicenceNumber.LineNumber,
                                    PageNumber = linkedLicenceNumber.PageNumber
                                }
                            ]
                        };
                    })
                    .ToList());
            }
        }

        return returnList;
    }

    private static List<LinkedLicence> GetRecordsLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        var records = matches
            .FirstOrDefault(result => result.LabelGroupName == "Records");

        if (records?.SubResults == null || records.SubResults.Count == 0)
        {
            return [];
        }

        var count = 0;

        return records
            .SubResults
            .SelectMany(subResult => subResult.SubResults)
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "RecordsLinkedLicenceNumber")
            .Select(linkedLicenceNumber =>
            {
                var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;

                var naldDataLine = GetNaldDataLine(naldData, licenceNumber, regionCode);
                
                var naldLicenceNumber =
                    (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ?? null;

                var (naldStatus, licenceType) = GetLicenceStatusAndType(
                    naldLicenceNumber,
                    naldLicenceStatusData,
                    naldDataLine,
                    regionCode);

                FormattingHelper.GetDmsFileData(
                    licenceNumber,
                    regionCode,
                    licenceNumbersMapping,
                    out var dmsFileData);

                noneSchemaData.Add($"Confidence:LinkedLicence_Records_{count++}", linkedLicenceNumber.Confidence);
                
                return new LinkedLicence
                {
                    Filename = dmsFileData?.DestinationFileName,
                    RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                    DmsPath = dmsFileData?.DmsPath,
                    LicenceNumber = licenceNumber,
                    RawScrapedLicenceNumber = licenceNumber,
                    PermitNumber = dmsFileData?.PermitNumber,
                    NaldStatus = naldStatus,
                    LicenceType = licenceType,
                    ContainedIn =
                    [
                        new LinkedLicenceSection
                        {
                            Source = LinkedLicenceSource.Document,
                            SectionName = LinkedLicenceSectionNames.Records,
                            LinkReason = GetLinkReason(
                                [GetParent(records, linkedLicenceNumber)],
                                linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                            LineNumber = linkedLicenceNumber.LineNumber,
                            PageNumber = linkedLicenceNumber.PageNumber
                        }
                    ]
                };
            })
            .ToList();
    }

    private static List<LinkedLicence> GetFurtherConditionsLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        var furtherConditions = matches
            .FirstOrDefault(result => result.LabelGroupName == "FurtherConditions");

        if (furtherConditions == null)
        {
            return [];
        }

        var count = 0;
        
        return furtherConditions
            .SubResults
            .SelectMany(subResult => subResult.SubResults)
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "FCLinkedLicenceNumber")
            .Select(linkedLicenceNumber =>
            {
                var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;

                var naldLicenceNumber =
                    (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ?? null;

                var naldDataLine = GetNaldDataLine(naldData, licenceNumber, regionCode);
                
                var (naldStatus, licenceType) = GetLicenceStatusAndType(
                    naldLicenceNumber,
                    naldLicenceStatusData,
                    naldDataLine,
                    regionCode);

                FormattingHelper.GetDmsFileData(
                    licenceNumber,
                    regionCode,
                    licenceNumbersMapping,
                    out var dmsFileData);

                noneSchemaData.Add($"Confidence:LinkedLicence_FurtherConditions_{count++}", linkedLicenceNumber.Confidence);
                
                return new LinkedLicence
                {
                    LicenceNumber = licenceNumber,
                    RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                    RawScrapedLicenceNumber = licenceNumber,
                    PermitNumber = dmsFileData?.PermitNumber,
                    Filename = dmsFileData?.DestinationFileName,
                    DmsPath = dmsFileData?.DmsPath,
                    NaldStatus = naldStatus,
                    LicenceType = licenceType,
                    ContainedIn =
                    [
                        new LinkedLicenceSection
                        {
                            Source = LinkedLicenceSource.Document,
                            SectionName = LinkedLicenceSectionNames.FurtherConditions,
                            LinkReason = GetLinkReason(
                                [GetParent(furtherConditions, linkedLicenceNumber)],
                                linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                            LineNumber = linkedLicenceNumber.LineNumber,
                            PageNumber = linkedLicenceNumber.PageNumber
                        }
                    ]
                };
            })
            .ToList();
    }

    private static List<LinkedLicence> GetFurtherProvisionsLinkedLicences(
        List<LabelGroupResult> matches,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        int regionCode,
        Dictionary<string, object?> noneSchemaData)
    {
        var furtherProvisions = matches
            .FirstOrDefault(result => result.LabelGroupName == "FurtherProvisions");

        if (furtherProvisions == null)
        {
            return [];
        }
        
        var count = 0;

        return furtherProvisions
            .SubResults
            .SelectMany(subResult => subResult.SubResults)
            .Where(linkedLicenceNumber =>
                linkedLicenceNumber.MatchedLabel?.Name == "FurtherProvisionsLinkedLicenceNumber")
            .Select(linkedLicenceNumber =>
            {
                var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;

                var naldLicenceNumber =
                    (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ?? null;

                var naldDataLine = GetNaldDataLine(naldData, licenceNumber, regionCode);
                
                var (naldStatus, licenceType) = GetLicenceStatusAndType(
                    naldLicenceNumber,
                    naldLicenceStatusData,
                    naldDataLine,
                    regionCode);

                FormattingHelper.GetDmsFileData(
                    licenceNumber,
                    regionCode,
                    licenceNumbersMapping,
                    out var dmsFileData);
                
                noneSchemaData.Add($"Confidence:LinkedLicence_FurtherProvisions_{count++}", linkedLicenceNumber.Confidence);
                
                return new LinkedLicence
                {
                    LicenceNumber = licenceNumber,
                    RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                    RawScrapedLicenceNumber = licenceNumber,
                    PermitNumber = dmsFileData?.PermitNumber,
                    Filename = dmsFileData?.DestinationFileName,
                    DmsPath = dmsFileData?.DmsPath,
                    NaldStatus = naldStatus,
                    LicenceType = licenceType,
                    ContainedIn =
                    [
                        new LinkedLicenceSection
                        {
                            Source = LinkedLicenceSource.Document,
                            SectionName = LinkedLicenceSectionNames.FurtherProvisions,
                            LinkReason = GetLinkReason(
                                [GetParent(furtherProvisions, linkedLicenceNumber)],
                                linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                            LineNumber = linkedLicenceNumber.LineNumber,
                            PageNumber = linkedLicenceNumber.PageNumber
                        }
                    ]
                };
            })
            .ToList();
    }

    private static string? GetLinkReason(List<LabelGroupResult> sections, string? linkedLicenceNumber)
    {
        foreach (var section in sections)
        {
            var text = string.Join('\n', section.Text!.Select(t => t.Text));
            var result = GetLinkReason(text, linkedLicenceNumber);

            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        return null;
    }

    private static string? GetLinkReason(string? text, string? linkedLicenceNumber)
    {
        if (string.IsNullOrEmpty(linkedLicenceNumber)
            || string.IsNullOrEmpty(text)
            || !text.Contains(linkedLicenceNumber))
        {
            return null;
        }

        if (text.Contains("lapsed licence", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.LapsedLicence;
        }

        if (text.Contains("discharge and re-abstraction", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.DischargeAndReabstractionCondition;
        }

        if (text.Contains("simultaneous discharge", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.SimultaneousDischargeCondition;
        }

        if (text.Contains("simultaneous abstraction", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.SimultaneousAbstractionCondition;
        }

        if (text.Contains("simultaneous compensatory discharge", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.SimultaneousCompensatoryDischargeCondition;
        }

        if (text.Contains("compensatory discharge", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.CompensatoryDischargeCondition;
        }

        if (text.Contains("read in conjunction", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.ReadInConjunction;
        }

        if (text.Contains("The donor licence was", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.DonorLicence;
        }

        if (text.Contains("used in conjunction", StringComparison.InvariantCultureIgnoreCase)
            || text.Contains("use in conjunction", StringComparison.InvariantCultureIgnoreCase)) // misspelling
        {
            return LinkReasons.UsedInConjunction;
        }

        if (text.Contains("revocation", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.Revocation;
        }            
        
        if (text.Contains("aggregate conditions", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.AggregateConditions;
        }

        if (text.Contains("emergency circumstances", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.EmergencyCircumstances;
        }

        if (text.Contains("Dewatering Discharge", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.DewateringDischargeCondition;
        }

        if (text.Contains("when added to", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.WhenAddedTo;
        }

        if (text.Contains("subsequent abstraction", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.SubsequentAbstraction;
        }

        if (text.Contains("re-abstraction", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.ReAbstraction;
        }

        if (text.Contains("readings", StringComparison.InvariantCultureIgnoreCase)
            && text.Contains("discharged", StringComparison.InvariantCultureIgnoreCase)
            && text.Contains("augmentation", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.ReadingsDischargedAugmentationCondition;
        }

        if (text.Contains("aggregate", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.AggregateCondition;
        }

        if (text.Contains("in an emergency", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.InAnEmergency;
        }
        
        if (text.Contains("shall not exceed", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.ShallNotExceed;
        }

        if (text.Contains("supporting", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.Supporting;
        }

        if (text.Contains("original licence", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.OriginalLicence;
        }

        if (text.Contains("transferred to this", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.TransferredToThis;
        }

        if (text.Contains("coincident", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.Coincident;
        }

        if (text.Contains("shall be supported", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.ShallBeSupported;
        }

        if (text.Contains("residual flow", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.ResidualFlow;
        }
        
        if (text.Contains("authorised by", StringComparison.InvariantCultureIgnoreCase))
        {
            return LinkReasons.AuthorisedBy;
        }

        return null;
    }

    private static (Aggregate[] aggregates, AbstractionLimitGroup[] indiviudal) GetAbstractionLimits(
        List<LabelGroupResult> matches,
        string? licenceNumber,
        string? licenceVersionId,
        PointOfAbstraction[] allPoints,
        PurposeOfAbstraction[] allPurposes,
        NaldData? naldDataLine,
        Dictionary<string, DmsFileData> licenceNumbersMapping,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        int regionCode,
        ref Dictionary<string, object?> noneSchemaData)
    {
        var abstractionLimitsSection = matches
            .FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        var abstractionLimitPoints = abstractionLimitsSection?
            .SubResults
            .Where(res => res.MatchedLabel?.Name == "AbstractionLimitPoint")
            .ToList();

        var abstractionLimitPointSubs = abstractionLimitPoints?
            .SelectMany(res => res.SubResults)
            .Where(res => res.MatchedLabel?.Name == "AbstractionLimitPointSub")
            .ToList();

        if (abstractionLimitPointSubs == null)
        {
            return ([], []);
        }

        var allAggregates = new List<Aggregate>();
        var allIndividualGroups = new List<AbstractionLimitGroup>();

        foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
        {
            var individualGroups = new List<AbstractionLimitGroup>();

            var limitPointTable = abstractionLimitPointSub.SubResults
                .FirstOrDefault(x => x.MatchedLabel?.Name == "LimitPointTable");

            // NE0260034052 has one
            if (noneSchemaData.ContainsKey(TemplateFeatures.LimitPointsTable))
            {
                if (limitPointTable != null)
                {
                    noneSchemaData[TemplateFeatures.LimitPointsTable] = true;
                }
            }
            else
            {
                noneSchemaData.Add(TemplateFeatures.LimitPointsTable, limitPointTable != null);
            }

            var documentIdentifier = abstractionLimitPointSub.SubResults
                .FirstOrDefault(sr => sr.MatchedLabel?.Name == "DocumentIdentifier")?
                .Text?
                .FirstOrDefault()?
                .Text;
            
            if (limitPointTable != null)
            {
                var tableLines = limitPointTable.Text!;

                foreach (var tableLine in tableLines)
                {
                    var words = tableLine.Text.Split(' ');
                    var abstractionPoint = words[0];
                    var hourlyQuantity = words.Length >= 2 && double.TryParse(words[1], out var hourlyQuantityDbl)
                        ? hourlyQuantityDbl : (double?)null;
                    var dailyQuantity = words.Length >= 3 && double.TryParse(words[2], out var dailyQuantityDbl)
                        ? dailyQuantityDbl : (double?)null;
                    var yearlyQuantity = words.Length >= 4 && double.TryParse(words[3], out var yearlyQuantityDbl)
                        ? yearlyQuantityDbl : (double?)null;
                    var instantRate = words.Length >= 5 && double.TryParse(words[4], out var instantRateDbl)
                        ? instantRateDbl : (double?)null;

                    if (hourlyQuantity == null
                        || dailyQuantity == null
                        || yearlyQuantity == null
                        || instantRate == null)
                    {
                        ConsoleHelper.WriteLine($"INFO - {nameof(WalSchemaConverter)} - Table was not in the expected format. Skipping");
                        continue;
                    }
                    
                    var points = new Point[]
                    {
                        new()
                        {
                            Id = abstractionPoint
                        }
                    };
                    
                    var lineAbstractionLimitGroup = new AbstractionLimitGroup
                    {
                        Points = points,
                        Purposes = null,
                        DocumentIdentifier = documentIdentifier,
                        Limits =
                        [
                            new()
                            {
                                Value = hourlyQuantity,
                                PeriodType = LimitPeriodType.PerHour,
                                Units = "cubic metres",
                                Points = points
                            },
                            new()
                            {
                                Value = dailyQuantity,
                                PeriodType = LimitPeriodType.PerDay,
                                Units = "cubic metres",
                                Points = points
                            },
                            new()
                            {
                                Value = yearlyQuantity,
                                PeriodType = LimitPeriodType.PerYear,
                                Units = "cubic metres",
                                Points = points
                            },
                            new()
                            {
                                Value = instantRate,
                                PeriodType = LimitPeriodType.PerSecond,
                                Units = "litres",
                                Points = points
                            }
                        ]
                    };

                    individualGroups.Add(lineAbstractionLimitGroup);
                }

                allIndividualGroups.AddRange(individualGroups);
                continue;
            }

            var siblings = abstractionLimitPointSub.SubResults;
            
            var purposeCondition = siblings
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PurposeCondition");
                    
            var purposeConditionSub = purposeCondition?
                .SubResults
                .Where(x => x.MatchedLabel?.Name == "PurposeConditionSub")
                .ToList();
                    
            var limitPurposes = purposeConditionSub?.Count > 0 ?
                purposeConditionSub.Select(pcs =>
                    new Purpose { Id = pcs.Text!.FirstOrDefault()?.Text }).ToList()
                : null;
                    
            var pointCondition = siblings
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition");

            var pointConditionSub = pointCondition?
                .SubResults
                .Where(x => x.MatchedLabel?.Name == "PointConditionSub")
                .ToList();
                    
            var limitPoints = pointConditionSub?.Count > 0 ?
                pointConditionSub.Select(pcs =>
                    new Point
                    {
                        Id = pcs.Text!.FirstOrDefault()?.Text
                    }).ToList()
                : null;

            var abstractionLimitPointSubText = string.Join(" ", abstractionLimitPointSub.Text?
                .Select(l => l.Text) ?? []);

            var wordedAsAggregateButAllPurposes = abstractionLimitPointSubText.Contains("The aggregate quantity", StringComparison.InvariantCultureIgnoreCase)
                && abstractionLimitPointSubText.Contains("for all purposes", StringComparison.InvariantCultureIgnoreCase)
                || (abstractionLimitPointSubText.Contains("for the purposes of", StringComparison.InvariantCultureIgnoreCase)
                    && allPurposes.Length > 1
                    && limitPurposes?.Count == allPurposes.Length);
            
            var textSuggestsIsAggregate = 
                (abstractionLimitPointSubText.Contains("The aggregate quantity", StringComparison.InvariantCultureIgnoreCase)
                    && !wordedAsAggregateButAllPurposes)
                || abstractionLimitPointSubText.Contains("The quantities detailed below are in aggregate", StringComparison.InvariantCultureIgnoreCase)
                || abstractionLimitPointSubText.Contains("quantity equal to the difference between", StringComparison.InvariantCultureIgnoreCase)
                || abstractionLimitPointSubText.Contains("In aggregate with licence", StringComparison.InvariantCultureIgnoreCase);

            var datePurposesTimePeriods = siblings
                .Where(sibling => sibling.MatchedLabel?.Name == "DatePurposeRough")
                .ToList(); // E.g. Jan, Feb etc..
            
            var timeCutoff = GetTimeCutoff(
                siblings.FirstOrDefault(s => s.MatchedLabel?.Name == "DateOnly"));

            var valueResults = siblings
                .Where(sibling => !string.IsNullOrEmpty(sibling.MatchedLabel?.RelatedName))
                .ToList();

            var anyPointsSpecified = limitPoints?.Count > 1;
            var limitedByPoints = anyPointsSpecified && limitPoints!.Count != allPoints.Length;

            var multiplePurposesSpecified = limitPurposes?.Count > 1;
            var thisLimitedByPurpose = multiplePurposesSpecified && limitPurposes!.Count != allPurposes.Length;
                    
            var othersLimitedByPurpose = allIndividualGroups.Any(g =>
                g.Purposes?.Length > 0
                && g.Purposes.Length != allPurposes.Length);

            var containsUnderThisLicenceText = abstractionLimitPointSubText.Contains("under this licence");
                    
            var meetsAggregateConditions = 
                textSuggestsIsAggregate
                && (limitedByPoints
                    || thisLimitedByPurpose
                    || (multiplePurposesSpecified && othersLimitedByPurpose)
                    || containsUnderThisLicenceText);
            
            var linkedLicenceNumbers = siblings
                .Where(sibling => sibling.MatchedLabel?.Name == "LinkedLicenceNumber")
                .Select(linkedLicenceNumber =>
                {
                    var condition = (Condition?)null; // TODO

                    var scrapedLicenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;

                    var naldLicenceNumber =
                        (string?)linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"] ??
                        null;

                    var naldDataLine2 = GetNaldDataLine(naldData, naldLicenceNumber, regionCode);
                    
                    var (naldStatus, licenceType) = GetLicenceStatusAndType(
                        naldLicenceNumber,
                        naldLicenceStatusData,
                        naldDataLine2,
                        regionCode);

                    FormattingHelper.GetDmsFileData(
                        scrapedLicenceNumber,
                        regionCode,
                        licenceNumbersMapping,
                        out var dmsFileData);

                    return new LinkedLicence
                    {
                        LicenceNumber = scrapedLicenceNumber,
                        RegionId = dmsFileData?.RegionId ?? naldDataLine?.FgacRegionCode ?? regionCode,
                        RawScrapedLicenceNumber = scrapedLicenceNumber,
                        PermitNumber = dmsFileData?.PermitNumber,
                        DmsPath = dmsFileData?.DmsPath,
                        Filename = dmsFileData?.DestinationFileName,
                        NaldStatus = naldStatus,
                        LicenceType = licenceType,
                        Condition = condition,
                        ContainedIn =
                        [
                            new LinkedLicenceSection
                            {
                                Source = LinkedLicenceSource.Document,
                                SectionName = LinkedLicenceSectionNames.AbstractionLimits,
                                IsBecauseOfAggregate = meetsAggregateConditions,
                                LinkReason = GetLinkReason([abstractionLimitPointSub],
                                    linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                                LineNumber = linkedLicenceNumber.LineNumber,
                                PageNumber = linkedLicenceNumber.PageNumber
                            }
                        ]
                    };
                })
                .ToList();

            linkedLicenceNumbers = linkedLicenceNumbers
                .Where(linkedLicence =>
                    FormattingHelper.IsValidLicenceNumber(
                        linkedLicence.LicenceNumber!,
                        regionCode) != false)
                .ToList();
            
            var hasLinkedLicenceNumber = linkedLicenceNumbers.Count > 0;
            var isAggregate = hasLinkedLicenceNumber || meetsAggregateConditions;
            
            if (timeCutoff != null && !isAggregate)
            {
                individualGroups.Add(new AbstractionLimitGroup
                {
                    TimeCutoff = timeCutoff,
                    DocumentIdentifier = documentIdentifier,
                    Limits = [],
                    Points = limitPoints?.ToArray(),
                    Purposes = limitPurposes?.ToArray()
                });
            }
            else if (datePurposesTimePeriods.Count >= 1)
            {
                individualGroups.Add(new AbstractionLimitGroup
                {
                    Limits = [],
                    Points = limitPoints?.ToArray(),
                    Purposes = limitPurposes?.ToArray(),
                    DocumentIdentifier = documentIdentifier
                });

                foreach (var datePurpose in datePurposesTimePeriods)
                {
                    individualGroups.Add(new AbstractionLimitGroup
                    {
                        TimePeriod = GetTimePeriod(datePurpose),
                        DocumentIdentifier = documentIdentifier,
                        Limits = [],
                        Points = limitPoints?.ToArray(),
                        Purposes = limitPurposes?.ToArray()
                    });
                }
            }
            else if (allIndividualGroups.Count == 0 && individualGroups.Count == 0)
            {
                // Add a group
                individualGroups.Add(new AbstractionLimitGroup
                {
                    Limits = [],
                    DocumentIdentifier = documentIdentifier,
                    Points = limitPoints?.ToArray(),
                    Purposes = limitPurposes?.ToArray()
                });
            }
            else if (individualGroups.Count == 0)
            {
                individualGroups.Add(allIndividualGroups[0]);
            }
            
            var relatedNamesDict = new Dictionary<string, int>();
            var aggregateAbstractionLimits = new List<AbstractionLimit>();

            var newValueResults = new List<LabelGroupResult>();

            // Work out the best match when a value found for multiple lines
            foreach (var valueResult in valueResults)
            {
                var allDuplicates = valueResults
                    .Where(vr => vr.Text?.FirstOrDefault()?.Text == valueResult.Text?.FirstOrDefault()?.Text
                        && vr.PageNumber == valueResult.PageNumber
                        && vr.LineNumber == valueResult.LineNumber)
                    .Select(vr => (vr, siblings.FirstOrDefault(sibling =>
                        sibling.MatchedLabel?.Name == vr.MatchedLabel?.RelatedName)))
                .ToList();

                var bestResult = allDuplicates
                    .OrderBy(vrg => vrg.Item2?.LineNumber == vrg.vr.LineNumber ? 0 : 1)
                    .First();

                if (!newValueResults.Contains(bestResult.vr))
                {
                    newValueResults.Add(bestResult.vr);                    
                }
            }

            valueResults = newValueResults;
            
            foreach (var valueResult in valueResults)
            {
                if (!double.TryParse(valueResult.Text?.FirstOrDefault()?.Text, out var number))
                {
                    continue;
                }

                if (!relatedNamesDict.TryAdd(valueResult.MatchedLabel?.RelatedName!, 0))
                {
                    relatedNamesDict[valueResult.MatchedLabel?.RelatedName!] += 1;
                }

                var allUnits = siblings?
                    .Where(sibling =>
                        sibling.MatchedLabel?.Name == valueResult.MatchedLabel?.RelatedName)
                    .ToList();

                var unitPosition = relatedNamesDict[valueResult.MatchedLabel?.RelatedName!];

                var units = allUnits!.Count > unitPosition
                    ? allUnits[unitPosition]
                        .Text?
                        .FirstOrDefault()?
                        .Text
                    : null;

                var text = valueResult.MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text;

                var abstractionLimit = new AbstractionLimit
                {
                    PeriodType = ToLimitPeriodType(text),
                    Value = number,
                    Units = units,
                    Points = limitPoints?.ToArray(),
                    Purposes = limitPurposes?.ToArray()
                };
                
                if (isAggregate)
                {
                    aggregateAbstractionLimits.Add(abstractionLimit);
                    continue;
                }
                
                var pos = GetPositionRelativeToDateLines(datePurposesTimePeriods, valueResult);
                var individualGroup = individualGroups[pos];
                
                var groupPointsStr = individualGroup.Points?.Length > 0
                    ? string.Join(',', individualGroup.Points.Select(p => p.Id))
                    : string.Empty;
                
                var limitPointsStr = abstractionLimit.Points?.Length > 0
                    ? string.Join(',', abstractionLimit.Points.Select(p => p.Id))
                    : string.Empty;
                
                var groupPurposesStr = individualGroup.Purposes?.Length > 0
                    ? string.Join(',', individualGroup.Purposes.Select(p => p.Id))
                    : string.Empty;
                
                var limitPurposesStr = abstractionLimit.Purposes?.Length > 0
                    ? string.Join(',', abstractionLimit.Purposes.Select(p => p.Id))
                    : string.Empty;

                if (individualGroup.Limits.Count > 0
                    && (groupPointsStr != limitPointsStr || groupPurposesStr != limitPurposesStr))
                {
                    individualGroup = individualGroups.FirstOrDefault(ig =>
                    {
                        groupPointsStr = ig.Points?.Length > 0
                            ? string.Join(',', ig.Points.Select(p => p.Id))
                            : string.Empty;
                        
                        groupPurposesStr = ig.Purposes?.Length > 0
                            ? string.Join(',', ig.Purposes.Select(p => p.Id))
                            : string.Empty;

                        return groupPointsStr == limitPointsStr && groupPurposesStr == limitPurposesStr;
                    });

                    if (individualGroup == null)
                    {
                        individualGroup = new AbstractionLimitGroup
                        {
                            Points = abstractionLimit.Points,
                            Purposes = abstractionLimit.Purposes,
                            Limits = [],
                            DocumentIdentifier = documentIdentifier,
                        };

                        individualGroups.Add(individualGroup);
                    }
                }
                
                individualGroup.Limits.Add(abstractionLimit);
            }

            var notIncluded = individualGroups
                .Where(ig => !allIndividualGroups.Contains(ig))
                .ToList();
            
            allIndividualGroups.AddRange(notIncluded);

            if (aggregateAbstractionLimits.Count == 0)
            {
                continue;
            }

            var pointsLoop = aggregateAbstractionLimits.First().Points;
            var purposesLoop = aggregateAbstractionLimits.First().Purposes;
            var timePeriod = GetTimePeriod(
                siblings?.FirstOrDefault(s => s.MatchedLabel?.Name == "DateOnly"));

            var aggregate = new Aggregate
            {
                LicenceNumber = licenceNumber,
                LicenceVersionId = licenceVersionId,
                PrimaryType = linkedLicenceNumbers.Count >= 1
                    ? PrimaryType.LicenceToLicence
                    : PrimaryType.InLicence,
                NaldType = GetNaldType(naldDataLine),
                AggregateSetId = PositionConstants.ReplacementMarker,
                LinkedLicences = linkedLicenceNumbers.Count > 0 ? linkedLicenceNumbers.ToArray() : null,
                Limits = aggregateAbstractionLimits,
                Points = pointsLoop?.ToArray() ?? [],
                Purposes = purposesLoop?.ToArray() ?? [],
                TimeCutoff = timeCutoff,
                TimePeriod = timePeriod,
                DocumentIdentifier = documentIdentifier
            };

            // If there are no points, purposes or licences specified, then it
            // must mean it's relevant to all points and purposes
            if (aggregate.Points.Length == 0
                && aggregate.Purposes.Length == 0
                && linkedLicenceNumbers.Count == 0)
            {
                aggregate.Points = allPoints.Select(Point (p) => p).ToArray();
                aggregate.Purposes = allPurposes.Select(Purpose (p) => p).ToArray();
            }

            if (aggregate.Points.Length > 1)
            {
                aggregate.SubType = SubType.PointToPoint;
            }
            else if (aggregate.Purposes.Length > 1)
            {
                aggregate.SubType = SubType.PurposeToPurpose;
            }

            if (aggregate.Purposes.Length > 0)
            {
                foreach (var aggregateLimit in aggregateAbstractionLimits)
                {
                    aggregateLimit.Purposes = null;
                }
            }
            else
            {
                aggregate.Purposes = null;
            }

            if (aggregate.Points.Length > 0)
            {
                foreach (var aggregateLimit in aggregateAbstractionLimits)
                {
                    aggregateLimit.Points = null;
                }
            }
            else
            {
                aggregate.Points = null;
            }

            allAggregates.Add(aggregate);
        }

        if (allIndividualGroups is [{ Limits.Count: 0 }])
        {
            allIndividualGroups.Clear();
        }

        return (allAggregates.ToArray(), allIndividualGroups.ToArray());
    }

    private static int GetPositionRelativeToDateLines(
        List<LabelGroupResult>? dateLines,
        LabelGroupResult line)
    {
        if (dateLines == null || dateLines.Count == 0)
        {
            return 0;
        }

        if (line.MatchedLabel?.Name == "PerYearValue")
        {
            return 0;
        }

        var match = dateLines
            .OrderBy(matchLineNumber =>
            {
                var diff = matchLineNumber.LineNumber - line.LineNumber;

                if (0 > diff)
                {
                    return int.MaxValue;
                }

                return diff;
            })
            .First();

        return dateLines.IndexOf(match) + 1;
    }

    private static TimePeriod? GetDefinitionOfYear(List<LabelGroupResult> matches)
    {
        var abstractionLimitsSection = matches
            .FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        var abstractionLimitPoints = abstractionLimitsSection?
            .SubResults
            .Where(res => res.MatchedLabel?.Name == "AbstractionLimitPoint")
            .ToList();

        var abstractionLimitPointSubs = abstractionLimitPoints?
            .SelectMany(res => res.SubResults)
            .Where(res => res.MatchedLabel?.Name == "AbstractionLimitPointSub")
            .ToList();

        if (abstractionLimitPointSubs != null)
        {
            foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
            {
                var definition = abstractionLimitPointSub.SubResults
                    .SingleOrDefault(sr => sr.MatchedLabel?.Name == "AYearDefinitionLine");

                if (definition == null)
                {
                    continue;
                }

                var dates = definition.SubResults;
                var text = definition.Text?.FirstOrDefault()?.Text;
                var inclusive = text?.Contains("beginning on") == true
                                || text?.Contains("ending on") == true;

                return new TimePeriod
                {
                    PeriodType = AbstractionPeriodType.SetPeriod,
                    Inclusive = inclusive,
                    StartDate = dates.FirstOrDefault()?.Text?.FirstOrDefault()?.Text,
                    EndDate = dates.LastOrDefault()?.Text?.FirstOrDefault()?.Text
                };
            }
        }

        return null;
    }

    private static MatchesResult ToMatchesResult(LabelGroupResult labelGroupResult)
    {
        return new MatchesResult
        {
            Matches = labelGroupResult.SubResults.ToList()
        };
    }

    private static PeriodOfAbstraction[] GetPeriods(
        List<LabelGroupResult> matches,
        NaldData? naldDataLine,
        ref Dictionary<string, object?> noneSchemaData)
    {
        noneSchemaData.Add("NaldPeriodsData", naldDataLine?.Periods ?? []);
        
        var periodResults = matches.FirstOrDefault(result => result.LabelGroupName == "PeriodsOfAbstraction");
        var returnList = new List<PeriodOfAbstraction>();

        if (periodResults == null)
        {
            return returnList.ToArray();
        }

        if (periodResults.MatchedLabel?.Name == "DuringTheMonthsXToYOnlyText")
        {
            if (periodResults.SubResults.Count != 2)
            {
                return returnList.ToArray();
            }

            returnList.Add(new PeriodOfAbstraction
            {
                PeriodType = AbstractionPeriodType.SetPeriod,
                Description = periodResults.Text?.FirstOrDefault()?.Text,
                Inclusive = true,
                StartDate = periodResults.SubResults[0].Text?.FirstOrDefault()?.Text,
                EndDate = periodResults.SubResults[1].Text?.FirstOrDefault()?.Text,
                NaldPeriodStart = GetNaldPeriodStartDate(naldDataLine,
                    periodResults.Text?.FirstOrDefault()?.Text),
                NaldPeriodEnd = GetNaldPeriodEndDate(naldDataLine,
                    periodResults.Text?.FirstOrDefault()?.Text)
            });
        }

        foreach (var pointResult in periodResults.SubResults)
        {
            var periodPeriodNumber = pointResult.SubResults
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PeriodPeriodNumber");

            var textWithoutNumber = pointResult.SubResults
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PeriodTextWithoutPurposeAndPoint")?
                .Text?
                .Select(t => t.Text)
                .ToList();

            if (textWithoutNumber == null && periodPeriodNumber == null)
            {
                continue;
            }

            var tKey = "Up to and Including ";

            var allTextWithoutNumber = textWithoutNumber?
                .Where(t => !t.StartsWith(tKey, StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            if (allTextWithoutNumber == null)
            {
                continue;
            }

            var upToAndIncludeLine = textWithoutNumber?
                .FirstOrDefault(t => t.StartsWith(tKey, StringComparison.InvariantCultureIgnoreCase));
            TimeCutoff? timeCutoff = null;

            if (upToAndIncludeLine != null)
            {
                var date = upToAndIncludeLine.Replace(tKey, string.Empty);

                timeCutoff = new TimeCutoff
                {
                    CutoffType = CutoffType.Upto,
                    Date = date
                };
            }

            var text = textWithoutNumber != null
                ? string.Join('\n', textWithoutNumber)
                : null;

            var number = periodPeriodNumber?.Text?.FirstOrDefault()?.Text;
            //var id = double.TryParse(number, out var numberResult) ? numberResult : (double?)null;

            var inclusive = text?.Contains("inclusive",
                StringComparison.InvariantCultureIgnoreCase) ?? false;

            var allYear = text == "All year";

            // TODO next bit should be done in config
            var dateParts = text?
                .Replace("From", string.Empty)
                .Replace("inclusive", string.Empty)
                .Split(" to ");

            var startDate = dateParts?[0].Trim();
            var endDate = dateParts?.Length >= 2 ? dateParts[1].Trim() : null;

            returnList.Add(new PeriodOfAbstraction
            {
                Id = number,
                PeriodType = allYear ? AbstractionPeriodType.PerYear : AbstractionPeriodType.SetPeriod,
                Description = text,
                Inclusive = inclusive,
                StartDate = startDate,
                EndDate = endDate,
                TimeCutoff = timeCutoff,
                PointIds = null, // TODO set purpose ids and point ids
                PurposeIds = null, // TODO set purpose ids and point ids
                NaldPeriodStart = GetNaldPeriodStartDate(naldDataLine,
                    periodResults.Text?.FirstOrDefault()?.Text),
                NaldPeriodEnd = GetNaldPeriodEndDate(naldDataLine,
                    periodResults.Text?.FirstOrDefault()?.Text)
            });
        }

        return returnList.ToArray();
    }

    private static MeanOfAbstraction[] GetMeansOfAbstraction(
        List<LabelGroupResult> matches,
        ref Dictionary<string, object?> noneSchemaData)
    {
        var meansResult = DataHelper.GetFirstMatchByLabelGroup(matches, "MeansOfAbstraction");
        var returnList = new List<MeanOfAbstraction>();

        if (meansResult == null)
        {
            return returnList.ToArray();
        }

        noneSchemaData.Add("Confidence:MeansOfAbstraction", meansResult.Confidence);
        
        foreach (var meanResult in meansResult.SubResults)
        {
            var textWithoutNumber = meanResult.SubResults
                .FirstOrDefault(subResult => subResult.MatchedLabel?.Name == "TextWithoutNumber")?
                .Text?
                .Select(t => t.Text);

            var meansPointTable = DataHelper.GetFirstMatchByLabel(meanResult.SubResults, "MeanPointTable");

            // NE0260034052 has one
            if (noneSchemaData.ContainsKey(TemplateFeatures.MeansPointsTable))
            {
                if (meansPointTable != null)
                {
                    noneSchemaData[TemplateFeatures.MeansPointsTable] = true;
                }
            }
            else
            {
                noneSchemaData.Add(TemplateFeatures.MeansPointsTable, meansPointTable != null);
            }

            var meanIdResult = DataHelper.GetFirstMatchByLabel(meanResult.SubResults, "MeanId");

            if (textWithoutNumber == null && meanIdResult == null)
            {
                continue;
            }

            var description = textWithoutNumber != null
                ? string.Join('\n', textWithoutNumber)
                : null;

            var documentSectionNumber = DataHelper.GetFirstLineTextFromMatch(meanIdResult);

            var perSecondUnits = DataHelper.GetTextFromFirstMatchByLabel(
                meanResult.SubResults,
                "PerSecondUnitsMeans");

            var perSecondValueString = DataHelper.GetTextFromFirstMatchByLabel(
                meanResult.SubResults,
                "PerSecondValueMeans");

            var perSecondValue = double.TryParse(perSecondValueString, out var valueResult)
                ? valueResult
                : (double?)null;

            var periodType = LimitPeriodType.Unknown;

            if (description?.Contains("second", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                periodType = LimitPeriodType.PerSecond;
            }

            returnList.Add(new MeanOfAbstraction
            {
                Id = documentSectionNumber,
                Description = description,
                AbstractionLimit = perSecondValue != null
                    ? new AbstractionLimit
                    {
                        PeriodType = periodType,
                        Units = perSecondUnits,
                        Value = perSecondValue
                    }
                    : null
            });
        }

        return returnList.ToArray();
    }

    private static PointOfAbstraction[] GetPoints(
        List<LabelGroupResult> matches,
        NaldData? naldDataLine,
        ref Dictionary<string, object?> noneSchemaData)
    {
        noneSchemaData.Add("NaldPointsData", naldDataLine?.Points ?? []);
        
        var pointsResults = DataHelper.GetFirstMatchByLabelGroup(matches, "Points");
        var returnList = new List<PointOfAbstraction>();

        if (pointsResults == null)
        {
            return returnList.ToArray();
        }
        
        noneSchemaData.Add("Confidence:Points", pointsResults.Confidence);
        var pointPurposeGroupCount = -1;
        
        foreach (var pointPurposeGroup in pointsResults.SubResults)
        {
            noneSchemaData.Add(
                $"Confidence:Points_PointPurposeGroup_{++pointPurposeGroupCount}",
                pointPurposeGroup.Confidence);
            
            var purposeGroupName = DataHelper.GetFirstMatchByLabel(
                pointPurposeGroup.SubResults,
                "PurposeGroupName");

            var purposeIds = purposeGroupName?.SubResults
                .Where(x => x.MatchedLabel?.Name == "PurposeGroupSub")
                .Select(x => x.Text?.FirstOrDefault()?.Text)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToArray();

            var points = DataHelper.GetMatchesByLabel(
                pointPurposeGroup.SubResults,
                "Point");

            var pointCount = 0;
            
            foreach (var point in points)
            {
                var pointNumber = DataHelper.GetTextFromFirstMatchByLabel(
                    point.SubResults,
                    "PointPointNumber");

                if (pointNumber != null)
                {
                    noneSchemaData.Add(
                        $"Confidence:Points_PointPurposeGroup_{pointPurposeGroupCount}_Point_{pointCount++}_PointPointNumber",
                        point.Confidence);
                }

                var pointTextWithoutPurposeAndPoint = point.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PointTextWithoutPurposeAndPoint");
                
                var tLines = pointTextWithoutPurposeAndPoint?
                    .Text?
                    .Select(t => t.Text)
                    .ToList();

                const string tKey = "Up to and Including ";
                var upToAndIncludeLine = tLines?
                    .FirstOrDefault(t => t.StartsWith(tKey, StringComparison.InvariantCultureIgnoreCase));

                TimeCutoff? timeCutoff = null;

                if (upToAndIncludeLine != null)
                {
                    var date = upToAndIncludeLine.Replace(tKey, string.Empty);

                    timeCutoff = new TimeCutoff
                    {
                        CutoffType = CutoffType.Upto,
                        Date = date
                    };
                }

                // NE0260034052 has one
                var pointTable = DataHelper.GetFirstMatchByLabel(point.SubResults, "PointTable");

                if (noneSchemaData.ContainsKey(TemplateFeatures.PointsTable))
                {
                    if (pointTable != null)
                    {
                        noneSchemaData[TemplateFeatures.PointsTable] = true;
                    }
                }
                else
                {
                    noneSchemaData.Add(TemplateFeatures.PointsTable, pointTable != null);
                }

                if (pointTable != null)
                {
                    noneSchemaData.Add($"Confidence:Points_PointPurposeGroup_{pointPurposeGroupCount}_Point_{pointCount}_PointTable", pointTable.Confidence);
                    var tableLines = pointTable.Text!;

                    foreach (var tableLine in tableLines)
                    {
                        var words = tableLine.Text.Split(' ');
                        var subId = words[0]; // e.g. A, D, E

                        returnList.Add(new PointOfAbstraction
                        {
                            Description = tableLine.Text,
                            Id = $"{pointNumber} {subId}", // e.g 2.1 - A
                            PurposeIds = purposeIds,
                            TimeCutoff = timeCutoff,
                            NaldData = GetNaldPointData(naldDataLine,
                                tableLine.Text) // TODO needs to get the correct point
                        });
                        // Format is 'Abstraction National Grid Location Description Map'
                    }

                    continue;
                }

                var allTextWithoutNumber = tLines?
                    .Where(t => !t.StartsWith(tKey, StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();

                if (allTextWithoutNumber == null)
                {
                    continue;
                }

                var description = string.Join(' ', allTextWithoutNumber);

                // If its like 'A SE' or 'B NE' get rid of the A and B
                if (description.Length > 2 && char.IsAsciiLetterUpper(description[0]) && description[1] == ' ')
                {
                    description = description[2..];
                }

                returnList.Add(new PointOfAbstraction
                {
                    Description = description,
                    Id = pointNumber,
                    PurposeIds = purposeIds,
                    TimeCutoff = timeCutoff,
                    NaldData = GetNaldPointData(naldDataLine, description) // TODO needs to get the correct point
                });
            }
        }

        return returnList.ToArray();
    }

    private static NaldData? GetNaldDataLine(
        Dictionary<string, List<NaldData>> naldData,
        string? licenceNumber,
        int regionCode)
    {
        var strippedLicenceNumbers = FormattingHelper.StripForComparisonMultipleOptions(
            licenceNumber,
            regionCode);

        foreach (var strippedLicenceNumber in strippedLicenceNumbers)
        {
            var naldDataKey = $"{regionCode}|{strippedLicenceNumber}";

            if (naldData.Count > 0
                && !string.IsNullOrEmpty(naldDataKey)
                && naldData.TryGetValue(naldDataKey, out var naldDataLine))
            {
                return naldDataLine.First();
            }
        }

        return null;
    }

    private static NaldPointData? GetNaldPointData(NaldData? naldDataLine, string description)
    {
        if (naldDataLine?.Points.Count is null or 0)
        {
            return null;
        }

        var points = naldDataLine.Points;
        NaldDataPoint point;

        if (points.Count == 1)
        {
            point = points[0];
        }
        else
        {
            // TODO - Work out which point matches the description

            point = points
                .First(p => p.PointId != 0);
        }

        return new NaldPointData
        {
            Id = point.PointId.ToString(),
            Name = point.PointName,
            NationalGridReferences = point.NationalGridReferences.Select(n =>
                new NaldNationalGridReference
                {
                    ReferenceIndex = n.ReferenceIndex,
                    Sheet = n.Sheet,
                    East = n.East,
                    North = n.North
                }).ToList(),
            CartesianReferences = point.CartesianReferences.Select(c =>
                new NaldCartesianReference
                {
                    ReferenceIndex = c.ReferenceIndex,
                    East = c.East,
                    North = c.North
                }).ToList(),
            NaldPurposeIds = point.PurposeIds
        };
    }

    private static NaldPurposeData? GetNaldPurposeData(NaldData? naldDataLine, string? description)
    {
        if (naldDataLine?.Purposes.Count is null or 0)
        {
            return null;
        }

        var purposes = naldDataLine.Purposes;
        NaldDataPurpose purpose;

        if (purposes.Count == 1)
        {
            purpose = purposes[0];
        }
        else
        {
            // TODO - Work out which purpose matches the description

            purpose = naldDataLine.Purposes
                .First(p => p.Id != 0);
        }

        return new NaldPurposeData
        {
            Id = purpose.Id.ToString(),
            Code = purpose.CategoryUse.Code,
            UseCode = purpose.CategoryUse.UseCode.ToString(),
            UseDescription = purpose.CategoryUse.UseDescription
        };
    }

    private static string? GetNaldPeriodStartDate(NaldData? naldDataLine, string? description)
    {
        if (naldDataLine == null)
        {
        }

        if (naldDataLine?.Periods.Count is null or 0)
        {
            return null;
        }

        var periods = naldDataLine.Periods;
        var period = periods.Count == 1
            ? periods[0]
            : periods.First(p => p.PeriodStartDay != null);

        return $"{period.PeriodStartDay}/{period.PeriodStartMonth}";
    }

    private static string? GetNaldPeriodEndDate(NaldData? naldDataLine, string? description)
    {
        if (naldDataLine?.Periods.Count is null or 0)
        {
            return null;
        }

        var periods = naldDataLine.Periods;
        var period = periods.Count == 1
            ? periods[0]
            : periods.First(p => p.PeriodEndDay != null);

        return $"{period.PeriodEndDay}/{period.PeriodEndMonth}";
    }

    private static PurposeOfAbstraction[] GetPurposes(
        List<LabelGroupResult> matches,
        NaldData? naldDataLine,
        ref Dictionary<string, object?> noneSchemaData)
    {
        noneSchemaData.Add("NaldPurposesData", naldDataLine?.Purposes ?? []);
        
        var purposeResults = matches.FirstOrDefault(result => result.LabelGroupName == "Purpose");
        var returnList = new List<PurposeOfAbstraction>();

        if (purposeResults == null)
        {
            return returnList.ToArray();
        }

        var pointPurposeGroupCount = -1;

        foreach (var purposePointGroup in purposeResults.SubResults)
        {
            pointPurposeGroupCount += 1;
            var pointCount = 0;
            
            var pointGroupName = purposePointGroup.SubResults
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PointGroupName");

            var pointIds = pointGroupName?.SubResults
                .Where(x => x.MatchedLabel?.Name == "PointGroupSub")
                .Select(x => x.Text?.FirstOrDefault()?.Text)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToArray();

            var purposes = purposePointGroup.SubResults
                .Where(x => x.MatchedLabel!.Name == "Purpose")
                .ToList();

            foreach (var purpose in purposes)
            {
                pointCount += 1;
                
                var purposeNumber = purpose.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PurposeNumber");

                var pointTextWithoutPurposeAndPoint = purpose.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "TextWithoutPoints");
                
                var tLines = pointTextWithoutPurposeAndPoint?
                    .Text?
                    .Select(t => t.Text)
                    .ToArray();

                if (pointTextWithoutPurposeAndPoint != null)
                {
                    noneSchemaData.Add(
                        $"Confidence:Points_PointPurposeGroup_{pointPurposeGroupCount}_Point_{pointCount}_PointTextWithoutPurposeAndPoint",
                        pointTextWithoutPurposeAndPoint.Confidence);
                }
                
                var tKey = "Up to and Including ";

                var allTextWithoutNumber = tLines?
                    .Where(t => !t.StartsWith(tKey, StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();

                if (allTextWithoutNumber == null && purposeNumber == null)
                {
                    continue;
                }

                var upToAndIncludeLine = tLines?
                    .FirstOrDefault(t => t.StartsWith(tKey, StringComparison.InvariantCultureIgnoreCase));
                TimeCutoff? timeCutoff = null;

                if (upToAndIncludeLine != null)
                {
                    var date = upToAndIncludeLine.Replace(tKey, string.Empty);

                    timeCutoff = new TimeCutoff
                    {
                        CutoffType = CutoffType.Upto,
                        Date = date
                    };
                }

                var description = allTextWithoutNumber != null
                    ? string.Join('\n', allTextWithoutNumber)
                    : null;

                var number = purposeNumber?.Text?.FirstOrDefault()?.Text;

                if (purposes.Count == 1)
                {
                    // TODO more of this should be done in the parser
                    if (description?.Contains("i) ") == true && description.Contains("ii)"))
                    {
                        var points = RomanNumeralsSplit(description);

                        foreach (var point in points)
                        {
                            returnList.Add(new PurposeOfAbstraction
                            {
                                Id = number,
                                Description = point.Trim(),
                                PointIds = pointIds,
                                TimeCutoff = timeCutoff,
                                NaldData = GetNaldPurposeData(naldDataLine, point.Trim())
                            });
                        }

                        continue;
                    }

                    // TODO more of this should be done in the parser
                    if (description?.Contains("(1) ") == true && description.Contains("(2)"))
                    {
                        var points = BracketNumbersSplit(description);

                        foreach (var point in points)
                        {
                            returnList.Add(new PurposeOfAbstraction
                            {
                                Id = number,
                                Description = point.Trim(),
                                PointIds = pointIds,
                                TimeCutoff = timeCutoff,
                                NaldData = GetNaldPurposeData(naldDataLine, point.Trim())
                            });
                        }

                        continue;
                    }

                    // TODO more of this should be done in the parser
                    if (description?.Contains("1. ") == true && description.Contains("2. "))
                    {
                        var points = NumberDotSplit(description);

                        foreach (var point in points)
                        {
                            returnList.Add(new PurposeOfAbstraction
                            {
                                Id = number,
                                Description = point.Trim(),
                                PointIds = pointIds,
                                TimeCutoff = timeCutoff,
                                NaldData = GetNaldPurposeData(naldDataLine, point.Trim())
                            });
                        }

                        continue;
                    }

                    // TODO more of this should be done in the parser
                    if (description?.Contains("(a) ") == true && description.Contains("(b) "))
                    {
                        var points = LetterBracketSplit(description);

                        foreach (var point in points)
                        {
                            returnList.Add(new PurposeOfAbstraction
                            {
                                Id = number,
                                Description = point.Trim(),
                                PointIds = pointIds,
                                TimeCutoff = timeCutoff,
                                NaldData = GetNaldPurposeData(naldDataLine, point.Trim())
                            });
                        }

                        continue;
                    }

                    // TODO more of this should be done in the parser
                    if (description?.Contains("4.2 ") == true && description.Contains("4.3 "))
                    {
                        var points = FourPointSplit(description);

                        foreach (var point in points)
                        {
                            returnList.Add(new PurposeOfAbstraction
                            {
                                Id = number,
                                Description = point.Trim(),
                                PointIds = pointIds,
                                TimeCutoff = timeCutoff,
                                NaldData = GetNaldPurposeData(naldDataLine, point.Trim())
                            });
                        }

                        continue;
                    }
                }

                returnList.Add(new PurposeOfAbstraction
                {
                    Id = number,
                    Description = description,
                    PointIds = pointIds,
                    TimeCutoff = timeCutoff,
                    NaldData = GetNaldPurposeData(naldDataLine, description)
                });
            }
        }

        return returnList.ToArray();
    }

    private static string[] FourPointSplit(string text)
    {
        var x = text
            .Replace("4.1 ", "$")
            .Replace("4.2 ", "$")
            .Replace("4.3 ", "$")
            .Replace("4.4 ", "$")
            .Replace("4.5 ", "$")
            .Replace("\n", " ")
            .Trim();

        if (x.StartsWith("$"))
        {
            x = x[1..];
        }

        return x.Split('$');
    }

    private static string[] LetterBracketSplit(string text)
    {
        var x = text
            .Replace("(a) ", "$")
            .Replace("(b) ", "$")
            .Replace("(c) ", "$")
            .Replace("(d) ", "$")
            .Replace("(e) ", "$")
            .Replace("\n", " ")
            .Trim();

        if (x.StartsWith("$"))
        {
            x = x[1..];
        }

        return x.Split('$');
    }

    private static string[] NumberDotSplit(string text)
    {
        var x = text
            .Replace("1. ", "$")
            .Replace("2. ", "$")
            .Replace("3. ", "$")
            .Replace("4. ", "$")
            .Replace("5. ", "$")
            .Replace("\n", " ")
            .Trim();

        if (x.StartsWith("$"))
        {
            x = x[1..];
        }

        return x.Split('$');
    }

    private static string[] BracketNumbersSplit(string text)
    {
        var x = text
            .Replace("(1) ", "$")
            .Replace("(2) ", "$")
            .Replace("(3) ", "$")
            .Replace("(4) ", "$")
            .Replace("(5) ", "$")
            .Replace("\n", " ")
            .Trim();

        if (x.StartsWith("$"))
        {
            x = x[1..];
        }

        return x.Split('$');
    }

    private static string[] RomanNumeralsSplit(string text)
    {
        var x = text
            .Replace("iii) ", "$")
            .Replace("ii) ", "$")
            .Replace("iv) ", "$")
            .Replace("i) ", "$")
            .Replace("v) ", "$")
            .Replace("\n", " ")
            .Trim();

        if (x.StartsWith("$"))
        {
            x = x[1..];
        }

        return x.Split('$');
    }

    private static LimitPeriodType ToLimitPeriodType(string? text)
    {
        return text?.ToLower() switch
        {
            "per second" => LimitPeriodType.PerSecond,
            "per minute" => LimitPeriodType.PerMinute,
            "per hour" => LimitPeriodType.PerHour,
            "per day" => LimitPeriodType.PerDay,
            "per week" => LimitPeriodType.PerWeek,
            "per month" => LimitPeriodType.PerMonth,
            "per annum" => LimitPeriodType.PerYear,
            "per year" => LimitPeriodType.PerYear,
            "in total" => LimitPeriodType.InTotal,
            "total annual quantity" => LimitPeriodType.InTotal,
            "consecutive five year" => LimitPeriodType.Per5Years,
            "five consecutive years" => LimitPeriodType.Per5Years,
            _ => throw new NotSupportedException($"Unknown limit period type '{text}'")
        };
    }

    private static string? GetNaldType(NaldData? naldDataLine)
    {
        var naldAggregateCondition = naldDataLine?.AggregateConditions;

        return naldAggregateCondition?.Count > 0 && !string.IsNullOrEmpty(naldAggregateCondition[0].Condition)
            ? naldAggregateCondition[0].Condition
            : null;
    }

    public static List<LicenceSet> AddAdditionalLicenceSets(
        List<IReadOnlyList<LicenceSet>> licenceSetGroups,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> licenceNumbersMapping)
    {
        var distinctLicenceSets = AsDistinctLicenceSets(licenceSetGroups);

        distinctLicenceSets.AddRange(AddIncomingLinks(
            licenceSetGroups,
            true,
            naldLicenceStatusData,
            naldData,
            licenceNumbersMapping));

        AddImplicitExplicitAndEncompassingLicenceSets(licenceSetGroups, distinctLicenceSets);
        return distinctLicenceSets;
    }

    private static void AddImplicitExplicitAndEncompassingLicenceSets(
        List<IReadOnlyList<LicenceSet>> initialLicenceSetGroups,
        List<LicenceSet> distinctLicenceSets)
    {
        foreach (var licenceSetGroup in initialLicenceSetGroups)
        {
            if (licenceSetGroup.Count == 0 || licenceSetGroup.First().Licences.Length == 0)
            {
                continue;
            }

            var licence = licenceSetGroup.First().Licences.First();

            if (licence.LicenceNumber == null)
            {
                ConsoleHelper.WriteLine(
                    $"WARNING - {nameof(WalSchemaConverter)} - AddImplicitExplicitAndEncompassingLicenceSets - Licence doesnt have licence number set");
                
                continue;
            }
            
            var licenceSetsForLicence = GetAllLicenceSetsForLicence(
                licence.LicenceNumber!.Value!,
                distinctLicenceSets);

            var updatedLicenceSetIds = AddImplicitAndExplicitLicenceSets(licence, licenceSetsForLicence);
            updatedLicenceSetIds = AddEncompassingLicenceSets(licence, distinctLicenceSets, updatedLicenceSetIds);

            licence.LicenceSets = updatedLicenceSetIds.ToArray();
        }
    }

    private static List<LicenceSet> GetAllLicenceSetsForLicence(string licenceNumber,
        IReadOnlyList<LicenceSet> licenceSets)
    {
        var returnList = new List<LicenceSet>();

        foreach (var licenceSet in licenceSets)
        {
            if (licenceSet.Licences.All(l => l.LicenceNumber?.Value != licenceNumber))
            {
                continue;
            }

            returnList.Add(licenceSet);
        }

        return returnList;
    }

    private static List<LicenceSet> AsDistinctLicenceSets(List<IReadOnlyList<LicenceSet>> licenceSetGroups)
    {
        var returnList = new List<LicenceSet>();

        foreach (var licenceSetGroup in licenceSetGroups)
        {
            foreach (var licenceSet in licenceSetGroup)
            {
                if (returnList.Any(dls => dls.LicenceSetId == licenceSet.LicenceSetId))
                {
                    continue;
                }

                returnList.Add(licenceSet);
            }
        }

        return returnList;
    }

    private static (string? LicenceNumber,
        string? ScrapedLicenceNumber,
        double? Confidence,
        double? OcrConfidence) GetLicenceNumber(
        MatchesResult matchesResult,
        Dictionary<string, object?>? noneSchemaData = null)
    {
        string? licenceNumber = null;
        var (scrapedLicenceNumber, confidence, ocrConfidence) =
            GetScrapedLicenceNumber(matchesResult);

        if (!string.IsNullOrEmpty(scrapedLicenceNumber))
        {
            noneSchemaData?.TryAdd("scrapedLicenceNumber", scrapedLicenceNumber);
            licenceNumber = FormattingHelper.FormatLicenceNumber(scrapedLicenceNumber, matchesResult.RegionCode);
        }

        string? fileNameLicenceNumber = null;

        if (!string.IsNullOrEmpty(matchesResult.Filename))
        {
            var filenameParts = matchesResult.Filename!.Replace(" ", "_").Split('_');
            var licenceNumberPart = filenameParts[0];
            var isPartALicenceNumber = licenceNumberPart.Length > 5
                && !licenceNumberPart.Contains('.')
                && licenceNumberPart.Count(char.IsDigit) >= 3;

            // Leave the below, we can't trust the bit in the filename for old files

            /*if (!isPartALicenceNumber)
            {
                licenceNumberPart = filenameParts[^1].Split('.')[0];

                isPartALicenceNumber = licenceNumberPart.Length > 5
                    && !licenceNumberPart.Contains('.')
                    && licenceNumberPart.Count(char.IsDigit) >= 3;
            }*/

            if (isPartALicenceNumber)
            {
                fileNameLicenceNumber = licenceNumberPart.Replace("-", "/");

                fileNameLicenceNumber = fileNameLicenceNumber.Contains('/')
                    ? FormattingHelper.FormatLicenceNumber(fileNameLicenceNumber, matchesResult.RegionCode)
                    : FormattingHelper.NoneSeperatedToNaldLicenceNumber(fileNameLicenceNumber,
                        matchesResult.RegionCode);

                if (!string.IsNullOrEmpty(fileNameLicenceNumber))
                {
                    noneSchemaData?.TryAdd("filenameLicenceNumber", fileNameLicenceNumber);

                    if (string.IsNullOrEmpty(scrapedLicenceNumber))
                    {
                        licenceNumber = fileNameLicenceNumber;
                    }
                }
            }
        }

        // If they are similar, use the filename version of the licence number
        if (!string.IsNullOrEmpty(scrapedLicenceNumber)
            && !string.IsNullOrEmpty(fileNameLicenceNumber)
            && FormattingHelper.FormatLicenceNumber(scrapedLicenceNumber, matchesResult.RegionCode) !=
            fileNameLicenceNumber)
        {
            var formattedScraped = FormattingHelper.FormatLicenceNumber(scrapedLicenceNumber, matchesResult.RegionCode);
            var characterDifferenceCount = DifferenceCount(fileNameLicenceNumber, formattedScraped);

            if (characterDifferenceCount <= 2)
            {
                licenceNumber = fileNameLicenceNumber;
            }
        }

        licenceNumber = FormattingHelper.FormatLicenceNumber(licenceNumber, matchesResult.RegionCode)?.ToUpper();
        
        return (licenceNumber, scrapedLicenceNumber, confidence, ocrConfidence);
    }

    private static (string? ScrapedLicenceNumber, double? Confidence, double? OcrConfidence)
        GetScrapedLicenceNumber(MatchesResult matches)
    {
        var text = DataHelper.GetTextFromFirstMatchByLabelGroup(
            matches.Matches!,
            "LicenceNumber",
            out var licenceNumberMatch);
        
        return (
            text,
            licenceNumberMatch?.Confidence,
            licenceNumberMatch?.Text?.FirstOrDefault()?.OcrConfidence);
    }

    private static List<LicenceSetReference> AddEncompassingLicenceSets(
        Licence licence1,
        List<LicenceSet> distinctLicenceSets,
        List<LicenceSetReference> newLicenceSetIds)
    {
        var returnList = new List<LicenceSetReference>(newLicenceSetIds);

        foreach (var distinctLicenceSet in distinctLicenceSets)
        {
            var setContainsLicence = distinctLicenceSet.Licences.Any(l => l.LicenceNumber == licence1.LicenceNumber);

            if (setContainsLicence)
            {
                var licenceContainsSet = returnList.Any(ls => ls.LicenceSetId == distinctLicenceSet.LicenceSetId);

                if (!licenceContainsSet)
                {
                    var fullyEncompassedIn = licence1.LinkedLicences
                        .All(ll => distinctLicenceSet.Licences.Any(l => ll.LicenceNumber == l.LicenceNumber?.Value));

                    var type = fullyEncompassedIn
                        ? LicenceSetType.FullyEncompassedIn
                        : LicenceSetType.PartiallyEncompassedIn;

                    var toAdd = new LicenceSetReference
                    {
                        LicenceSetId = distinctLicenceSet.LicenceSetId,
                        LicenceSetType = type
                    };

                    returnList.Add(toAdd);

                    if (!distinctLicenceSet.LicenceSetTypes.Contains(type))
                    {
                        var dls = new List<LicenceSetType>(distinctLicenceSet.LicenceSetTypes) { type };
                        distinctLicenceSet.LicenceSetTypes = dls.ToArray();
                    }
                }

                var licencesLicenceSet =
                    returnList.First(x =>
                        x.LicenceSetId ==
                        distinctLicenceSet
                            .LicenceSetId); // TODO should be single, but that errors for some reaosn in some circumstances

                var licencesLicenceSetType = licencesLicenceSet.LicenceSetType;
                var licenceSetContainsType = distinctLicenceSet.LicenceSetTypes.Contains(licencesLicenceSetType);

                if (!licenceSetContainsType)
                {
                    var ndlst = new List<LicenceSetType>(distinctLicenceSet.LicenceSetTypes) { licencesLicenceSetType };
                    distinctLicenceSet.LicenceSetTypes = ndlst.ToArray();
                }
            }
        }

        return returnList;
    }

    private static List<LicenceSetReference> AddImplicitAndExplicitLicenceSets(
        Licence licence1,
        List<LicenceSet> allLicenceSetsForLicence)
    {
        var returnList = new List<LicenceSetReference>(licence1.LicenceSets);

        foreach (var licenceSetForLicence in allLicenceSetsForLicence)
        {
            if (returnList.Any(lsi => lsi.LicenceSetId == licenceSetForLicence.LicenceSetId))
            {
                continue;
            }

            var allLinkedLicenceOfLicence = licenceSetForLicence.Licences
                .All(l => licence1.LicenceNumber?.Value == l.LicenceNumber?.Value
                    || licence1.LinkedLicences.Select(ll => ll.LicenceNumber).Contains(l.LicenceNumber?.Value));

            if (!allLinkedLicenceOfLicence)
            {
                continue;
            }

            var allLinkedLicenceOfLicenceExplicit = licenceSetForLicence.Licences
                .All(l => licence1.LicenceNumber?.Value == l.LicenceNumber?.Value
                  || licence1.LinkedLicences.Where(ll => ll.ContainedIn?.Any(ci =>
                          ci.Direction == LinkedLicenceDirection.Incoming) != true)
                      .Select(ll => ll.LicenceNumber).Contains(l.LicenceNumber?.Value));

            var type = licenceSetForLicence.LicenceSetTypes[0];

            if (!allLinkedLicenceOfLicenceExplicit)
            {
                if (type == LicenceSetType.AllLicencesExplicitlyReferencedInLimits)
                {
                    type = LicenceSetType.AllLicencesImplicitlyReferencedInLimits;

                    if (!licenceSetForLicence.LicenceSetTypes.Contains(type))
                    {
                        var newLTypes = new List<LicenceSetType>(licenceSetForLicence.LicenceSetTypes) { type };
                        licenceSetForLicence.LicenceSetTypes = newLTypes.ToArray();
                    }
                }
                else if (type == LicenceSetType.AllLicencesExplicitlyReferencedAnywhere)
                {
                    type = LicenceSetType.AllLicencesIncludingImplicitlyReferenced;

                    if (!licenceSetForLicence.LicenceSetTypes.Contains(type))
                    {
                        var newLTypes = new List<LicenceSetType>(licenceSetForLicence.LicenceSetTypes) { type };
                        licenceSetForLicence.LicenceSetTypes = newLTypes.ToArray();
                    }
                }
            }

            returnList.Add(new()
            {
                LicenceSetId = licenceSetForLicence.LicenceSetId,
                LicenceSetType = type
            });
        }

        return returnList;
    }

    private static int DifferenceCount(string? str1, string? str2)
    {
        if (str1 == null)
        {
            return -1;
        }

        if (str2 == null)
        {
            return -1;
        }

        var set1 = str1.Split(' ').Distinct().ToList();
        var set2 = str2.Split(' ').Distinct().ToList();

        var diff = set2.Count > set1.Count
            ? set2.Except(set1).ToList()
            : set1.Except(set2).ToList();

        return diff.Count;
    }

    [GeneratedRegex("(13|14|15|16|17|18|19|20)\\d\\d")]
    private static partial Regex FourDigitYearRegex();
}