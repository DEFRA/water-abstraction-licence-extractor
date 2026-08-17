using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Constants;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using CartesianReference = WRADI.Core.AbstractionLicence.Models.CartesianReference;
using Date = WALE.ProcessFile.Services.Formats.Date;
using LicenceType = WRADI.Core.AbstractionLicence.Enums.LicenceType;
using NationalGridReference = WRADI.Core.AbstractionLicence.Models.NationalGridReference;

namespace WRADI.DocumentType.AbstractionLicence.Converters;

public static class AbstractionLicenceSchemaConverter
{
    private static async Task<Licence> ToLicenceAsync(
        MatchesResult matchesResult,
        DmsFileData? dmsFileData,
        string? naldLicenceNumber,
        NaldLinkedLicenceHelper? naldLinkedLicenceHelper,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService,
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
            ConsoleHelper.WriteLine($"WARNING - {nameof(AbstractionLicenceSchemaConverter)} - No match object exists to " +
                $"convert, {dmsFileData?.FileId} {naldLicenceNumber}");
            
            return new Licence
            {
                Filename = matchesResult.Filename,
                ProcessRunId = processRunId,
                DmsFileId = dmsFileData!.FileId,
                Status = ScrapeStatus.Error,
                LicenceNumber = new ValueWithConfidence<string>
                {
                    Value = naldLicenceNumber,
                    Confidence = null,
                    OcrConfidence = null
                }
            };
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

        var (licenceNumber, scrapedLicenceNumber, confidence, ocrConfidence, source) =
            GetLicenceNumber(matchesResult, naldLicenceNumber, noneSchemaData);

        var licenceNumberWithConfidence = !string.IsNullOrEmpty(licenceNumber)
            ? new ValueWithConfidence<string>(
                licenceNumber,
                ocrConfidence,
                confidence)
            : null;

        var naldDataLine = await Core.AbstractionLicence.Helpers.FormattingHelper.GetNaldDataLineAsync(
            cacheService,
            licenceNumber,
            regionCode);
        
        var licenceVersion = GetLicenceVersion(matches, naldDataLine, noneSchemaData, dmsFileIdInfo);

        var means = GetMeansOfAbstraction(
            matches,
            ref noneSchemaData);

        var sourceOfSupply = GetPoints(
            "SourceOfSupply",
            matches,
            naldDataLine,
            ref noneSchemaData);
        
        var points = GetPoints(
            "Points",
            matches,
            naldDataLine,
            ref noneSchemaData);

        if (points.Length == 0 && sourceOfSupply.Length != 0)
        {
            // Use source of supply as points (some older documents do this)
            points = sourceOfSupply;
        }
        
        var purposes = GetPurposes(
            matches,
            naldDataLine,
            ref noneSchemaData);

        var periods = GetPeriods(
            matches,
            naldDataLine,
            ref noneSchemaData);

        var (aggregates, individual, aggregateLinkedLicences) =
            await GetAbstractionLimitsAsync(
                matches,
                licenceNumber,
                licenceVersion.LicenceVersionId,
                points,
                purposes,
                naldDataLine,
                matchesResult.RegionCode,
                noneSchemaData,
                lookupConfiguration!,
                cacheService);

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

            var issuedToMatchedLabelText = companyNameMatch.MatchedLabelTextFirstLine ?? string.Empty;
            noneSchemaData.Add("issuedToMatchedLabelText", issuedToMatchedLabelText);

            var issuedToMatchLabelPosition = companyNameMatch.MatchedLabelPosition.ToString() ?? "--";
            noneSchemaData.Add("issuedToMatchLabelPosition", issuedToMatchLabelPosition);

            var issuedToCertainty = (int)companyNameMatchType / 100;
            noneSchemaData.Add("issuedToCertainty", issuedToCertainty);
        }

        var ocr = matchesResult.ScannedFile ? "OCR" : "NoOCR";
        noneSchemaData.Add("ocr", ocr);

        noneSchemaData.Add("servicesUsed", matchesResult.ServicesUsed.ToArray());
        
        var (naldStatus, licenceType) = GetLicenceStatusAndType(naldDataLine);

        var sectionDataDict = new Dictionary<
            string,
            (
                List<LinkedLicence> LinkedLicences,
                List<AbstractionLimitGroup> AbstractionLimits,
                List<Aggregate> Aggregates
            )>();
        
        var sectionsToLookAt = new List<string>
        {
            DocumentSectionNames.OtherConditions,
            DocumentSectionNames.Records,
            DocumentSectionNames.FurtherConditions,
            DocumentSectionNames.FurtherProvisions,
            DocumentSectionNames.Additional,
            DocumentSectionNames.ReasonsForConditions
        };

        foreach (var sectionToLookAt in sectionsToLookAt)
        {
            var sectionData = await GetSectionDataAsync(
                sectionToLookAt,
                matches,
                matchesResult.RegionCode,
                licenceNumber,
                licenceVersion.LicenceVersionId,
                points,
                purposes,
                naldDataLine,
                noneSchemaData,
                lookupConfiguration,
                cacheService,
                individual);
            
            sectionDataDict.Add(sectionToLookAt, sectionData);
        }

        var linkedLicences = new List<LinkedLicence>();
        
        if (naldLinkedLicenceHelper != null)
        {
            var naldLinkedLicences =
                naldLinkedLicenceHelper.GetLinkedLicences(licenceNumber, true);

            foreach (var naldLinkedLicence in naldLinkedLicences)
            {
                var thisDmsFileData = await FormattingHelper.GetDmsFileDataAsync(
                    naldLinkedLicence.NaldLicence.LicenceNumber,
                    lookupConfiguration.CacheService);

                var outputLicenceType = LicenceType.Unknown;

                if (naldLinkedLicence.NaldLicence.Type == LicenceType.Impoundment)
                {
                    outputLicenceType = LicenceType.Impoundment;
                }
                else if (naldLinkedLicence.NaldLicence.Type == LicenceType.SurfaceWaterAbstraction)
                {
                    outputLicenceType = LicenceType.SurfaceWaterAbstraction;
                }
                else if (naldLinkedLicence.NaldLicence.Type == LicenceType.GroundWaterAbstraction)
                {
                    outputLicenceType = LicenceType.GroundWaterAbstraction;
                }
                else if (naldLinkedLicence.NaldLicence.Type == LicenceType.Abstraction)
                {
                    outputLicenceType = LicenceType.Abstraction;
                }

                var sourceFields = naldLinkedLicence.SourceFields?
                    .Where(sf => sf.Value != null)
                    .ToDictionary();
                
                linkedLicences.Add(new LinkedLicence
                {
                    LicenceNumber = naldLinkedLicence.NaldLicence.LicenceNumber,
                    RegionId = naldLinkedLicence.NaldLicence.RegionCode,
                    DmsPermitNumber = thisDmsFileData?.PermitNumber,
                    DmsPath = thisDmsFileData?.DmsPath,
                    LicenceType = outputLicenceType,
                    ContainedIn =
                    [
                        new ContainedInInformation
                        {
                            Source = InformationSource.Nald,
                            Direction = naldLinkedLicence.LinkType == NaldLinkedLicenceType.Incoming
                                ? InformationDirection.Incoming
                                : InformationDirection.Outgoing,
                            LinkReason = GetLinkReason(
                                naldLinkedLicence.SourceFields![naldLinkedLicence.FromField],
                                naldLinkedLicence.LinkType == NaldLinkedLicenceType.Incoming
                                    ? licenceNumber
                                    : naldLinkedLicence.NaldLicence.LicenceNumber),
                            SectionName = naldLinkedLicence.FromField,
                            AcinCode = naldLinkedLicence.AcinCode,
                            SourceFields = sourceFields
                        }
                    ]
                });
            }
        }

        linkedLicences.AddRange(aggregateLinkedLicences);
        
        linkedLicences.AddRange(await GetPurposesLinkedLicencesAsync(
            matches,
            matchesResult.RegionCode,
            noneSchemaData,
            lookupConfiguration!,
            cacheService));
        
        linkedLicences.AddRange(await GetPointsLinkedLicencesAsync(
            matches,
            matchesResult.RegionCode,
            noneSchemaData,
            lookupConfiguration!,
            cacheService));
        
        foreach (var (_, (list, _, _)) in sectionDataDict)
        {
            linkedLicences.AddRange(list);   
        }

        var licenceHistorySection = matches
            .FirstOrDefault(result => result.LabelGroupName == "LicenceHistory");
        
        var licenceHistoryLinkedLicences =
            await GetLicenceHistoryLinkedLicencesAsync(
                licenceHistorySection,
                matchesResult.RegionCode,
                noneSchemaData,
                lookupConfiguration,
                cacheService);

        // NOTE - We don't want to include licence history licences in our output, we just want to check against them

        linkedLicences = await ConsolidateLinkedLicencesAsync(
            linkedLicences,
            licenceNumber!,
            lookupConfiguration,
            cacheService);

        var anywhereInDocumentLinkedLicences = await GetAnywhereInDocumentLinkedLicencesAsync(
            matches,
            matchesResult.RegionCode,
            noneSchemaData,
            lookupConfiguration,
            cacheService);

        var additionalLinkedLicenceCount = 1;
        
        var licenceHistoryStartPageAndLineCalc = (licenceHistorySection?.LabelStartPageNumber * 100)
            + licenceHistorySection?.LabelStartLineNumber;
        var licenceHistoryEndPageAndLineCalc = (licenceHistorySection?.Text?.LastOrDefault()?.PageNumber * 100)
            + licenceHistorySection?.Text?.LastOrDefault()?.LineNumber;
        
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

            if (!found && licenceHistoryLinkedLicences.Count > 0)
            {
                // TODO this needs updating so it only excludes them if there from the licnce history line number

                found = licenceHistoryLinkedLicences
                    .Any(lhLinkedLicence =>
                    {
                        var paddedLinkedLicenceNumber =
                            FormattingHelper.FormatLicenceNumber(lhLinkedLicence.LicenceNumber, regionCode);
                        
                        var onlyInLicenceHistory = anywhereInDocumentLinkedLicence.ContainedIn?
                            .All(aci =>
                                {
                                    var aciPageAndLineCalc = (aci.PageNumber * 100) + aci.LineNumber;
                                    
                                    return licenceHistoryStartPageAndLineCalc <= aciPageAndLineCalc
                                           && licenceHistoryEndPageAndLineCalc >= aciPageAndLineCalc;
                                }
                                ) == true;

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

        var combinedAggregates = new List<Aggregate>(aggregates);
        
        foreach (var (_, (_, _, list)) in sectionDataDict)
        {
            combinedAggregates.AddRange(list);
        }
        
        aggregates = combinedAggregates.ToArray();
        
        var combinedIndividual = new List<AbstractionLimitGroup>(individual);
        
        foreach (var (_, (_, list, _)) in sectionDataDict)
        {
            combinedIndividual.AddRange(list);
        }
        
        individual = combinedIndividual.ToArray();

        (individual, aggregates) = PromoteAnyIndividualLimitsThatShouldBeAggregates(
            individual,
            aggregates,
            points,
            purposes,
            licenceNumber,
            licenceVersion.LicenceVersionId,
            naldDataLine);

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
        
        var naldHasAggCondition = naldDataLine?.HasAggCondition ?? false;
        
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
            NaldHasAggregateCondition = naldHasAggCondition,
            LicenceType = licenceType,
            RegionId = naldDataLine?.FgacRegionCode ?? regionCode
        };
    }

    private static (AbstractionLimitGroup[] individuals, Aggregate[] aggregates)
        PromoteAnyIndividualLimitsThatShouldBeAggregates(
            AbstractionLimitGroup[] individuals,
            Aggregate[] aggregates,
            PointOfAbstraction[] points,
            PurposeOfAbstraction[] purposes,
            string? licenceNumber,
            string? licenceVersionId,
            NaldData? naldDataLine)
    {
        // TODO should eventually do this for periods
        
        var multiplePointsInDocument = points.Length > 1;
        var multiplePurposesInDocument = purposes.Length > 1;

        if (!multiplePointsInDocument && !multiplePurposesInDocument)
        {
            return (individuals, aggregates);
        }
        
        var countsOfPoints = aggregates
            .Select(a => a.Points?.Length)
            .Where(c => c != null)
            .ToList();
        
        countsOfPoints.AddRange(individuals
            .Select(a => a.Points?.Length)
            .Where(c => c != null)
            .ToList());

        countsOfPoints = countsOfPoints.Distinct().ToList();
        
        var countsOfPurposes = aggregates
            .Select(a => a.Purposes?.Length)
            .Where(c => c != null)
            .ToList();
        
        countsOfPurposes.AddRange(individuals
            .Select(a => a.Purposes?.Length)
            .Where(c => c != null)
            .ToList());

        countsOfPurposes = countsOfPurposes.Distinct().ToList();
        
        var mixedPointsCounts = countsOfPoints.Count > 1;
        var mixedPurposesCounts = countsOfPurposes.Count > 1;
        
        if (!mixedPointsCounts && !mixedPurposesCounts)
        {
            return (individuals, aggregates);
        }

        var lowestPoints = aggregates.Min(a => a.Points?.Length) ?? int.MaxValue;
        var lowestPointsInd = individuals.Min(a => a.Points?.Length) ?? int.MaxValue;

        if (lowestPoints > lowestPointsInd)
        {
            lowestPoints = lowestPointsInd;
        }
        
        var lowestPurposes = aggregates.Min(a => a.Purposes?.Length) ?? int.MaxValue;
        var lowestPurposesInd = individuals.Min(a => a.Purposes?.Length) ?? int.MaxValue;

        if (lowestPurposes > lowestPurposesInd)
        {
            lowestPurposes = lowestPurposesInd;
        }
        
        var newIndividuals = new List<AbstractionLimitGroup>();
        var newAggregates = new List<Aggregate>();
        
        foreach (var individual in individuals)
        {
            if (mixedPointsCounts)
            {
                var individualExplicitPointsCount = individual.Points?.Count(p => p.IsImplicit != true);

                var multipleExplicitIndividualPointsSet = individualExplicitPointsCount >= 2;
                var morePointsSetThenAnotherAggregate = individual.Points?.Length > lowestPoints;
                
                if (multipleExplicitIndividualPointsSet || morePointsSetThenAnotherAggregate)
                {
                    var pointsLoop = individual.Points;
                    var isAllPoints = individual.Points?.Length == points.Length;
                    
                    if (isAllPoints)
                    {
                        var areExplicit = individualExplicitPointsCount == points.Length;
                        
                        pointsLoop = points
                            .Select (p => new Point
                            {
                                Id = p.Id,
                                Description = p.Description,
                                IsImplicit = !areExplicit
                            })
                            .ToArray();
                    }

                    newAggregates.Add(new Aggregate
                    {
                        Points = pointsLoop,
                        Purposes = individual.Purposes,
                        TimePeriod = individual.TimePeriod,
                        TimeCutoff = individual.TimeCutoff,
                        DocumentIdentifier = individual.DocumentIdentifier,
                        Limits = individual.Limits,
                        ContainedIn = individual.ContainedIn,
                        SourceLicenceNumber = licenceNumber,
                        SourceLicenceVersionId = licenceVersionId,
                        PrimaryType = PrimaryType.InLicence,
                        NaldType = GetNaldType(naldDataLine),
                        AggregateSetId = PositionConstants.ReplacementMarker
                    });

                    continue;
                }
            }

            if (mixedPurposesCounts)
            {
                var individualExplicitPurposesCount = individual.Purposes?.Count(p => p.IsImplicit != true);

                var multipleExplicitIndividualPurposesSet = individualExplicitPurposesCount >= 2;
                var morePurposesSetThenAnotherAggregate = individual.Purposes?.Length > lowestPurposes;
                
                if (multipleExplicitIndividualPurposesSet || morePurposesSetThenAnotherAggregate)
                {
                    var purposesLoop = individual.Purposes;
                    var isAllPurposes = individual.Purposes?.Length == purposes.Length;

                    if (isAllPurposes)
                    {
                        var areExplicit = individualExplicitPurposesCount == purposes.Length;
                        
                        purposesLoop = purposes
                            .Select (p => new Purpose
                            {
                                Id = p.Id,
                                Description = p.Description,
                                IsImplicit = !areExplicit
                            })
                            .ToArray();
                    }

                    newAggregates.Add(new Aggregate
                    {
                        Points = individual.Points,
                        Purposes = purposesLoop,
                        TimePeriod = individual.TimePeriod,
                        TimeCutoff = individual.TimeCutoff,
                        DocumentIdentifier = individual.DocumentIdentifier,
                        Limits = individual.Limits,
                        ContainedIn = individual.ContainedIn,
                        SourceLicenceNumber = licenceNumber,
                        SourceLicenceVersionId = licenceVersionId,
                        PrimaryType = PrimaryType.InLicence,
                        NaldType = GetNaldType(naldDataLine),
                        AggregateSetId = PositionConstants.ReplacementMarker
                    });

                    continue;
                }
            }

            newIndividuals.Add(individual);
        }

        newAggregates.AddRange(aggregates);
        return (newIndividuals.ToArray(), newAggregates.ToArray());
    }

    private static async Task<List<LinkedLicence>> ConsolidateLinkedLicencesAsync(
        List<LinkedLicence> linkedLicences,
        string? licenceNumber,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService)
    {
        var tempLinkedLicencesGrp = linkedLicences
            .GroupBy(linkedLicence => (
                FormattingHelper.StripForComparison(
                    linkedLicence.LicenceNumber,
                    linkedLicence.RegionId!.Value),
                linkedLicence.RegionId!.Value));

        var tempLinkedLicences = new List<(LinkedLicence linkedLicence, int regionId)>();

        foreach (var linkedLicencesGroup in tempLinkedLicencesGrp)
        {
            var containedIn = new List<ContainedInInformation>();

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
                            sectionItem.Source != InformationSource.Nald
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

            tempLinkedLicences.Add((await ToLinkedLicenceAsync(
                linkedLicenceNumber,
                linkedLicencesGroup
                    .FirstOrDefault(ll => !string.IsNullOrEmpty(ll.RawScrapedLicenceNumber))?
                    .RawScrapedLicenceNumber,
                linkedLicencesGroup
                    .FirstOrDefault(ll => !string.IsNullOrEmpty(ll.DmsPermitNumber))?
                    .DmsPermitNumber,
                linkedLicencesGroup
                    .FirstOrDefault(ll => !string.IsNullOrEmpty(ll.Filename))?
                    .Filename,
                linkedLicencesGroup
                    .FirstOrDefault(ll => ll.Condition != null)?
                    .Condition,
                containedIn.ToArray(),
                linkedLicencesGroup
                    .FirstOrDefault(ll => ll.LicenceVersion.DmsFileIdStatus != null)?
                    .LicenceVersion.Clone() ?? new LicenceVersion(),
                regionId,
                lookupConfiguration.CacheService,
                cacheService,
                linkedLicencesGroup
                    .OrderByDescending(ll => ll.IsBecauseOfAggregate).FirstOrDefault()?.IsBecauseOfAggregate), regionId));
        }
        
        tempLinkedLicences = tempLinkedLicences
            .Where(linkedLicence =>
                !LicenceNumberContainsOther(
                    licenceNumber,
                    linkedLicence.linkedLicence.LicenceNumber,
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
    
    private static string? GetDateFormatConsistent(
        List<LabelGroupResult> matches,
        string labelName,
        bool setConfidence,
        Dictionary<string, object?>? noneSchemaData = null)
    {
        var text = DataHelper.GetTextFromFirstMatchByLabelGroup(matches, labelName, out var labelGroupResult);

        if (setConfidence && labelGroupResult?.Confidence != null)
        {
            noneSchemaData?.Add($"Confidence:{labelName}", labelGroupResult?.Confidence);
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
        var text = DataHelper.GetTextFromFirstMatchByLabelGroup(matches, labelName, out var labelGroupResult);

        if (labelGroupResult?.Confidence != null)
        {
            noneSchemaData.Add($"Confidence:{labelName}", labelGroupResult?.Confidence);
        }

        return text;
    }

    private static (NaldLicenceStatus status, LicenceType licenceType) GetLicenceStatusAndType(
        NaldData? naldData)
    {
        if (naldData == null)
        {
            return (NaldLicenceStatus.Unknown, LicenceType.Unknown);
        }
        
        NaldLicenceStatus status;

        if (naldData.RevocationDate != null && naldData.RevocationDate.Value < DateTime.Now)
        {
            status = NaldLicenceStatus.Revoked;
        }
        else if (naldData.ExpiryDate != null && naldData.ExpiryDate.Value < DateTime.Now)
        {
            status = NaldLicenceStatus.Expired;
        }
        else if (naldData.LapsedDate != null && naldData.LapsedDate.Value < DateTime.Now)
        {
            status = NaldLicenceStatus.Lapsed;
        }
        else if ((naldData.EffEndDate == null || naldData.EffEndDate.Value > DateTime.Now)
            && (naldData.EffStDate == null || DateTime.Now > naldData.EffStDate.Value))
        {
            status = NaldLicenceStatus.Live;
        }
        else
        {
            status = NaldLicenceStatus.Unknown;
        }
        
        var isImpoundmentLicence = false;
        LicenceType type;
        
        if (isImpoundmentLicence)
        {
            type = LicenceType.Impoundment;
        }
        else
            type = naldData.AsrcCode switch
            {
                "G" => LicenceType.GroundWaterAbstraction,
                "S" => LicenceType.SurfaceWaterAbstraction,
                _ => LicenceType.Abstraction
            };

        return (status, type);
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

    private static async Task<LinkedLicence> ToLinkedLicenceAsync(
        string? linkedLicenceNumber,
        string? scrapedLinkedLicenceNumber,
        string? linkedLicencePermitNumber,
        string? filename,
        Condition? condition,
        ContainedInInformation[] containedIn,
        LicenceVersion licenceVersion,
        int? regionId,
        ICacheService cacheService,
        IAbstractionLicenceCacheService abstractionLicenceCacheService,
        bool? isBecauseOfAggregate)
    {
        var licenceOrPermitNumber = linkedLicenceNumber;
        if (string.IsNullOrWhiteSpace(linkedLicenceNumber))
        {
            licenceOrPermitNumber = linkedLicencePermitNumber;
        }

        if (regionId == null)
        {
            throw new Exception("regionId is null");
        }
        
        var naldDataLineTask = Core.AbstractionLicence.Helpers.FormattingHelper.GetNaldDataLineAsync(
            abstractionLicenceCacheService,
            licenceOrPermitNumber,
            regionId.Value);
        
        var dmsFileData = await FormattingHelper.GetDmsFileDataAsync(
            linkedLicenceNumber,
            cacheService);

        var naldDataLine = await naldDataLineTask;
        var (naldStatus, licenceType) = GetLicenceStatusAndType(naldDataLine);

        return new LinkedLicence
        {
            LicenceNumber = linkedLicenceNumber,
            RegionId = naldDataLine?.FgacRegionCode ?? regionId,
            RawScrapedLicenceNumber = scrapedLinkedLicenceNumber,
            DmsPermitNumber = dmsFileData?.PermitNumber,
            DmsFileId = dmsFileData?.FileId,
            DmsPath = dmsFileData?.DmsPath,
            Filename = filename,
            Condition = condition,
            ContainedIn = containedIn,
            NaldStatus = naldStatus,
            LicenceType = licenceType,
            LicenceVersion = licenceVersion,
            IsBecauseOfAggregate = isBecauseOfAggregate
        };
    }

    public static async Task<List<LicenceSet>> ToLicenceSetsAsync(
        MatchesResult matchesResult,
        IPdfDataExtractorService pdfDataExtractorService,
        int processRunId,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService,
        DmsFileData? dmsDataForFile = null,
        string? naldLicenceNumber = null)
    {
        var returnList = new List<LicenceSet>();

        var primaryLicence = await ToLicenceAsync(
            matchesResult,
            dmsDataForFile,
            naldLicenceNumber,
            (NaldLinkedLicenceHelper?)lookupConfiguration.NaldLinkedLicenceHelper,
            lookupConfiguration,
            cacheService,
            processRunId);

        var previouslyParsedPaths = new List<string> { matchesResult.Filename! };

        var linkedLicences = await GetLinkedLicencesAsync(
            primaryLicence,
            pdfDataExtractorService,
            previouslyParsedPaths,
            processRunId,
            lookupConfiguration,
            cacheService);
        
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
                    ci.SectionName == DocumentSectionNames.AbstractionLimits
                    && ci.Direction == InformationDirection.Outgoing) == true)
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

            await AddIncomingLinksAsync(
                [[explicitlyReferencedLicenceSet ?? singleLicenceOnlySet]],
                false,
                lookupConfiguration,
                cacheService);

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

        var beforeRecordList = await lookupConfig.CacheService.GetDmsFileIdInformationAsync(dmsDataForFile.FileId);

        var outputDmsFileIdInformation = new DmsFileIdInformation
        {
            FileId = dmsDataForFile.FileId,
            DmsFilePath = dmsDataForFile.DmsPath,
            ProcessRunId = processRunId,
            StatusDateUtc = DateTime.UtcNow
        };

        if (beforeRecordList.Count == 0)
        {
            outputDmsFileIdInformation.Status = "FirstSeen";
            await lookupConfig.CacheService.AddDmsFileIdInformationAsync(outputDmsFileIdInformation);
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
        }

        return outputDmsFileIdInformation;
    }
    
    private static async Task<List<LicenceSet>> AddIncomingLinksAsync(
        IReadOnlyList<IReadOnlyList<LicenceSet>> licenceSetGroups,
        bool addImplicitLicenceSet,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService)
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
                    if (licence.Status != ScrapeStatus.Ok)
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
                            ll.ContainedIn?.Any(ci => ci.Direction == InformationDirection.Incoming) == true))
                        {
                            continue;
                        }

                        var newSections = incomingLink.Sections
                            .Select(section => new ContainedInInformation
                            {
                                Source = InformationSource.OtherDocument,
                                Direction = InformationDirection.Incoming,
                                SectionName = section.SectionName,
                                LinkReason = section.LinkReason,
                                LineNumber = section.LineNumber,
                                PageNumber = section.PageNumber
                            })
                            .ToArray();

                        var incomingLicence = allLicencesInSets
                            .FirstOrDefault(l => l.LicenceNumber?.Value == incomingLink.LicenceNumber);
                        
                        var incomingLinkedLicence = await ToLinkedLicenceAsync(
                            incomingLink.LicenceNumber,
                            incomingLink.ScrapedLicenceNumber,
                            null,
                            incomingLink.Filename,
                            null,
                            newSections,
                            incomingLicence?.LicenceVersion.Clone() ?? new LicenceVersion(),
                            licence.RegionId,
                            lookupConfiguration.CacheService,
                            cacheService,
                            null);

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

                    licence.LinkedLicences = (await ConsolidateLinkedLicencesAsync(
                        licence.LinkedLicences.ToList(),
                        licence.LicenceNumber?.Value,
                        lookupConfiguration,
                        cacheService)).ToArray();
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
        List<ContainedInInformation> Sections)>
        GetLicencesReferencingLicenceInDocument(IEnumerable<Licence> licences, string licenceNumber)
    {
        var returnList = new List<(string, string, string?, List<ContainedInInformation>)>();

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
                            Source: InformationSource.Document,
                            Direction: InformationDirection.Outgoing
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
                        Source: InformationSource.Document,
                        Direction: InformationDirection.Outgoing
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
                    Aggregates = [AggregateWithContext.FromAggregate(aggregate)]
                };

                // Need to do this as the version in the aggregate set is a clone
                aggregate.AggregateSetId = aggregateSet.SetAggregateSetId(allLicences);
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
                            lln => licences.Any(l => l.LicenceNumber?.Value == lln)))
                    .ToArray();
            }

            aggregates.AddRange(relevantAggregates);
        }

        var aggregatesGroupedByLicencesList = aggregates
            .GroupBy(aggregate =>
            {
                var allLicenceNumbers = new List<string> { aggregate.SourceLicenceNumber! };
                allLicenceNumbers.AddRange(aggregate.LinkedLicences ?? []);
                
                return string.Join(',', allLicenceNumbers.OrderBy(lln => lln));
            })
            .ToList();

        var aggregateSets = new List<AggregateSet>();

        foreach (var aggregatesGroupedByLicences in aggregatesGroupedByLicencesList)
        {
            var aggregateSet = new AggregateSet
            {
                Aggregates = aggregatesGroupedByLicences
                    .Select(AggregateWithContext.FromAggregate)
                    .ToArray()
            };

            var aggregateSetId = aggregateSet.SetAggregateSetId(allLicences);

            // Need to do this as the version in the aggregate set is a clone
            foreach (var aggregate in aggregatesGroupedByLicences)
            {
                aggregate.AggregateSetId = aggregateSetId;
            }
            
            aggregateSets.Add(aggregateSet);
        }

        return aggregateSets.Count == 0 ? null : aggregateSets.ToArray();
    }

    private static async Task<List<Licence>> GetLinkedLicencesAsync(
        Licence primaryLicence,
        IPdfDataExtractorService pdfDataExtractorService,
        List<string> previouslyParsedFiles,
        int processRunId,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService)
    {
        var returnLicences = new List<Licence>();
        
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

            var dmsFileData = await FormattingHelper.GetDmsFileDataAsync(
                linkedLicence.LicenceNumber,
                lookupConfiguration.CacheService);

            var foundDmsData = dmsFileData != null;

            var destinationFileId = dmsFileData?.FileId;
            var destinationFileName = dmsFileData?.DestinationFileName;
            
            var missingDmsData = !foundDmsData;
            var missingFileId = destinationFileId == Guid.Empty || destinationFileId == null;
            var missingFilename = string.IsNullOrEmpty(destinationFileName);
                        
            if (missingDmsData || missingFileId || missingFilename)
            {
                var status = ScrapeStatus.NotFound;
                
                if (missingDmsData) {}
                else if (missingFilename) status = ScrapeStatus.PathMissing;
                else if (missingFileId) status = ScrapeStatus.FileIdMissing;
                
                returnLicences.Add(new Licence
                {
                    LicenceNumber = new ValueWithConfidence<string>(linkedLicence.LicenceNumber, -1, -1),
                    Status = status,
                    RegionId = primaryLicence.RegionId!.Value,
                });
                
                continue;
            }
            
            var naldDataLine = await Core.AbstractionLicence.Helpers.FormattingHelper.GetNaldDataLineAsync(
                cacheService,
                linkedLicence.LicenceNumber,
                primaryLicence.RegionId!.Value);

            var clonedConfig = lookupConfiguration.Clone();
            clonedConfig.RegionId = naldDataLine?.FgacRegionCode ?? primaryLicence.RegionId!.Value;

            (bool StopExecution, bool? AlreadySaved, MatchesResult? Item) relatedFileMatches;

            try
            {
                relatedFileMatches = await pdfDataExtractorService.GetMatchesAsync(
                    destinationFileName!,
                    dmsFileData!,
                    clonedConfig,
                    previouslyParsedFiles,
                    processRunId);

                if (relatedFileMatches.StopExecution)
                {
                    continue;
                }
                
                ConsoleHelper.WriteLine($"INFO - {nameof(AbstractionLicenceSchemaConverter)} - Finished/released lock/saving for {dmsFileData!.FileId}");

                if (relatedFileMatches.AlreadySaved != true && lookupConfiguration.UseLockExclusivity)
                {
                    await pdfDataExtractorService.SaveMatchResultAsync(
                        relatedFileMatches.Item!,
                        dmsFileData.FileId,
                        processRunId);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(AbstractionLicenceSchemaConverter)} - {dmsFileData!.FileId} had error, releasing lock");
                
                await lookupConfiguration.OutputService.SaveErrorMatchesResultAsync(
                    destinationFileName!,
                    dmsFileData.FileId,
                    processRunId,
                    ex.ToString());
                
                throw;
            }

            if (relatedFileMatches.StopExecution)
            {
                continue;
            }
            
            var licence = await ToLicenceAsync(
                relatedFileMatches.Item!,
                dmsFileData,
                naldDataLine?.LicenceNumber,
                (NaldLinkedLicenceHelper?)lookupConfiguration.NaldLinkedLicenceHelper,
                lookupConfiguration,
                cacheService,
                processRunId);

            if (licence.Status == ScrapeStatus.Error)
            {
                continue;
            }
            
            returnLicences.Add(licence);
        }

        var allLicences = new List<Licence>
        {
            primaryLicence
        };
        
        allLicences.AddRange(returnLicences);
        
        foreach (var licence in allLicences)
        {
            foreach (var linkedLicence in licence.LinkedLicences)
            {
                var linkedLicenceFull = allLicences
                    .FirstOrDefault(l => l.LicenceNumber?.Value == linkedLicence.LicenceNumber);

                if (linkedLicenceFull != null)
                {
                    linkedLicence.LicenceVersion = linkedLicenceFull.LicenceVersion.Clone();
                }
                
                linkedLicence.DmsFileId = linkedLicenceFull?.DmsFileId;
                linkedLicence.DmsPath = linkedLicenceFull?.DmsPath;
            }
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

        var isFrom = value.Contains("From ", StringComparison.OrdinalIgnoreCase);
        var isUntil = value.Contains("Until ", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Up to and including", StringComparison.OrdinalIgnoreCase);
        
        if (!isFrom && !isUntil)
        {
            return null;
        }

        var parts = value
            .Replace("From", "~", StringComparison.OrdinalIgnoreCase)
            .Replace("Until", "~", StringComparison.OrdinalIgnoreCase)
            .Replace("Up to and including", "~", StringComparison.OrdinalIgnoreCase)
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
        
        var isFrom = value.Contains("From ", StringComparison.OrdinalIgnoreCase);
        var isUntil = value.Contains("Until ", StringComparison.OrdinalIgnoreCase);
        
        if (isFrom || isUntil)
        {
            return null;
        }
        
        var hasTo = value.Contains(" to ", StringComparison.OrdinalIgnoreCase);
        var hasBeginningOn = value.Contains("beginning on ", StringComparison.OrdinalIgnoreCase);

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
    
    private static async Task<(
        List<LinkedLicence> LinkedLicences,
        List<AbstractionLimitGroup> AbstractionLimits,
        List<Aggregate> Aggregates)> GetSectionDataAsync(
            string sectionName,
            List<LabelGroupResult> matches,
            int regionCode,
            string? licenceNumber,
            string? licenceVersionId,
            PointOfAbstraction[] allPoints,
            PurposeOfAbstraction[] allPurposes,
            NaldData? naldDataLine,
            Dictionary<string, object?> noneSchemaData,
            LookupConfiguration lookupConfiguration,
            IAbstractionLicenceCacheService cacheService,
            AbstractionLimitGroup[] previouslyFoundIndividualLimits)
    {
        var section = matches
            .FirstOrDefault(result => result.LabelGroupName == sectionName);

        if (section == null)
        {
            return ([], [], []);
        }
        
        var sectionPoints = section.SubResults;
        
        var sectionLinkedLicences = new List<LinkedLicence>();
        var abstractionLimits = new List<AbstractionLimitGroup>();
        var aggregates = new List<Aggregate>();
        
        var count = 0;

        foreach (var sectionPoint in sectionPoints)
        {
            var abstractionLimitPointSubs = sectionPoint.SubResults
                .Where(linkedLicenceNumber =>
                    linkedLicenceNumber.MatchedLabelName == "AbstractionLimitPointSub")
                .ToList();

            foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
            {
                await GetAbstractionLimitsFromSectionAsync(
                    abstractionLimitPointSub,
                    licenceNumber,
                    licenceVersionId,
                    allPoints,
                    allPurposes,
                    naldDataLine,
                    regionCode,
                    sectionName,
                    lookupConfiguration,
                    abstractionLimits,
                    aggregates,
                    sectionLinkedLicences,
                    noneSchemaData,
                    cacheService,
                    previouslyFoundIndividualLimits);
            }

            var linkedLicenceNumbers = sectionPoint.SubResults
                .Where(linkedLicenceNumber =>
                    linkedLicenceNumber.MatchedLabelName == $"{sectionName}LinkedLicenceNumber")
                .ToList();

            foreach (var linkedLicenceNumber in linkedLicenceNumbers)
            {
                sectionLinkedLicences.Add(
                    await LabelResultToLinkedLicenceAsync(
                        linkedLicenceNumber,
                        section,
                        sectionName,
                        regionCode,
                        count++,
                        noneSchemaData,
                        lookupConfiguration,
                        cacheService));
            }
        }

        foreach (var abstractionLimitGroup in abstractionLimits)
        {
            NullOutLimitLevelPointsAndPurposesIfRelevant(abstractionLimitGroup, abstractionLimitGroup.Limits);   
        }
        
        return (sectionLinkedLicences, abstractionLimits, aggregates);
    }

    private static async Task<LinkedLicence> LabelResultToLinkedLicenceAsync(
        LabelGroupResult linkedLicenceNumber,
        LabelGroupResult section,
        string sectionName,
        int regionCode,
        int count,
        Dictionary<string, object?> noneSchemaData,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService)
    {
        var licenceNumberLoop = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;
        
        var dmsFileDataTask = FormattingHelper.GetDmsFileDataAsync(
            licenceNumberLoop,
            lookupConfiguration.CacheService);
        
        var naldDataLineLoop = await Core.AbstractionLicence.Helpers.FormattingHelper.GetNaldDataLineAsync(
            cacheService,
            licenceNumberLoop,
            regionCode);

        var (naldStatus, licenceType) = GetLicenceStatusAndType(naldDataLineLoop);
        var dmsFileData = await dmsFileDataTask;

        if (linkedLicenceNumber.Confidence != null)
        {
            noneSchemaData.Add($"Confidence:LinkedLicence_{sectionName}_{count}",
                linkedLicenceNumber.Confidence);
        }

        return new LinkedLicence
        {
            LicenceNumber = licenceNumberLoop,
            RegionId = naldDataLineLoop?.FgacRegionCode ?? regionCode,
            RawScrapedLicenceNumber = licenceNumberLoop,
            DmsPermitNumber = dmsFileData?.PermitNumber,
            Filename = dmsFileData?.DestinationFileName,
            DmsPath = dmsFileData?.DmsPath,
            NaldStatus = naldStatus,
            LicenceType = licenceType,
            ContainedIn =
            [
                new ContainedInInformation
                {
                    Source = InformationSource.Document,
                    Direction = InformationDirection.Outgoing,
                    SectionName = sectionName,
                    LinkReason = GetLinkReason(
                        [GetParent(section, linkedLicenceNumber)],
                        linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                    LineNumber = linkedLicenceNumber.LabelStartLineNumber,
                    PageNumber = linkedLicenceNumber.LabelStartPageNumber
                }
            ]
        };
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

    private static async Task<List<LinkedLicence>> GetAnywhereInDocumentLinkedLicencesAsync(
        List<LabelGroupResult> matches,
        int regionCode,
        Dictionary<string, object?> noneSchemaData,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService)
    {
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
            if (generalLinkedLicenceNumber is { LabelStartPageNumber: 1, LabelStartLineNumber: <= 3 })
            {
                continue;
            }

            var linkedLicenceNumber = generalLinkedLicenceNumber.Text?.FirstOrDefault()?.Text;

            var dmsFileDataTask = FormattingHelper.GetDmsFileDataAsync(
                linkedLicenceNumber,
                lookupConfiguration.CacheService);
            
            var naldDataLine = await Core.AbstractionLicence.Helpers.FormattingHelper.GetNaldDataLineAsync(
                cacheService,
                linkedLicenceNumber,
                regionCode);
            
            var (naldStatus, licenceType) = GetLicenceStatusAndType(naldDataLine);
            var dmsFileData = await dmsFileDataTask;

            if (generalLinkedLicenceNumber.Confidence != null)
            {
                noneSchemaData.Add($"Confidence:LinkedLicence_SomewhereInDocument_{count++}",
                    generalLinkedLicenceNumber.Confidence);
            }

            returnList.Add(new LinkedLicence
            {
                LicenceNumber = linkedLicenceNumber,
                RegionId = naldDataLine?.FgacRegionCode ?? regionCode,
                RawScrapedLicenceNumber = linkedLicenceNumber,
                DmsPermitNumber = dmsFileData?.PermitNumber,
                Filename = dmsFileData?.DestinationFileName,
                DmsPath = dmsFileData?.DmsPath,
                NaldStatus = naldStatus,
                LicenceType = licenceType,
                ContainedIn =
                [
                    new ContainedInInformation
                    {
                        Source = InformationSource.Document,
                        Direction = InformationDirection.Outgoing,
                        SectionName = GetUnknownSectionName(generalLinkedLicenceNumber.LabelStartPageNumber),
                        LinkReason = GetLinkReason([generalLinkedLicenceNumber], linkedLicenceNumber),
                        LineNumber = generalLinkedLicenceNumber.LabelStartLineNumber,
                        PageNumber = generalLinkedLicenceNumber.LabelStartPageNumber
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
            1 => DocumentSectionNames.UnknownPage1,
            2 => DocumentSectionNames.UnknownPage2,
            3 => DocumentSectionNames.UnknownPage3,
            4 => DocumentSectionNames.UnknownPage4,
            5 => DocumentSectionNames.UnknownPage5,
            6 => DocumentSectionNames.UnknownPage6,
            7 => DocumentSectionNames.UnknownPage7,
            8 => DocumentSectionNames.UnknownPage8,
            9 => DocumentSectionNames.UnknownPage9,
            _ => DocumentSectionNames.Unknown
        };
    }

    private static async Task<List<LinkedLicence>> GetLicenceHistoryLinkedLicencesAsync(
        LabelGroupResult? licenceHistorySection,
        int regionCode,
        Dictionary<string, object?> noneSchemaData,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService)
    {
        if (licenceHistorySection == null)
        {
            return [];
        }
        
        var count = 0;

        var licenceHistoryLinkedLicenceNumbers = licenceHistorySection
            .SubResults
            .Where(linkedLicenceNumber =>
                linkedLicenceNumber.MatchedLabelName == "LicenceHistoryLinkedLicenceNumber")
            .ToList();

        var returnList = new List<LinkedLicence>();

        foreach (var linkedLicenceNumber in licenceHistoryLinkedLicenceNumbers)
        {
            var lln = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;
            var licenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;
            
            var dmsFileDataTask = FormattingHelper.GetDmsFileDataAsync(
                licenceNumber,
                lookupConfiguration.CacheService);
            
            var naldDataLine = await Core.AbstractionLicence.Helpers.FormattingHelper.GetNaldDataLineAsync(
                cacheService,
                lln, regionCode);
            
            var (naldStatus, licenceType) = GetLicenceStatusAndType(naldDataLine);
            var dmsFileData = await dmsFileDataTask;

            if (linkedLicenceNumber.Confidence != null)
            {
                noneSchemaData.Add($"Confidence:LinkedLicence_LicenceHistory_{count++}",
                    linkedLicenceNumber.Confidence);
            }

            returnList.Add(new LinkedLicence
            {
                LicenceNumber = lln,
                RegionId = naldDataLine?.FgacRegionCode ?? regionCode,
                RawScrapedLicenceNumber = lln,
                DmsPermitNumber = dmsFileData?.PermitNumber,
                Filename = dmsFileData?.DestinationFileName,
                DmsPath = dmsFileData?.DmsPath,
                NaldStatus = naldStatus,
                LicenceType = licenceType,
                ContainedIn =
                [
                    new ContainedInInformation
                    {
                        Source = InformationSource.Document,
                        Direction = InformationDirection.Outgoing,
                        SectionName = DocumentSectionNames.LicenceHistory,
                        LinkReason =
                            GetLinkReason([licenceHistorySection],
                                lln), // We haven't split licence history into sections like the others
                        LineNumber = linkedLicenceNumber.LabelStartLineNumber,
                        PageNumber = linkedLicenceNumber.LabelStartPageNumber
                    }
                ]
            });
        }

        return returnList;
    }

    private static async Task<List<LinkedLicence>> GetPurposesLinkedLicencesAsync(
        List<LabelGroupResult> matches,
        int regionCode,
        Dictionary<string, object?> noneSchemaData,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService)
    {
        var purposeSection = matches
            .FirstOrDefault(result => result.LabelGroupName == "Purposes");

        if (purposeSection == null)
        {
            return [];
        }

        var count = 0;
        var returnList = new List<LinkedLicence>();

        foreach (var purposePointGroup in purposeSection.SubResults)
        {
            var purposes = purposePointGroup.SubResults
                .Where(x => x.MatchedLabelName == "Purposes")
                .ToList();

            foreach (var purpose in purposes)
            {
                var purposeLinkedLicenceNumber = purpose.SubResults
                    .Where(linkedLicenceNumber =>
                        linkedLicenceNumber.MatchedLabelName == "PurposeLinkedLicenceNumber")
                    .ToList();

                foreach (var linkedLicenceNumber in purposeLinkedLicenceNumber)
                {
                    returnList.Add(await LabelResultToLinkedLicenceAsync(
                        linkedLicenceNumber,
                        purposePointGroup,
                        DocumentSectionNames.Purposes,
                        regionCode,
                        count++,
                        noneSchemaData,
                        lookupConfiguration,
                        cacheService));
                }
            }
        }

        return returnList;
    }

    private static async Task<List<LinkedLicence>> GetPointsLinkedLicencesAsync(
        List<LabelGroupResult> matches,
        int regionCode,
        Dictionary<string, object?> noneSchemaData,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService)
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
                .Where(x => x.MatchedLabelName == "Point")
                .ToList();

            foreach (var point in points)
            {
                var linkedLicenceNumbers = point.SubResults
                    .Where(linkedLicenceNumber =>
                        linkedLicenceNumber.MatchedLabelName == "LinkedLicenceNumber")
                    .ToList();

                foreach (var linkedLicenceNumber in linkedLicenceNumbers)
                {
                    returnList.Add(await LabelResultToLinkedLicenceAsync(
                        linkedLicenceNumber,
                        pointPurposeGroup,
                        DocumentSectionNames.Points,
                        regionCode,
                        count++,
                        noneSchemaData,
                        lookupConfiguration,
                        cacheService));
                }
            }
        }

        return returnList;
    }

    private static string? GetLinkReason(List<LabelGroupResult> sections, string? textToFind)
    {
        foreach (var section in sections)
        {
            var text = string.Join('\n', section.Text!.Select(t => t.Text));
            var result = GetLinkReason(text, textToFind);

            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        return null;
    }

    private static string? GetLinkReason(string? text, string? textToFind)
    {
        if (string.IsNullOrEmpty(textToFind)
            || string.IsNullOrEmpty(text)
            || !text.Contains(textToFind))
        {
            return null;
        }

        if (text.Contains("lapsed licence", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.LapsedLicence;
        }

        if (text.Contains("discharge and re-abstraction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.DischargeAndReabstractionCondition;
        }

        if (text.Contains("simultaneous discharge", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.SimultaneousDischargeCondition;
        }

        if (text.Contains("simultaneous abstraction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.SimultaneousAbstractionCondition;
        }

        if (text.Contains("simultaneous compensatory discharge", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.SimultaneousCompensatoryDischargeCondition;
        }

        if (text.Contains("compensatory discharge", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.CompensatoryDischargeCondition;
        }
        
        if (text.Contains("compensation flow", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.CompensationFlow;
        }

        if (text.Contains("read in conjunction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ReadInConjunction;
        }

        if (text.Contains("The donor licence was", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.DonorLicence;
        }

        if (text.Contains("used in conjunction", StringComparison.OrdinalIgnoreCase)
            || text.Contains("use in conjunction", StringComparison.OrdinalIgnoreCase)) // misspelling
        {
            return LinkReason.UsedInConjunction;
        }

        if (text.Contains("revocation", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.Revocation;
        }
        
        if (text.Contains("aggregate conditions", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.AggregateConditions;
        }

        if (text.Contains("emergency circumstances", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.EmergencyCircumstances;
        }

        if (text.Contains("Dewatering Discharge", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.DewateringDischargeCondition;
        }

        if (text.Contains("when added to", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.WhenAddedTo;
        }

        if (text.Contains("subsequent abstraction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.SubsequentAbstraction;
        }

        if (text.Contains("re-abstraction", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ReAbstraction;
        }

        if (text.Contains("readings", StringComparison.OrdinalIgnoreCase)
            && text.Contains("discharged", StringComparison.OrdinalIgnoreCase)
            && text.Contains("augmentation", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ReadingsDischargedAugmentationCondition;
        }

        if (text.Contains("aggregate", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.AggregateCondition;
        }
        
        if (text.Contains("in total between", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.AggregateCondition;
        }

        if (text.Contains("in an emergency", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.InAnEmergency;
        }
        
        if (text.Contains("shall not exceed", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ShallNotExceed;
        }

        if (text.Contains("supporting", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.Supporting;
        }

        if (text.Contains("original licence", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.OriginalLicence;
        }

        if (text.Contains("transferred to this", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.TransferredToThis;
        }

        if (text.Contains("coincident", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.Coincident;
        }

        if (text.Contains("shall be supported", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ShallBeSupported;
        }

        if (text.Contains("residual flow", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.ResidualFlow;
        }
        
        if (text.Contains("authorised by", StringComparison.OrdinalIgnoreCase))
        {
            return LinkReason.AuthorisedBy;
        }

        return null;
    }

    private static async Task<(Aggregate[] aggregates, AbstractionLimitGroup[] indiviudal, LinkedLicence[] linkedLicences)>
        GetAbstractionLimitsAsync(
            List<LabelGroupResult> matches,
            string? licenceNumber,
            string? licenceVersionId,
            PointOfAbstraction[] allPoints,
            PurposeOfAbstraction[] allPurposes,
            NaldData? naldDataLine,
            int regionCode,
            Dictionary<string, object?> noneSchemaData,
            LookupConfiguration lookupConfiguration,
            IAbstractionLicenceCacheService cacheService)
    {
        var abstractionLimitsSection = matches
            .FirstOrDefault(result => result.LabelGroupName == DocumentSectionNames.AbstractionLimits);

        var abstractionLimitPoints = abstractionLimitsSection?
            .SubResults
            .Where(res => res.MatchedLabelName == "AbstractionLimitPoint")
            .ToList();

        var abstractionLimitPointSubs = abstractionLimitPoints?
            .SelectMany(res => res.SubResults)
            .Where(res => res.MatchedLabelName == "AbstractionLimitPointSub")
            .ToList();

        if (abstractionLimitPointSubs == null)
        {
            return ([], [], []);
        }

        var allAggregateLinkedLicences = new List<LinkedLicence>();
        var allAggregates = new List<Aggregate>();
        var allIndividualGroups = new List<AbstractionLimitGroup>();

        foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
        {
            await GetAbstractionLimitsFromSectionAsync(
                abstractionLimitPointSub,
                licenceNumber,
                licenceVersionId,
                allPoints,
                allPurposes,
                naldDataLine,
                regionCode,
                DocumentSectionNames.AbstractionLimits,
                lookupConfiguration,
                allIndividualGroups,
                allAggregates,
                allAggregateLinkedLicences,
                noneSchemaData,
                cacheService,
                []);
        }

        if (allIndividualGroups is [{ Limits.Count: 0 }])
        {
            allIndividualGroups.Clear();
        }
        
        foreach (var individualGroup in allIndividualGroups)
        {
            NullOutLimitLevelPointsAndPurposesIfRelevant(individualGroup, individualGroup.Limits);   
        }
        
        return (
            allAggregates.ToArray(),
            allIndividualGroups.ToArray(),
            allAggregateLinkedLicences.ToArray());
    }
    
    private static async Task GetAbstractionLimitsFromSectionAsync(
        LabelGroupResult abstractionLimitPointSub,
        string? licenceNumber,
        string? licenceVersionId,
        PointOfAbstraction[] allPoints,
        PurposeOfAbstraction[] allPurposes,
        NaldData? naldDataLine,
        int regionCode,
        string sectionName,
        LookupConfiguration lookupConfiguration,
        List<AbstractionLimitGroup> allIndividualGroups,
        List<Aggregate> allAggregates,
        List<LinkedLicence> sectionLinkedLicences,
        Dictionary<string, object?> noneSchemaData,
        IAbstractionLicenceCacheService cacheService,
        AbstractionLimitGroup[] previouslyFoundIndividualLimits)
    {
        var individualGroups = new List<AbstractionLimitGroup>();

        var limitPointTable = abstractionLimitPointSub.SubResults
            .FirstOrDefault(x => x.MatchedLabelName == "LimitPointTable");

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

        var linkReason = GetLinkReason([abstractionLimitPointSub], " "); // Text to find being a space is a bit of a hack
        
        var containedIn = new ContainedInInformation[]
        {
            new()
            {
                Source = InformationSource.Document,
                Direction = InformationDirection.Outgoing,
                SectionName = sectionName,
                LinkReason = linkReason,
                PageNumber = abstractionLimitPointSub.LabelStartPageNumber,
                LineNumber = abstractionLimitPointSub.LabelStartLineNumber
            }
        };
        
        var documentIdentifier = abstractionLimitPointSub.SubResults
            .FirstOrDefault(sr => sr.MatchedLabelName == "DocumentIdentifier")?
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
                    ConsoleHelper.WriteLine($"INFO - {nameof(AbstractionLicenceSchemaConverter)} - Table was not in the expected format. Skipping");
                    continue;
                }
                
                var points = new Point[]
                {
                    new()
                    {
                        Id = abstractionPoint,
                        IsImplicit = false
                    }
                };
                
                var lineAbstractionLimitGroup = new AbstractionLimitGroup
                {
                    Points = points,
                    Purposes = allPurposes
                        .Select(p => new Purpose
                        {
                            IsImplicit = true,
                            Id = p.Id,
                            Description = p.Description
                        })
                        .ToArray(),
                    DocumentIdentifier = documentIdentifier,
                    ContainedIn = containedIn,
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
            return;
        }

        var siblings = abstractionLimitPointSub.SubResults;
        
        var purposeConditions = siblings
            .Where(x => x.MatchedLabelName is "PurposeCondition" or "PurposeConditionSingleLine")
            .ToList();
                
        var purposeConditionSub = purposeConditions
            .SelectMany(pc => pc.SubResults)
            .Where(x => x.MatchedLabelName is "PurposeConditionSub" or "PurposeConditionSingleLineSub")
            .ToList();
        
        var abstractionLimitPointSubText = string.Join(" ", abstractionLimitPointSub.Text?
            .Select(l => l.Text) ?? []);
        
        var limitPurposes = purposeConditionSub.Count > 0 ?
            purposeConditionSub
                .Select(pcs =>
                {
                    var text = FormattingHelper.CapitaliseFirstLetter(pcs.Text!.FirstOrDefault()?.Text);
                    var documentPurpose = allPurposes.FirstOrDefault(ap => ap.Id == text
                        || ap.Description?.Equals(text, StringComparison.InvariantCultureIgnoreCase) == true);

                    if (documentPurpose == null)
                    {
                        return null;
                    }
                    
                    var purpose = new Purpose
                    {
                        Id = documentPurpose.Id,
                        Description = documentPurpose.Description,
                        IsImplicit = false
                    };

                    return purpose;
                })
                .Where(p => p != null)
                .Select(p => p!)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToList()
            : null;

        limitPurposes = CheckForGenericTransferPurpose(limitPurposes, abstractionLimitPointSubText);
        
        foreach (var documentPurpose in allPurposes)
        {
            var documentPurposeNameSet = !string.IsNullOrEmpty(documentPurpose.Description);
            var documentPurposeIdSet = !string.IsNullOrEmpty(documentPurpose.Id);
            
            var nameInSingleQuotes = $"'{documentPurpose.Description}'";
            var idInSingleQuotes = $"'{documentPurpose.Id}'";
            
            var textContainsPurposeName1 = documentPurposeNameSet &&
                abstractionLimitPointSubText.Contains(
                    nameInSingleQuotes,
                    StringComparison.OrdinalIgnoreCase);
            
            var textContainsPurposeName2 = documentPurposeNameSet && documentPurpose.Description!.Length > 10 &&
                abstractionLimitPointSubText.Contains(
                    documentPurpose.Description!,
                    StringComparison.OrdinalIgnoreCase);
            
             var textContainsPurposeId1 = documentPurposeIdSet &&
                abstractionLimitPointSubText.Contains(
                    idInSingleQuotes,
                    StringComparison.OrdinalIgnoreCase);
            
            var textContainsPurposeId2 = documentPurposeIdSet
                && documentPurpose.Id?.Contains(')') == true
                && abstractionLimitPointSubText.Contains(
                    documentPurpose.Id!,
                    StringComparison.OrdinalIgnoreCase);

            var textContains = textContainsPurposeName1
                || textContainsPurposeName2
                || textContainsPurposeId1
                || textContainsPurposeId2;

            var matchedDocumentPurpose = limitPurposes?
                .FirstOrDefault(lp => lp.Id?.Equals(documentPurpose.Id, StringComparison.OrdinalIgnoreCase) == true
                    || lp.Id?.Equals(documentPurpose.Description, StringComparison.OrdinalIgnoreCase) == true
                    || lp.Description?.Equals(documentPurpose.Description, StringComparison.OrdinalIgnoreCase) == true);

            var allPurposesContains = matchedDocumentPurpose != null;
            
            if (!textContains || allPurposesContains)
            {
                continue;
            }
            
            limitPurposes ??= [];
            limitPurposes.Add(new Purpose
            {
                Id = documentPurpose.Id,
                Description = documentPurpose.Description,
                IsImplicit = false
            });
        }
        
        var pointCondition = siblings
            .Where(x => x.MatchedLabelName is "PointCondition" or "PointConditionSingleLine")
            .ToList();

        var pointConditionSub = pointCondition
            .SelectMany(pc => pc.SubResults)
            .Where(x => x.MatchedLabelName is "PointConditionSub" or "PointConditionSingleLineSub")
            .ToList();
        
        var limitPoints = pointConditionSub.Count > 0 ?
            pointConditionSub
                .Select(pcs =>
                    new Point
                    {
                        Id = pcs.Text!.FirstOrDefault()?.Text,
                        IsImplicit = false
                    })
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .Where(p => allPoints.Any(ap => ap.Id == p.Id))
                .ToList()
            : null;

        foreach (var documentPoint in allPoints)
        {
            var documentPointNameSet = !string.IsNullOrEmpty(documentPoint.Name);
            var nameInSingleQuotes = $"'{documentPoint.Name}'";
            var abstractionPointExplicit = $"Abstraction Point {documentPoint.Id}";
            var abstractionPointInQuotesExplicit = $"Abstraction Point '{documentPoint.Id}'";
            var abstractionPointExplicitAlt = !string.IsNullOrEmpty(documentPoint.AltId)
                ? $"Abstraction Point {documentPoint.AltId}"
                : "[NEVER_FIND_THIS]";
            var abstractionPointInQuotesExplicitAlt = !string.IsNullOrEmpty(documentPoint.AltId)
                ? $"Abstraction Point '{documentPoint.AltId}'"
                : "[NEVER_FIND_THIS]";
            
            var textContainsPointName1 = documentPointNameSet &&
                abstractionLimitPointSubText.Contains(
                    nameInSingleQuotes,
                    StringComparison.OrdinalIgnoreCase);
            
            var textContainsPointName2 = documentPointNameSet &&
                abstractionLimitPointSubText.Contains(
                    abstractionPointExplicit,
                    StringComparison.OrdinalIgnoreCase);
            
            var textContainsPointName3 = documentPointNameSet &&
                abstractionLimitPointSubText.Contains(
                    abstractionPointExplicitAlt,
                    StringComparison.OrdinalIgnoreCase);
            
            var textContainsPointName4 = documentPointNameSet &&
                abstractionLimitPointSubText.Contains(
                    abstractionPointInQuotesExplicit,
                    StringComparison.OrdinalIgnoreCase);
            
            var textContainsPointName5 = documentPointNameSet &&
                abstractionLimitPointSubText.Contains(
                    abstractionPointInQuotesExplicitAlt,
                    StringComparison.OrdinalIgnoreCase);            
            
            var textContainsPointName6 = documentPointNameSet &&
                documentPoint.Name!.Length > 10 &&
                abstractionLimitPointSubText.Contains(
                    documentPoint.Name!,
                    StringComparison.OrdinalIgnoreCase);

            var textContainsPoint = textContainsPointName1
                || textContainsPointName2 
                || textContainsPointName3
                || textContainsPointName4                
                || textContainsPointName5                
                || textContainsPointName6;
            
            if (!textContainsPoint || limitPoints?.Any(lp => lp.Id == documentPoint.Name) == true)
            {
                continue;
            }
            
            limitPoints ??= [];
            limitPoints.Add(new Point
            {
                Id = documentPoint.Id,
                IsImplicit = false
            });
        }
        
        var wordedAsAggregateButAllPurposes = abstractionLimitPointSubText.Contains("The aggregate quantity", StringComparison.OrdinalIgnoreCase)
            && abstractionLimitPointSubText.Contains("for all purposes", StringComparison.OrdinalIgnoreCase)
            || (abstractionLimitPointSubText.Contains("for the purposes of", StringComparison.OrdinalIgnoreCase)
                && allPurposes.Length > 1
                && limitPurposes?.Count == allPurposes.Length);
        
        var textSuggestsIsAggregate = 
            (abstractionLimitPointSubText.Contains("The aggregate quantity", StringComparison.OrdinalIgnoreCase)
                && !wordedAsAggregateButAllPurposes)
            || abstractionLimitPointSubText.Contains("The quantities detailed below are in aggregate", StringComparison.OrdinalIgnoreCase)
            || abstractionLimitPointSubText.Contains("quantity equal to the difference between", StringComparison.OrdinalIgnoreCase)
            || abstractionLimitPointSubText.Contains("In aggregate with licence", StringComparison.OrdinalIgnoreCase);

        var textIsMisleadinglyWordedAsAggregate =
            abstractionLimitPointSubText.Contains("In aggregate from both sources", StringComparison.OrdinalIgnoreCase);

        if (textIsMisleadinglyWordedAsAggregate)
        {
            textSuggestsIsAggregate = false;
        }
        
        var datePurposesTimePeriods = siblings
            .Where(sibling => sibling.MatchedLabelName == "DatePurposeRough")
            .ToList(); // E.g. Jan, Feb etc..
        
        var timeCutoff = GetTimeCutoff(
            siblings.FirstOrDefault(s => s.MatchedLabelName == "DateOnly"));

        var valueResults = siblings
            .Where(sibling => !string.IsNullOrEmpty(sibling.MatchedLabelRelatedName))
            .ToList();

        var multiplePointsSpecified = limitPoints?.Count > 1;
        var thisLimitedByPoints = multiplePointsSpecified && limitPoints!.Count != allPoints.Length;

        var multiplePurposesSpecified = limitPurposes?.Count > 1;
        var thisLimitedByPurpose = multiplePurposesSpecified && limitPurposes!.Count != allPurposes.Length;
                
        var othersLimitedByPurpose = allIndividualGroups.Any(g =>
            g.Purposes?.Count(p => p.IsImplicit != true) > 0
            && g.Purposes?.Count(p => p.IsImplicit != true) != allPurposes.Length);

        var containsUnderThisLicenceText = abstractionLimitPointSubText.Contains("under this licence");
        
        // Need to see if there are any limits that were for a single point or purpose and this has
        // multiple points or purposes
        var alreadyHadSpecificLimitsForAPointOrPurpose = allIndividualGroups.Any(
            ig => ig.Purposes?.Length < allPurposes.Length
                || ig.Points?.Length < allPoints.Length);

        var countPurposesAppliesTo = limitPurposes?.Count ?? allPurposes.Length;
        var countPointsAppliesTo = limitPoints?.Count ?? allPoints.Length;
        
        var lessSpecificThenPrevious = alreadyHadSpecificLimitsForAPointOrPurpose
            && allIndividualGroups.Any(
                ig => countPurposesAppliesTo > ig.Purposes?.Length
                    || countPointsAppliesTo > ig.Points?.Length);
        
        var meetsAggregateConditions = 
            (textSuggestsIsAggregate
            && (thisLimitedByPoints
                || thisLimitedByPurpose
                || (containsUnderThisLicenceText && lessSpecificThenPrevious)))
            || (multiplePurposesSpecified && othersLimitedByPurpose);

        // We are limited by points or purposes - its an aggregate
        if (!meetsAggregateConditions)
        {
            if (limitPoints?.Count > 1 && limitPoints.Count < allPoints.Length)
            {
                meetsAggregateConditions = true;
            }
            
            if (limitPurposes?.Count > 1 && limitPurposes.Count < allPurposes.Length)
            {
                meetsAggregateConditions = true;
            }
        }

        var linkedLicenceNumbers1 = siblings
            .Where(sibling => sibling.MatchedLabelName == "LinkedLicenceNumber")
            .ToList();

        var linkedLicenceNumbers = new List<LinkedLicence>();

        foreach (var linkedLicenceNumber in linkedLicenceNumbers1)
        {
            var condition = (Condition?)null; // TODO

            var scrapedLicenceNumber = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;

            var naldLicenceNumber =
                (string?)JsonHelper.CastFromJsonTypeToNative(linkedLicenceNumber.Text?.FirstOrDefault()?.AdditionalData?["NaldLicenceNumber"]) ??
                null;

            var dmsFileDataTask = FormattingHelper.GetDmsFileDataAsync(
                scrapedLicenceNumber,
                lookupConfiguration.CacheService);
            
            var naldDataLine2 = await Core.AbstractionLicence.Helpers.FormattingHelper.GetNaldDataLineAsync(
                cacheService,
                naldLicenceNumber,
                regionCode);

            var (naldStatus, licenceType) = GetLicenceStatusAndType(naldDataLine2);
            var dmsFileData = await dmsFileDataTask;
            
            linkedLicenceNumbers.Add(new LinkedLicence
            {
                LicenceNumber = naldDataLine2?.LicenceNumber ?? scrapedLicenceNumber,
                RegionId = naldDataLine?.FgacRegionCode ?? regionCode,
                RawScrapedLicenceNumber = scrapedLicenceNumber,
                DmsPermitNumber = dmsFileData?.PermitNumber,
                DmsPath = dmsFileData?.DmsPath,
                Filename = dmsFileData?.DestinationFileName,
                NaldStatus = naldStatus,
                LicenceType = licenceType,
                Condition = condition,
                IsBecauseOfAggregate = meetsAggregateConditions,
                ContainedIn =
                [
                    new ContainedInInformation
                    {
                        Source = InformationSource.Document,
                        Direction = InformationDirection.Outgoing,
                        SectionName = sectionName,
                        LinkReason = GetLinkReason([abstractionLimitPointSub],
                            linkedLicenceNumber.Text?.FirstOrDefault()?.Text),
                        LineNumber = linkedLicenceNumber.LabelStartLineNumber,
                        PageNumber = linkedLicenceNumber.LabelStartPageNumber
                    }
                ]
            });
        }
        
        linkedLicenceNumbers = linkedLicenceNumbers
            .Where(linkedLicence =>
                FormattingHelper.IsValidLicenceNumber(
                    linkedLicence.LicenceNumber!,
                    regionCode) != false)
            .Where(linkedLicence =>
                !LicenceNumberContainsOther(
                    licenceNumber,
                    linkedLicence.LicenceNumber,
                    regionCode))
            .ToList();

        const string shallNotExceedTheQuantityAuthorisedToBeAbstractedUnderThisLicence
            = "shall not exceed the quantity authorised to be abstracted under this licence";
        
        var shouldCopyIndividualValues = abstractionLimitPointSubText.Contains(
            shallNotExceedTheQuantityAuthorisedToBeAbstractedUnderThisLicence
            , StringComparison.OrdinalIgnoreCase);

        if (shouldCopyIndividualValues)
        {
            allAggregates.AddRange(
                previouslyFoundIndividualLimits
                    .Select(previouslyFoundIndividualLimit =>
                        new Aggregate
                        {
                            PrimaryType = PrimaryType.LicenceToLicence,
                            LinkedLicences = linkedLicenceNumbers
                                .Select(ll => ll.LicenceNumber!)
                                .ToArray(),
                            SourceLicenceNumber = licenceNumber,
                            SourceLicenceVersionId = licenceVersionId,
                            NaldType = GetNaldType(naldDataLine),
                            AggregateSetId = PositionConstants.ReplacementMarker,
                            Limits = previouslyFoundIndividualLimit.Limits
                                .Select(l => l.Clone())
                                .ToList(),
                            Points = previouslyFoundIndividualLimit.Points,
                            Purposes = previouslyFoundIndividualLimit.Purposes,
                            ContainedIn = previouslyFoundIndividualLimit.ContainedIn,
                            IsExplicitlyAggregate = true
                        }
                    )
            );

            return;
        }
        
        var abstractionLinkedLicences = linkedLicenceNumbers
            .Where(lln => lln.ContainedIn?.Any(ci => !IsExcludedLinkReason(ci.LinkReason)) == true)
            .ToList();
        
        var hasLinkedLicenceNumber = abstractionLinkedLicences.Count > 0;
        var isAggregate = hasLinkedLicenceNumber || meetsAggregateConditions;

        if (isAggregate)
        {
            foreach (var linkedLicenceNumber in linkedLicenceNumbers)
            {
                linkedLicenceNumber.IsBecauseOfAggregate = true;
            }
        }

        // If points is null, then get all the points from the document implicitly
        if (limitPoints == null || limitPoints.Count == 0)
        {
            limitPoints = allPoints
                .Select(p => new Point
                {
                    Id = p.Id,
                    Description = !string.IsNullOrEmpty(p.Id) ? null : p.Description,
                    IsImplicit = true
                })
                .ToList();
        }
        
        // If purposes is null, then get all the purposes from the document implicitly
        if (limitPurposes == null || limitPurposes.Count == 0)
        {
            limitPurposes = allPurposes
                .Select(p => new Purpose
                {
                    Id = p.Id,
                    Description = !string.IsNullOrEmpty(p.Id) ? null : p.Description,
                    IsImplicit = true
                })
                .ToList();
        }
        
        if (timeCutoff != null && !isAggregate)
        {
            individualGroups.Add(new AbstractionLimitGroup
            {
                TimeCutoff = timeCutoff,
                DocumentIdentifier = documentIdentifier,
                ContainedIn = containedIn,
                Limits = [],
                Points = limitPoints.ToArray(),
                Purposes = limitPurposes.ToArray()
            });
        }
        else if (datePurposesTimePeriods.Count >= 1)
        {
            individualGroups.Add(new AbstractionLimitGroup
            {
                Limits = [],
                Points = limitPoints.ToArray(),
                Purposes = limitPurposes.ToArray(),
                DocumentIdentifier = documentIdentifier,
                ContainedIn = containedIn
            });

            foreach (var datePurpose in datePurposesTimePeriods)
            {
                individualGroups.Add(new AbstractionLimitGroup
                {
                    TimePeriod = GetTimePeriod(datePurpose),
                    DocumentIdentifier = documentIdentifier,
                    ContainedIn = containedIn,
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
                ContainedIn = containedIn,
                Points = limitPoints.ToArray(),
                Purposes = limitPurposes.ToArray()
            });
        }
        else if (individualGroups.Count == 0)
        {
            individualGroups.Add(allIndividualGroups[0]);
        }

        var isExcludedLinkReason = IsExcludedLinkReason(linkReason);
        
        var relatedNamesDict = new Dictionary<string, int>();
        var aggregateAbstractionLimits = new List<AbstractionLimit>();

        var newValueResults = new List<LabelGroupResult>();

        if (!isExcludedLinkReason)
        {
            // Work out the best match when a value found for multiple lines
            foreach (var valueResult in valueResults)
            {
                var allDuplicates = valueResults
                    .Where(vr => vr.Text?.FirstOrDefault()?.Text == valueResult.Text?.FirstOrDefault()?.Text
                                 && vr.LabelStartPageNumber == valueResult.LabelStartPageNumber
                                 && vr.LabelStartLineNumber == valueResult.LabelStartLineNumber)
                    .Select(vr => (vr, siblings.FirstOrDefault(sibling =>
                        sibling.MatchedLabelName == vr.MatchedLabelRelatedName)))
                    .ToList();

                var bestResult = allDuplicates
                    .OrderBy(vrg => vrg.Item2?.LabelStartLineNumber == vrg.vr.LabelStartLineNumber ? 0 : 1)
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

                if (!relatedNamesDict.TryAdd(valueResult.MatchedLabelRelatedName!, 0))
                {
                    relatedNamesDict[valueResult.MatchedLabelRelatedName!] += 1;
                }

                var allUnits = siblings?
                    .Where(sibling =>
                        sibling.MatchedLabelName == valueResult.MatchedLabelRelatedName)
                    .ToList();

                var unitPosition = relatedNamesDict[valueResult.MatchedLabelRelatedName!];

                var units = allUnits!.Count > unitPosition
                    ? allUnits[unitPosition]
                        .Text?
                        .FirstOrDefault()?
                        .Text
                    : null;

                // We can't give a value that has no units (this fixes an issue on 12100068)
                if (string.IsNullOrEmpty(units))
                {
                    continue;
                }
                
                var text = valueResult.MatchedLabelTextFirstLine;

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

                var groupPointsStr = individualGroup.Points?.Count(p => p.IsImplicit != true) > 0
                    ? string.Join(',', individualGroup.Points.Select(p => p.Id))
                    : string.Empty;

                var limitPointsStr = abstractionLimit.Points?.Count(p => p.IsImplicit != true) > 0
                    ? string.Join(',', abstractionLimit.Points.Select(p => p.Id))
                    : string.Empty;

                var groupPurposesStr = individualGroup.Purposes?.Count(p => p.IsImplicit != true) > 0
                    ? string.Join(',', individualGroup.Purposes.Select(p => p.Id))
                    : string.Empty;

                var limitPurposesStr = abstractionLimit.Purposes?.Count(p => p.IsImplicit != true) > 0
                    ? string.Join(',', abstractionLimit.Purposes.Select(p => p.Id))
                    : string.Empty;

                if (individualGroup.Limits.Count > 0
                    && (groupPointsStr != limitPointsStr || groupPurposesStr != limitPurposesStr))
                {
                    individualGroup = individualGroups.FirstOrDefault(ig =>
                    {
                        groupPointsStr = ig.Points?.Count(p => p.IsImplicit != true) > 0
                            ? string.Join(',', ig.Points.Select(p => p.Id))
                            : string.Empty;

                        groupPurposesStr = ig.Purposes?.Count(p => p.IsImplicit != true) > 0
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
                            ContainedIn = containedIn
                        };

                        individualGroups.Add(individualGroup);
                    }
                }

                individualGroup.Limits.Add(abstractionLimit);
            }
        }

        var notIncludedList = new List<AbstractionLimitGroup>();

        foreach (var individualGroup in individualGroups)
        {
            if (allIndividualGroups.Contains(individualGroup))
            {
                continue;
            }
            
            notIncludedList.Add(individualGroup);
        }

        if (!isExcludedLinkReason)
        {
            allIndividualGroups.AddRange(
                notIncludedList.Where(grp => grp.Limits.Count > 0));
        }

        if (aggregateAbstractionLimits.Count == 0)
        {
            foreach (var linkedLicenceNumber in linkedLicenceNumbers)
            {
                linkedLicenceNumber.IsBecauseOfAggregate = false;
            }
            
            sectionLinkedLicences.AddRange(linkedLicenceNumbers);
            return;
        }

        const string noneDigitAggregateKey = "Plus a quantity equal to the";
        var containsVagueValue = abstractionLimitPointSubText.Contains(
            noneDigitAggregateKey,
            StringComparison.OrdinalIgnoreCase);

        if (containsVagueValue)
        {
            var abstractionLimitPointSubTemp = abstractionLimitPointSubText.Replace(
                noneDigitAggregateKey,
                "±",
                StringComparison.OrdinalIgnoreCase);
            
            var parts = abstractionLimitPointSubTemp.Split('±');

            if (parts.Length >= 2)
            {
                var untilDot = parts[1].Split('.')[0];
                var fullLine = $"{noneDigitAggregateKey} {untilDot}";

                foreach (var limit in aggregateAbstractionLimits)
                {
                    limit.ValueAdditionalText = fullLine;
                }
            }
        }
        
        var pointsLoop = aggregateAbstractionLimits.First().Points;
        var purposesLoop = aggregateAbstractionLimits.First().Purposes;
        var timePeriod = GetTimePeriod(
            siblings?.FirstOrDefault(s => s.MatchedLabelName == "DateOnly"));

        var aggregate = new Aggregate
        {
            SourceLicenceNumber = licenceNumber,
            SourceLicenceVersionId = licenceVersionId,
            PrimaryType = abstractionLinkedLicences.Count >= 1
                ? PrimaryType.LicenceToLicence
                : PrimaryType.InLicence,
            NaldType = GetNaldType(naldDataLine),
            AggregateSetId = PositionConstants.ReplacementMarker,
            LinkedLicences = abstractionLinkedLicences.Count > 0
                ? abstractionLinkedLicences.Select(lln => lln.LicenceNumber!).ToArray()
                : null,
            Limits = aggregateAbstractionLimits,
            Points = pointsLoop?.ToArray() ?? [],
            Purposes = purposesLoop?.ToArray() ?? [],
            TimeCutoff = timeCutoff,
            TimePeriod = timePeriod,
            DocumentIdentifier = documentIdentifier,
            ContainedIn = containedIn
        };

        var aggregatePointsLength = aggregate.Points.Count(p => p.IsImplicit != true);
        
        // If there are no points, purposes or licences specified, then it
        // must mean it's relevant to all points and purposes
        if (aggregatePointsLength == 0
            && aggregate.Purposes.Length == 0
            && abstractionLinkedLicences.Count == 0)
        {
            aggregate.Points = allPoints.Select(Point (p) => p).ToArray();
            aggregate.Purposes = allPurposes.Select(Purpose (p) => p).ToArray();
        }

        if (aggregatePointsLength > 1)
        {
            aggregate.SubType = SubType.PointToPoint;
        }
        else if (aggregate.Purposes.Length > 1)
        {
            aggregate.SubType = SubType.PurposeToPurpose;
        }

        NullOutLimitLevelPointsAndPurposesIfRelevant(aggregate, aggregateAbstractionLimits);

        if (!isExcludedLinkReason)
        {
            allAggregates.Add(aggregate);
        }

        sectionLinkedLicences.AddRange(linkedLicenceNumbers);
    }

    private static void NullOutLimitLevelPointsAndPurposesIfRelevant(
        AbstractionLimitGroup limitGroup,
        List<AbstractionLimit> abstractionLimits)
    {
        if (limitGroup.Purposes?.Length > 0)
        {
            foreach (var aggregateLimit in abstractionLimits)
            {
                aggregateLimit.Purposes = null;
            }
        }
        else if (limitGroup.Purposes?.Length == 0)
        {
            limitGroup.Purposes = null;
        }

        if (limitGroup.Points?.Length > 0)
        {
            foreach (var aggregateLimit in abstractionLimits)
            {
                aggregateLimit.Points = null;
            }
        }
        else if (limitGroup.Points?.Length == 0)
        {
            limitGroup.Points = null;
        }
    }
    
    private static int GetPositionRelativeToDateLines(
        List<LabelGroupResult>? dateLines,
        LabelGroupResult line)
    {
        if (dateLines == null || dateLines.Count == 0)
        {
            return 0;
        }

        if (line.MatchedLabelName == "PerYearValue")
        {
            return 0;
        }

        var match = dateLines
            .OrderBy(matchLineNumber =>
            {
                var diff = matchLineNumber.LabelStartLineNumber - line.LabelStartLineNumber;

                if (0 > diff)
                {
                    return int.MaxValue;
                }

                return diff;
            })
            .First();

        return dateLines.IndexOf(match) + 1;
    }

    private static List<Purpose>? CheckForGenericTransferPurpose(
        List<Purpose>? limitPurposes,
        string abstractionLimitPointSubText)
    {
        const string forThePurposeOfTransferGeneric = "for the purpose of transfer shall";
        
        if ((limitPurposes == null || limitPurposes.Count == 0)
            && abstractionLimitPointSubText.Contains(forThePurposeOfTransferGeneric,
                StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new Purpose
                {
                    Id = "Transfer",
                    Description = "Transfer",
                    IsImplicit = false
                }
            ];
        }

        return limitPurposes;
    }
    
    private static bool IsExcludedLinkReason(string? linkReason)
    {
        return linkReason is LinkReason.SimultaneousDischargeCondition
            or LinkReason.CompensationFlow;
    }

    private static TimePeriod? GetDefinitionOfYear(List<LabelGroupResult> matches)
    {
        var abstractionLimitsSection = matches
            .FirstOrDefault(result => result.LabelGroupName == DocumentSectionNames.AbstractionLimits);

        var abstractionLimitPoints = abstractionLimitsSection?
            .SubResults
            .Where(res => res.MatchedLabelName == "AbstractionLimitPoint")
            .ToList();

        var abstractionLimitPointSubs = abstractionLimitPoints?
            .SelectMany(res => res.SubResults)
            .Where(res => res.MatchedLabelName == "AbstractionLimitPointSub")
            .ToList();

        if (abstractionLimitPointSubs != null)
        {
            foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
            {
                var definition = abstractionLimitPointSub.SubResults
                    .SingleOrDefault(sr => sr.MatchedLabelName == "AYearDefinitionLine");

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

        if (periodResults.MatchedLabelName == "DuringTheMonthsXToYOnlyText")
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
                .FirstOrDefault(x => x.MatchedLabelName == "PeriodPeriodNumber");

            var textWithoutNumber = pointResult.SubResults
                .FirstOrDefault(x => x.MatchedLabelName == "PeriodTextWithoutPurposeAndPoint")?
                .Text?
                .Select(t => t.Text)
                .ToList();

            if (textWithoutNumber == null && periodPeriodNumber == null)
            {
                continue;
            }

            var tKey = "Up to and Including ";

            var allTextWithoutNumber = textWithoutNumber?
                .Where(t => !t.StartsWith(tKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (allTextWithoutNumber == null)
            {
                continue;
            }

            var upToAndIncludeLine = textWithoutNumber?
                .FirstOrDefault(t => t.StartsWith(tKey, StringComparison.OrdinalIgnoreCase));
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
                StringComparison.OrdinalIgnoreCase) ?? false;

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

        if (meansResult.Confidence != null)
        {
            noneSchemaData.Add("Confidence:MeansOfAbstraction", meansResult.Confidence);
        }

        foreach (var meanResult in meansResult.SubResults)
        {
            var textWithoutNumber = meanResult.SubResults
                .FirstOrDefault(subResult => subResult.MatchedLabelName == "TextWithoutNumber")?
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

            if (description?.Contains("second", StringComparison.OrdinalIgnoreCase) == true)
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
        string sectionName,
        List<LabelGroupResult> matches,
        NaldData? naldDataLine,
        ref Dictionary<string, object?> noneSchemaData)
    {
        noneSchemaData.Add($"Nald{sectionName}Data", naldDataLine?.Points ?? []);
        
        var pointsResults = DataHelper.GetFirstMatchByLabelGroup(matches, sectionName);
        var returnList = new List<PointOfAbstraction>();

        if (pointsResults == null)
        {
            return returnList.ToArray();
        }

        if (pointsResults.Confidence != null)
        {
            noneSchemaData.Add($"Confidence:{sectionName}", pointsResults.Confidence);
        }

        List<LabelGroupResult> pointPurposeGroups;
        
        var pointPurposeGroupSingleLinePerItem = pointsResults.SubResults
            .Where(sr => sr.MatchedLabelName == "PointPurposeGroupSingleLinePerItem")
            .ToList();

        if (pointPurposeGroupSingleLinePerItem.Count > 1)
        {
            pointPurposeGroups = pointPurposeGroupSingleLinePerItem;
        }
        else
        {
            pointPurposeGroups = pointsResults.SubResults
                .Where(sr => sr.MatchedLabelName == "PointPurposeGroup")
                .ToList();
        }
        
        var anyNoneEmptyStringMatches = pointPurposeGroups
            .Any(ppg => ppg.MatchedLabelTextFirstLine != string.Empty);

        if (anyNoneEmptyStringMatches)
        {
            pointPurposeGroups = pointPurposeGroups
                .Where(ppg => ppg.MatchedLabelTextFirstLine != string.Empty)
                .ToList();
        }
        
        var pointPurposeGroupCount = -1;
        
        foreach (var pointPurposeGroup in pointPurposeGroups)
        {
            if (pointPurposeGroup.Confidence != null)
            {
                noneSchemaData.Add(
                    $"Confidence:{sectionName}_PointPurposeGroup_{++pointPurposeGroupCount}",
                    pointPurposeGroup.Confidence);
            }

            var purposeGroupName = DataHelper.GetFirstMatchByLabel(
                pointPurposeGroup.SubResults,
                "PurposeGroupName");

            var purposeIds = purposeGroupName?.SubResults
                .Where(x => x.MatchedLabelName == "PurposeGroupSub")
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

                if (pointNumber != null && point.Confidence != null)
                {
                    noneSchemaData.Add(
                        $"Confidence:{sectionName}_PointPurposeGroup_{pointPurposeGroupCount}_Point_{pointCount++}_PointPointNumber",
                        point.Confidence);
                }

                var pointTextWithoutPurposeAndPoint = point.SubResults
                    .FirstOrDefault(x => x.MatchedLabelName == "PointTextWithoutPurposeAndPoint");
                
                var tLines = pointTextWithoutPurposeAndPoint?
                    .Text?
                    .Select(t => t.Text)
                    .ToList();

                const string tKey = "Up to and Including ";
                var upToAndIncludeLine = tLines?
                    .FirstOrDefault(t => t.StartsWith(tKey, StringComparison.OrdinalIgnoreCase));

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

                // 63347S0172R01 has one
                var fromToPointTable = DataHelper.GetFirstMatchByLabel(point.SubResults, "FromToPointTable");

                if (noneSchemaData.ContainsKey(TemplateFeatures.FromToPointsTable))
                {
                    if (fromToPointTable != null)
                    {
                        noneSchemaData[TemplateFeatures.FromToPointsTable] = true;
                    }
                }
                else
                {
                    noneSchemaData.Add(TemplateFeatures.FromToPointsTable, fromToPointTable != null);
                }

                if (fromToPointTable != null)
                {
                    if (fromToPointTable.Confidence != null)
                    {
                        noneSchemaData.Add(
                            $"Confidence:{sectionName}_PointPurposeGroup_{pointPurposeGroupCount}_Point_{pointCount}_FromToPointTable",
                            fromToPointTable.Confidence);
                    }

                    var tableLines = fromToPointTable.Text!;

                    foreach (var tableLine in tableLines)
                    {
                        var id = $"{tableLine.Columns[0].Text} to {tableLine.Columns[1].Text}";

                        var containedInList1 = new List<ContainedInInformation>
                        {
                            new()
                            {
                                Source = InformationSource.Document,
                                SectionName = sectionName,
                                PageNumber = tableLine.PageNumber,
                                LineNumber = tableLine.LineNumber
                            }
                        };

                        var naldData2 = GetNaldPointData(naldDataLine, tableLine.Text);

                        if (naldData2 != null)
                        {
                            containedInList1.Add(new ContainedInInformation
                            {
                                Source = InformationSource.Nald
                            });
                        }
                        
                        returnList.Add(
                            new PointOfAbstraction
                            {
                                Description = $"From {id}",
                                Id = $"{pointNumber} {id}", // e.g 2.1 - From TL123 to TL456
                                AltId = id,
                                PurposeIds = purposeIds,
                                TimeCutoff = timeCutoff,
                                
                                ContainedIn = containedInList1.ToArray()
                            });
                        // Format is 'Abstraction National Grid Location Description Map'
                    }

                    continue;
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
                    if (pointTable.Confidence != null)
                    {
                        noneSchemaData.Add(
                            $"Confidence:{sectionName}_PointPurposeGroup_{pointPurposeGroupCount}_Point_{pointCount}_PointTable",
                            pointTable.Confidence);
                    }

                    var tableLines = pointTable.Text!;

                    foreach (var tableLine in tableLines)
                    {
                        var words = tableLine.Text.Split(' ');
                        var subId = words[0]; // e.g. A, D, E

                        var containedInList2 = new List<ContainedInInformation>
                        {
                            new()
                            {
                                Source = InformationSource.Document,
                                SectionName = sectionName,
                                PageNumber = tableLine.PageNumber,
                                LineNumber = tableLine.LineNumber
                            }
                        };

                        var naldData1 = GetNaldPointData(naldDataLine, tableLine.Text);
                        
                        if (naldData1 != null)
                        {
                            containedInList2.Add(new ContainedInInformation
                            {
                                Source = InformationSource.Nald
                            });
                        }
                        
                        returnList.Add(
                            new PointOfAbstraction
                            {
                                Description = tableLine.Text,
                                Id = $"{pointNumber} {subId}", // e.g 2.1 - A
                                AltId = subId,
                                PurposeIds = purposeIds,
                                TimeCutoff = timeCutoff,
                                ContainedIn = containedInList2.ToArray()
                            });
                        // Format is 'Abstraction National Grid Location Description Map'
                    }

                    continue;
                }

                var allTextWithoutNumber = tLines?
                    .Where(t => !t.StartsWith(tKey, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (allTextWithoutNumber == null)
                {
                    continue;
                }

                var description = string.Join(' ', allTextWithoutNumber);
                description = FormattingHelper.TrimFormatting(description, false, true);
                
                var (name, gridRef, letterId) = GetPointNameGridRefLetterId(description);

                var naldData = GetNaldPointData(naldDataLine, description);

                var containedInList = new List<ContainedInInformation>
                {
                    new()
                    {
                        Source = InformationSource.Document,
                        SectionName = sectionName,
                        PageNumber = point.LabelStartPageNumber,
                        LineNumber = point.LabelStartLineNumber
                    }
                };

                if (naldData != null)
                {
                    containedInList.Add(new ContainedInInformation
                    {
                        Source = InformationSource.Nald
                    });
                }
                
                returnList.Add(
                    new PointOfAbstraction
                    {
                        Name = name,
                        NationalGridReferences = !string.IsNullOrEmpty(gridRef)
                            ? [GetGridReference(gridRef)!]
                            : null,
                        Description = description,
                        Id = pointNumber,
                        AltId = letterId,
                        PurposeIds = purposeIds,
                        TimeCutoff = timeCutoff,
                        ContainedIn = containedInList.ToArray()
                    });
            }
        }

        foreach (var item in returnList)
        {
            if (string.IsNullOrEmpty(item.Id) && !string.IsNullOrEmpty(item.Name))
            {
                item.Id = item.Name;
            }
        }
        
        return returnList.ToArray();
    }

    private static NationalGridReference? GetGridReference(string? gridRef)
    {
        if (string.IsNullOrEmpty(gridRef))
        {
            return null;
        }

        var gridRefNoLetters = gridRef.Replace(" ", string.Empty);

        if (gridRefNoLetters.Length == 10)
        {
            return Get10LetterGridRef(gridRefNoLetters);
        }
        
        return gridRefNoLetters.Length != 12 ? null : Get12LetterGridRef(gridRefNoLetters);
    }

    private static NationalGridReference Get10LetterGridRef(string gridRefNoLetters)
    {
        var letters = gridRefNoLetters[..2];
        var east = gridRefNoLetters.Substring(2, 4);
        var north = gridRefNoLetters.Substring(6, 4);

        return new NationalGridReference
        {
            Sheet = letters,
            East = east,
            North = north
        };
    }
    
    private static NationalGridReference Get12LetterGridRef(string gridRefNoLetters)
    {
        var letters = gridRefNoLetters[..2];
        var east = gridRefNoLetters.Substring(2, 5);
        var north = gridRefNoLetters.Substring(7, 5);

        return new NationalGridReference
        {
            Sheet = letters,
            East = east,
            North = north
        };
    }
    
    private static (string? Name, string? GridRef, string? LetterId) GetPointNameGridRefLetterId(
        string? description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return (null, null, null);
        }
        
        // If its like 'A SE' or 'B NE' get rid of the A and B
        if (description.Length > 2 && char.IsAsciiLetterUpper(description[0]) && description[1] == ' ')
        {
            description = description[2..];
        }

        var parts = description.Split(" at ");
        var name = parts[0];
        var gridRef = parts.Length >= 2 ? parts[1] : null;

        if (parts.Length == 1 && description.Contains("National Grid Reference"))
        {
            parts = description.Split("National Grid Reference");

            name = null;
            gridRef = parts[1].Trim();
        }

        if (gridRef?.Contains(" marked") == true)
        {
            parts = gridRef.Split(" marked");
            gridRef = parts[0];
        }
        
        gridRef = gridRef?.Replace("point ", string.Empty);

        var letterIdParts = description.Replace("\"", "'").Split("marked '");
        var letterId = letterIdParts.Length >= 2 ? letterIdParts[1].Replace("\"", "'").Split('\'')[0] : null;

        if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(letterId))
        {
            name = letterId;
        }

        return (name, gridRef, letterId);
    }

    private static NaldPointData? GetNaldPointData(NaldData? naldDataLine, string? description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return null;
        }
        
        if (naldDataLine?.Points.Count is null or 0)
        {
            return null;
        }

        var naldPoints = naldDataLine.Points;
        NaldDataPoint? point;

        // There is only one, must be this one
        if (naldPoints.Count == 1)
        {
            point = naldPoints[0];
        }
        else
        {
            var relevantDescription = description.Split(" at ")[0];
            
            point = naldPoints
                .FirstOrDefault(p =>
                    p.PointName?.Contains(relevantDescription, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (point is null)
        {
            return null;
        }

        return new NaldPointData
        {
            Id = point.PointId.ToString(),
            Name = point.PointName,
            NationalGridReferences = point.NationalGridReferences.Select(n =>
                new NationalGridReference
                {
                    ReferenceIndex = n.ReferenceIndex,
                    Sheet = n.Sheet,
                    East = n.East,
                    North = n.North
                }).ToList(),
            CartesianReferences = point.CartesianReferences.Select(c =>
                new CartesianReference
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

        var naldPurposes = naldDataLine.Purposes;
        NaldDataPurpose purpose;

        // There is only one, so must be that
        if (naldPurposes.Count == 1)
        {
            purpose = naldPurposes[0];
        }
        else
        {
            var relevantDescription = description?.Split(" at ")[0];
            
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
        
        var purposeResults = matches.FirstOrDefault(result => result.LabelGroupName == "Purposes");
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
                .FirstOrDefault(x => x.MatchedLabelName == "PointGroupName");

            var pointIds = pointGroupName?.SubResults
                .Where(x => x.MatchedLabelName == "PointGroupSub")
                .Select(x => x.Text?.FirstOrDefault()?.Text)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToArray();

            var purposes = purposePointGroup.SubResults
                .Where(x => x.MatchedLabelName == "Purposes")
                .ToList();

            foreach (var purpose in purposes)
            {
                pointCount += 1;
                
                var purposeNumber = purpose.SubResults
                    .FirstOrDefault(x => x.MatchedLabelName == "PurposeNumber");

                var pointTextWithoutPurposeAndPoint = purpose.SubResults
                    .FirstOrDefault(x => x.MatchedLabelName == "TextWithoutPoints");
                
                var tLines = pointTextWithoutPurposeAndPoint?
                    .Text?
                    .Select(t => t.Text)
                    .ToArray();

                if (pointTextWithoutPurposeAndPoint is { Confidence: not null })
                {
                    noneSchemaData.Add(
                        $"Confidence:Points_PointPurposeGroup_{pointPurposeGroupCount}_Point_{pointCount}_PointTextWithoutPurposeAndPoint",
                        pointTextWithoutPurposeAndPoint.Confidence);
                }
                
                var tKey = "Up to and Including ";

                var allTextWithoutNumber = tLines?
                    .Where(t => !t.StartsWith(tKey, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (allTextWithoutNumber == null && purposeNumber == null)
                {
                    continue;
                }

                var upToAndIncludeLine = tLines?
                    .FirstOrDefault(t => t.StartsWith(tKey, StringComparison.OrdinalIgnoreCase));
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
            "aggregate annual abstraction" => LimitPeriodType.PerYear,            
            "in total" => LimitPeriodType.InTotal,
            "total annual quantity" => LimitPeriodType.InTotal,
            "consecutive five year" => LimitPeriodType.Per5Years,
            "five consecutive years" => LimitPeriodType.Per5Years,
            "over any 5-year period" => LimitPeriodType.Per5Years,            
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

    public static async Task<List<LicenceSet>> AddAdditionalLicenceSetsAsync(
        List<IReadOnlyList<LicenceSet>> licenceSetGroups,
        LookupConfiguration lookupConfiguration,
        IAbstractionLicenceCacheService cacheService)
    {
        var distinctLicenceSets = AsDistinctLicenceSets(licenceSetGroups);

        distinctLicenceSets.AddRange(await AddIncomingLinksAsync(
            licenceSetGroups,
            true,
            lookupConfiguration,
            cacheService));

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
                    $"WARNING - {nameof(AbstractionLicenceSchemaConverter)} - AddImplicitExplicitAndEncompassingLicenceSets - Licence doesnt have licence number set");
                
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

    private static (
        string? LicenceNumber,
        string? ScrapedLicenceNumber,
        double? Confidence,
        double? OcrConfidence,
        string Source)
        GetLicenceNumber(
            MatchesResult matchesResult,
            string? naldLicenceNumber,
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

        if (!string.IsNullOrEmpty(naldLicenceNumber))
        {
            licenceNumber = naldLicenceNumber;
            return (licenceNumber, scrapedLicenceNumber, 100.0, null, "NaldLicenceNumber");
        }
        
        string? fileNameLicenceNumber = null;
        var source = "Scraped";

        if (!string.IsNullOrEmpty(matchesResult.Filename))
        {
            var filenameParts = matchesResult.Filename!.Replace(" ", "_").Split('_');
            var licenceNumberPart = filenameParts[0];
            var isPartALicenceNumber = licenceNumberPart.Length > 5
                && !licenceNumberPart.Contains('.')
                && licenceNumberPart.Count(char.IsDigit) >= 3;

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
                        source = "Filename";
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
                source = "Filename";
            }
        }

        licenceNumber = FormattingHelper.FormatLicenceNumber(licenceNumber, matchesResult.RegionCode)?.ToUpper();
        return (licenceNumber, scrapedLicenceNumber, confidence, ocrConfidence, source);
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
                          ci.Direction == InformationDirection.Incoming) != true)
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

    public static void CalculateCombinedAggregates(List<LicenceSet> allLicenceSets)
    {
        var allLicences = allLicenceSets
            .SelectMany(ls => ls.Licences)
            .GroupBy(l => l.Id)
            .Select(l => l.First())
            .ToList();
    
        foreach (var licence in allLicences)
        {
            if (licence.AbstractionLimits.Aggregates == null)
            {
                continue;
            }

            foreach (var aggregate in licence.AbstractionLimits.Aggregates)
            {
                if (aggregate.LinkedLicences == null || aggregate.LinkedLicences.Length == 0)
                {
                    continue;
                }
                
                foreach (var limit in aggregate.Limits)
                {
                    if (string.IsNullOrEmpty(limit.ValueAdditionalText))
                    {
                        continue;
                    }

                    var otherLicence = allLicences
                        .FirstOrDefault(l => l.LicenceNumber?.Value == aggregate.LinkedLicences[0]);

                    if (otherLicence == null)
                    {
                        continue;
                    }
                
                    var otherLicenceLimits = new List<AbstractionLimit>();

                    if (otherLicence.AbstractionLimits.Aggregates != null)
                    {
                        otherLicenceLimits.AddRange(
                            otherLicence.AbstractionLimits.Aggregates!.SelectMany(a => a.Limits));
                    }
                
                    if (otherLicence.AbstractionLimits.Individual != null)
                    {
                        otherLicenceLimits.AddRange(
                            otherLicence.AbstractionLimits.Individual!.SelectMany(i => i.Limits));
                    }

                    var otherLicenceLimit = otherLicenceLimits.FirstOrDefault();
                    if (otherLicenceLimit == null)
                    {
                        continue;
                    }

                    var units1 = limit.Units;
                    var value1 = limit.Value;
                    
                    var units2 = otherLicenceLimit.Units;
                    var value2 = otherLicenceLimit.Value;
                    
                    var differentUnits = units1?.Equals(units2) != true;

                    const string cubicMetres = "cubic metres";
                    const string thousandCubicMetres = "thousand cubic metres";
                    
                    if (differentUnits)
                    {
                        switch (units1?.ToLower())
                        {
                            case thousandCubicMetres:
                                if (units2 == cubicMetres)
                                {
                                    value1 *= 1_000;
                                }
                                
                                break;
                            case cubicMetres:
                                if (units2 == thousandCubicMetres)
                                {
                                    value2 *= 1_000;                                    
                                }
                                
                                break;                            
                        }
                    }
                    
                    var combinedAmount = value1 + value2;

                    limit.Value = combinedAmount;
                    limit.ValueAdditionalText = null;
                }
            }
        }
    }
}