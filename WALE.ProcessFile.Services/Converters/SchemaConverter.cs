using WALE.ProcessFile.Core.Helpers;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Enums;

namespace WALE.ProcessFile.Services.Converters;

public static partial class SchemaConverter
{
    public static int DiffCounter;
    
    private static Licence ToLicence(
        MatchesResult matchesResult,
        HashSet<string> impoundmentLicenceNumbers,
        HashSet<string> deadLicenceNumbers,
        HashSet<string> liveLicenceNumbers)
    {
        var matches = matchesResult.Matches;

        if (matches == null)
        {
            throw new Exception("No match object exists to convert");
        }
        
        var scrapedLicenceNumber = matches
            .FirstOrDefault(result => result.LabelGroupName == "LicenceNumber")?
            .Text?
            .FirstOrDefault()?
            .Text;

        var effectiveDateStr = Formats.Date.DateFormatConsistent(matches
            .FirstOrDefault(result => result.LabelGroupName == "DateEffective")?
            .Text?
            .FirstOrDefault()?
            .Text);

        var dateOfIssueStr = Formats.Date.DateFormatConsistent(matches
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue")?
            .Text?
            .FirstOrDefault()?
            .Text);

        var dateOfOriginalIssueStr = Formats.Date.DateFormatConsistent(matches
            .FirstOrDefault(result => result.LabelGroupName == "DateOfOriginalIssue")?
            .Text?
            .FirstOrDefault()?
            .Text);

        var dateOfExpiryStr = Formats.Date.DateFormatConsistent(matches
            .FirstOrDefault(result => result.LabelGroupName == "DateOfExpiry")?
            .Text?
            .FirstOrDefault()?
            .Text);

        var expiryDate = DateTime.TryParse(dateOfExpiryStr, out var dateOfExpiryOut)
            ? dateOfExpiryOut
            : (DateTime?)null;
        
        var effectiveDate = DateTime.TryParse(effectiveDateStr, out var effectiveDateOut)
            ? effectiveDateOut
            : (DateTime?)null;
        
        var dateOfIssue = DateTime.TryParse(dateOfIssueStr, out var dateOfIssueOut)
            ? dateOfIssueOut
            : (DateTime?)null;
        
        var dateOfOriginalIssue = DateTime.TryParse(dateOfOriginalIssueStr, out var dateOfOriginalIssueOut)
            ? dateOfOriginalIssueOut
            : (DateTime?)null;

        var issuer = matches
            .FirstOrDefault(result => result.LabelGroupName == "Issuer")?
            .Text?
            .FirstOrDefault()?
            .Text;
        
        var licenceVersion = new LicenceVersion
        {
            EffectiveDate = effectiveDate,
            ExpiryDate = expiryDate,
            IssueDate = dateOfIssue,
            Issuer = issuer,
            OriginalIssueDate = dateOfOriginalIssue
        };
        
        var means = GetMeansOfAbstraction(matches);
        var points = GetPoints(matches);
        var purposes = GetPurposes(matches);
        
        var noneSchemaData = new Dictionary<string, object>();
        
        var licenceNumber = scrapedLicenceNumber;
        
        if (!string.IsNullOrEmpty(scrapedLicenceNumber))
        {
            noneSchemaData.TryAdd("scrapedLicenceNumber", scrapedLicenceNumber);
            licenceNumber = FormattingHelper.PadLicenceNumber(scrapedLicenceNumber);
        }
        
        string? fileNameLicenceNumber = null;

        if (!string.IsNullOrEmpty(matchesResult.Filename))
        {
            var filenameParts = matchesResult.Filename!.Replace(" ", "_").Split('_');

            if (filenameParts[0].Length > 5 && !filenameParts[0].Contains('.') && filenameParts[0].Count(char.IsDigit) >= 3)
            {
                fileNameLicenceNumber = filenameParts[0].Replace("-", string.Empty);
                fileNameLicenceNumber = FormattingHelper.NoneSeperatedToNaldLicenceNumber(fileNameLicenceNumber);

                if (!string.IsNullOrEmpty(fileNameLicenceNumber))
                {
                    noneSchemaData.TryAdd("filenameLicenceNumber", fileNameLicenceNumber);

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
            && FormattingHelper.PadLicenceNumber(scrapedLicenceNumber) != fileNameLicenceNumber)
        {
            var formattedScraped = FormattingHelper.PadLicenceNumber(scrapedLicenceNumber);
            var diffCount = DifferenceCount(fileNameLicenceNumber, formattedScraped);

            if (diffCount <= 2)
            {
                licenceNumber = fileNameLicenceNumber;
                DiffCounter += 1;
            }
        }
        
        licenceNumber = FormattingHelper.PadLicenceNumber(licenceNumber);

        var (aggregates, individual) = GetAbstractionLimits(
            matches,
            licenceNumber,
            licenceVersion.LicenceVersionId,
            points,
            purposes);

        var issuedToMatch = matchesResult.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "Company");

        if (issuedToMatch != null)
        {
            var issuedToMatchType = issuedToMatch.MatchType;
            noneSchemaData.Add("issuedToMatchType", issuedToMatchType.ToString());
            
            var issuedTo = issuedToMatch
                .Text?
                .FirstOrDefault()?
                .Text;

            if (!string.IsNullOrEmpty(issuedTo))
            {
                noneSchemaData.Add("issuedTo", issuedTo);
            }

            var issuedToConfidence = issuedToMatch
                .Text?
                .FirstOrDefault()?
                .OcrConfidence;

            if (issuedToConfidence != null)
            {
                noneSchemaData.Add("issuedToConfidence", issuedToConfidence);
            }

            var issuedToMatchedLabelText = issuedToMatch.MatchedLabel?.Text?.FirstOrDefault()?.Text ?? string.Empty;
            noneSchemaData.Add("issuedToMatchedLabelText", issuedToMatchedLabelText);
            
            var issuedToMatchLabelPosition = issuedToMatch.MatchedLabel?.Position.ToString() ?? "--";
            noneSchemaData.Add("issuedToMatchLabelPosition", issuedToMatchLabelPosition);
            
            var issuedToCertainty = (int)issuedToMatchType / 100;
            noneSchemaData.Add("issuedToCertainty", issuedToCertainty);
        }

        var licenceNumberOcrConfidence = matchesResult.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "LicenceNumber")?
            .Text?
            .FirstOrDefault()?
            .OcrConfidence;
        
        if (licenceNumberOcrConfidence != null)
        {
            noneSchemaData.Add("licenceNumberConfidence", licenceNumberOcrConfidence);
        }
        
        var ocr = matchesResult.ScannedFile ? "OCR" : "NoOCR";
        noneSchemaData.Add("ocr", ocr);
        
        noneSchemaData.Add("servicesUsed", matchesResult.ServicesUsed.ToArray());

        var naldLicenceNumber = (string?)null;
        
        var isLiveLicence = liveLicenceNumbers.Count > 0 && !string.IsNullOrEmpty(licenceNumber)
            ? liveLicenceNumbers.Contains(licenceNumber)
            : (bool?)null;

        if (isLiveLicence == true)
        {
            naldLicenceNumber = licenceNumber;
        }
        
        var isDeadLicence = deadLicenceNumbers.Count > 0 && !string.IsNullOrEmpty(licenceNumber)
            ? deadLicenceNumbers.Contains(licenceNumber)
            : (bool?)null;

        if (isDeadLicence == true)
        {
            naldLicenceNumber = licenceNumber;
        }

        var isImpoundmentLicence = impoundmentLicenceNumbers.Count > 0 && !string.IsNullOrEmpty(licenceNumber)
            ? impoundmentLicenceNumbers.Contains(licenceNumber)
            : (bool?)null;

        if (isImpoundmentLicence == true)
        {
            naldLicenceNumber = licenceNumber;
        }
        
        var isFound = isDeadLicence == true
            || isImpoundmentLicence == true
            || isLiveLicence == true;
        
        
        var linkedLicences = aggregates
            .Where(x => x.LinkedLicences?.Length >= 1)
            .SelectMany(x => x.LinkedLicences!)
            .ToList();

        linkedLicences.AddRange(GetRecordsLinkedLicences(matches));
        linkedLicences.AddRange(GetFurtherConditionsLinkedLicences(matches));
        linkedLicences.AddRange(GetFurtherProvisionsLinkedLicences(matches));
        linkedLicences.AddRange(GetAdditionalInformationLinkedLicences(matches));
        linkedLicences.AddRange(GetPurposesLinkedLicences(matches));
        linkedLicences.AddRange(GetPointsLinkedLicences(matches));
        
        var licenceHistory = GetLicenceHistoryLinkedLicences(matches);
        // NOTE - We don't want to include licence history licences in our output, we just want to check against them
        
        linkedLicences = linkedLicences
            .GroupBy(linkedLicence => linkedLicence.LicenceNumber)
            .Select(linkedLicencesGroup =>
            {
                var firstLinkedLicence = linkedLicencesGroup.First();
                var fromSection = new List<LinkedLicenceSection>();

                foreach (var linkedLicence in linkedLicencesGroup)
                {
                    if (linkedLicence.ContainedIn == null)
                    {
                        continue;
                    }
                    
                    var sectionItems = linkedLicence.ContainedIn;

                    foreach (var sectionItem in sectionItems)
                    {
                        if (fromSection.Any(fs => fs.SectionName == sectionItem.SectionName))
                        {
                            continue;
                        }
                        
                        fromSection.Add(sectionItem);
                    }
                }

                var linkedLicenceNumber = FormattingHelper.PadLicenceNumber(firstLinkedLicence.LicenceNumber);

                return ToLinkedLicence(
                    linkedLicenceNumber,
                    firstLinkedLicence.Filename,
                    firstLinkedLicence.Condition,
                    fromSection.ToArray(),
                    impoundmentLicenceNumbers,
                    deadLicenceNumbers,
                    liveLicenceNumbers);
            })
            .Where(linkedLicence =>
                FormattingHelper.ToZeroFormatting(linkedLicence.LicenceNumber)
                != FormattingHelper.ToZeroFormatting(licenceNumber))
            .ToList();

        var allDocumentLinkedLicences = GetAllDocumentLinkedLicences(matches);
        var additionalLinkedLicenceCount = 1;
        
        foreach (var allDocumentLinkedLicence in allDocumentLinkedLicences)
        {
            var paddedAllDocumentLinkedLicenceNumber = FormattingHelper.PadLicenceNumber(allDocumentLinkedLicence.LicenceNumber);
            if (FormattingHelper.ToZeroFormatting(paddedAllDocumentLinkedLicenceNumber)
                == FormattingHelper.ToZeroFormatting(licenceNumber))
            {
                continue;
            }
            
            var found = linkedLicences
                .Any(linkedLicence =>
                    FormattingHelper.ToZeroFormatting(linkedLicence.LicenceNumber)
                        == FormattingHelper.ToZeroFormatting(paddedAllDocumentLinkedLicenceNumber));

            if (!found && licenceHistory.Count > 0)
            {
                found = licenceHistory
                    .Any(linkedLicence =>
                    {
                        var paddedLinkedLicenceNumber = FormattingHelper.PadLicenceNumber(linkedLicence.LicenceNumber);
                        
                        return FormattingHelper.ToZeroFormatting(paddedLinkedLicenceNumber)
                            == FormattingHelper.ToZeroFormatting(paddedAllDocumentLinkedLicenceNumber);
                    });
            }
            
            if (!found)
            {
                //linkedLicences.Add(allDocumentLinkedLicence);
                
                noneSchemaData.Add(
                    $"AdditionalLinkedLicence:{additionalLinkedLicenceCount++}",
                    allDocumentLinkedLicence);
            }
        }
        
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
        
        return new Licence
        {
            Filename = matchesResult.Filename,
            LicenceNumber = licenceNumber,
            NaldLicenceNumber = naldLicenceNumber,
            LicenceVersion = licenceVersion,
            MeansOfAbstraction = means,
            Points = points,
            Purposes = purposes,
            PeriodsOfAbstraction = GetPeriods(matches),
            DefinitionOfYear = GetDefinitionOfYear(matches),
            AbstractionLimits = limits,
            LinkedLicences = linkedLicences.ToArray(),
            NoneSchemaData = noneSchemaData,
            IsDeadLicence = isDeadLicence,
            IsImpoundmentLicence = isImpoundmentLicence,
            IsLiveLicence = isLiveLicence,
            LicenceFoundInList = isFound,
            DmsPath = null
        };
    }

    private static LinkedLicence ToLinkedLicence(
        string? linkedLicenceNumber,
        string? filename,
        Condition? condition,
        LinkedLicenceSection[] containedIn,
        HashSet<string> impoundmentLicenceNumbers,
        HashSet<string> deadLicenceNumbers,
        HashSet<string> liveLicenceNumbers)
    {
        var naldLicenceNumber = (string?)null;
        
        var isLiveLicence = liveLicenceNumbers.Count > 0 && !string.IsNullOrEmpty(linkedLicenceNumber)
            ? liveLicenceNumbers.Contains(linkedLicenceNumber)
            : (bool?)null;
        
        if (isLiveLicence == true)
        {
            naldLicenceNumber = linkedLicenceNumber;
        }
        
        var isDeadLicence = deadLicenceNumbers.Count > 0 && !string.IsNullOrEmpty(linkedLicenceNumber)
            ? deadLicenceNumbers.Contains(linkedLicenceNumber)
            : (bool?)null;
        
        if (isDeadLicence == true)
        {
            naldLicenceNumber = linkedLicenceNumber;
        }
        
        var isImpoundmentLicence = impoundmentLicenceNumbers.Count > 0 && !string.IsNullOrEmpty(linkedLicenceNumber)
            ? impoundmentLicenceNumbers.Contains(linkedLicenceNumber)
            : (bool?)null;
        
        if (isImpoundmentLicence == true)
        {
            naldLicenceNumber = linkedLicenceNumber;
        }
        
        var isFound = isDeadLicence == true
            || isImpoundmentLicence == true
            || isLiveLicence == true;
        
        return new LinkedLicence
        {
            LicenceNumber = linkedLicenceNumber,
            NaldLicenceNumber = naldLicenceNumber,
            Filename = filename,
            Condition = condition,
            ContainedIn = containedIn,
            IsDeadLicence = isDeadLicence,
            IsImpoundmentLicence = isImpoundmentLicence,
            IsLiveLicence = isLiveLicence,
            LicenceFoundInList = isFound,
            DmsPath = null
        };
    }
    
    public static async Task<List<LicenceSet>> ToLicenceSetsAsync(
        MatchesResult matchesResult,
        Dictionary<string, string> licenceNumbersMapping,
        HashSet<string> impoundmentLicenceNumbers,
        HashSet<string> deadLicenceNumbers,
        HashSet<string> liveLicenceNumbers,
        IPdfDataExtractorService pdfDataExtractorService,
        string pdfFolder,
        int processRunId)
    {
        var returnList = new List<LicenceSet>();
        
        var primaryLicence = ToLicence(
            matchesResult,
            impoundmentLicenceNumbers,
            deadLicenceNumbers,
            liveLicenceNumbers);
        
        var previouslyParsedPaths = new List<string> { matchesResult.Filename! };
        
        var linkedLicences = await GetLinkedLicencesAsync(
            matchesResult,
            primaryLicence,
            licenceNumbersMapping,
            impoundmentLicenceNumbers,
            deadLicenceNumbers,
            liveLicenceNumbers,
            pdfDataExtractorService,
            pdfFolder,
            previouslyParsedPaths,
            processRunId);
        
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
            || allLicences[0].LicenceNumber != primaryLicence.LicenceNumber;
        
        var explicitlyReferencedLicenceSet = hasExplicitlyReferencedLicenceSet ? new LicenceSet
        {
            LicenceSetTypes = [LicenceSetType.AllLicencesExplicitlyReferencedAnywhere],
            Licences = allLicences.ToArray(),
            AggregateSets = GetAggregateSets(allLicences, allLicences, true)
        } : null;

        if (explicitlyReferencedLicenceSet != null)
        {
            returnList.Add(explicitlyReferencedLicenceSet);            
        }

        var licencesReferencedInLimits = primaryLicence.LinkedLicences
            .Where(linkedLicence => linkedLicence.ContainedIn?.Any(ci => ci.SectionName == LinkedLicenceSectionNames.AbstractionLimits) == true)
            .Select(ll => ll.LicenceNumber)
            .Select(ln => allLicences.FirstOrDefault(l => l.LicenceNumber == ln))
            .Where(ln => ln != null)
            .Select(ln => ln!)
            .ToList();
        
        var licencesExplicitlyMentionedInLimits = licencesReferencedInLimits.Any();

        if (licencesExplicitlyMentionedInLimits)
        {
            licencesReferencedInLimits.Insert(0, primaryLicence);
        }

        var explicitlyReferencedLimitsLicenceSet = licencesExplicitlyMentionedInLimits ? new LicenceSet
        {
            LicenceSetTypes = [LicenceSetType.AllLicencesExplicitlyReferencedInLimits],
            Licences = licencesReferencedInLimits.ToArray(),
            AggregateSets = GetAggregateSets(licencesReferencedInLimits, allLicences)
        } : null;

        if (explicitlyReferencedLimitsLicenceSet != null)
        {
            returnList.Add(explicitlyReferencedLimitsLicenceSet);            
        }
        
        foreach (var licence in allLicences)
        {
            if (licence.AbstractionLimits.Aggregates != null)
            {
                PopulateAggregateSetIds(licence.AbstractionLimits.Aggregates, allLicences);
            }
            
            AddMissingBackLinks(
                [[explicitlyReferencedLicenceSet ?? singleLicenceOnlySet]],
                false,
                impoundmentLicenceNumbers,
                deadLicenceNumbers,
                liveLicenceNumbers);

            var newLicenceSetIds = new List<LicenceSetReference>
            {
                new()
                {
                    LicenceSetId = singleLicenceOnlySet.LicenceSetId,
                    LicenceSetType =  singleLicenceOnlySet.LicenceSetTypes[0]
                }
            };

            if (explicitlyReferencedLicenceSet != null)
            {
                newLicenceSetIds.Add(new ()
                {
                    LicenceSetId = explicitlyReferencedLicenceSet.LicenceSetId,
                    LicenceSetType =  explicitlyReferencedLicenceSet.LicenceSetTypes[0]
                });
            }

            if (explicitlyReferencedLimitsLicenceSet != null)
            {
                newLicenceSetIds.Add(new ()
                {
                    LicenceSetId = explicitlyReferencedLimitsLicenceSet.LicenceSetId,
                    LicenceSetType =  explicitlyReferencedLimitsLicenceSet.LicenceSetTypes[0]
                });
            }
            
            // Add LicenceSetIds to licence
            licence.LicenceSets = newLicenceSetIds.ToArray();
        }
        
        return returnList;
    }
    
    private static List<LicenceSet> AddMissingBackLinks(
        IReadOnlyList<IReadOnlyList<LicenceSet>> licenceSetGroups,
        bool addImplicitLicenceSet,
        HashSet<string> impoundmentLicenceNumbers,
        HashSet<string> deadLicenceNumbers,
        HashSet<string> liveLicenceNumbers)
    {
        var returnList = new List<LicenceSet>();
        
        var allLicencesInSets = licenceSetGroups
            .SelectMany(ls => ls)
            .SelectMany(ls => ls.Licences)
            .GroupBy(l => l.LicenceNumber)
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

                    var incomingLinks = GetLicencesThatReferenceLicence(allLicencesInSets, licence.LicenceNumber!);
                    var outgoingLinks = licence.LinkedLicences.Select(lll => lll.LicenceNumber!).ToList();

                    var incomingAndOutgoingLinks = new List<string>(incomingLinks.Select(l => l.LicenceNumber));
                    incomingAndOutgoingLinks.AddRange(outgoingLinks);
                    
                    //...
                    
                    foreach (var incomingLink in incomingLinks)
                    {
                        if (outgoingLinks.Contains(incomingLink.LicenceNumber)
                            || licence.LinkedLicences.Any(linkedLicence => linkedLicence.LicenceNumber == incomingLink.LicenceNumber))
                        {
                            continue;
                        }

                        // Back link is missing
                        var linkedLicencesNew = new List<LinkedLicence>(licence.LinkedLicences)
                        {
                            ToLinkedLicence(
                                incomingLink.LicenceNumber,
                                incomingLink.Filename,
                                null,
                                [new LinkedLicenceSection
                                {
                                    SectionName = LinkedLicenceSectionNames.ImplicitBackLink,
                                    LinkReason = $"Linked from {incomingLink.LicenceNumber} ({incomingLink.Filename})"
                                }],
                                impoundmentLicenceNumbers,
                                deadLicenceNumbers,
                                liveLicenceNumbers)
                        };

                        licence.LinkedLicences = linkedLicencesNew.ToArray();

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
            if (!licenceNumbers.Contains(licence.LicenceNumber))
            {
                continue;
            }
            
            returnList.Add(licence);
        }

        return returnList.ToArray();
    }

    private static List<(string LicenceNumber, string? Filename)> GetLicencesThatReferenceLicence(IEnumerable<Licence> licences, string licenceNumber)
    {
        var returnList = new List<(string, string?)>();
        
        foreach (var licence in licences)
        {
            if (licence.LicenceNumber == licenceNumber)
            {
                continue;
            }

            if (licence.LinkedLicences.Any(lll => lll.LicenceNumber == licenceNumber))
            {
                returnList.Add((licence.LicenceNumber!, licence.Filename));
            }
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
                relevantAggregates = relevantAggregates.Where(agg => agg.LinkedLicences == null
                     || agg.LinkedLicences.Length == 0
                     || agg.LinkedLicences.All(linkedLicence =>
                         licences.Any(l => l.LicenceNumber == linkedLicence.LicenceNumber))).ToArray();
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
        Dictionary<string, string> licenceNumberMapping,
        HashSet<string> impoundmentLicenceNumbers,
        HashSet<string> deadLicenceNumbers,
        HashSet<string> liveLicenceNumbers,
        IPdfDataExtractorService pdfDataExtractorService,
        string pdfFolder,
        List<string> previouslyParsedPaths,
        int processRunId)
    {
        var returnLicences = new List<Licence>();
        
        var abstractionLimits = matchesResult.Matches?
            .FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        var abstractionLimitsPoints = abstractionLimits?.SubResults;

        if (abstractionLimitsPoints == null)
        {
            return returnLicences;
        }
        
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
                    var linkedLicence = ToLicence(
                        matches,
                        impoundmentLicenceNumbers,
                        deadLicenceNumbers,
                        liveLicenceNumbers);
                        
                    returnLicences.Add(linkedLicence);   
                }
                    
                var linkedLicenceNumbers = abstractionLimitPointSub.SubResults
                    .Where(subResult =>
                        subResult.MatchedLabel!.Name == "LinkedLicenceNumber")
                    .ToList();

                foreach (var linkedLicencesNumberResult in linkedLicenceNumbers)
                {
                    var licenceNumber = linkedLicencesNumberResult.Text?.FirstOrDefault()?.Text;
                    var licenceNumberTransformed = FormattingHelper.PadLicenceNumber(licenceNumber);

                    // Don't process ones we've already found
                    if (licenceNumberTransformed == primaryLicence.LicenceNumber
                        || returnLicences.Any(licence => licence.LicenceNumber == licenceNumberTransformed))
                    {
                        continue;
                    }
                    
                    if (!licenceNumberMapping.TryGetValue(licenceNumber!, out var relatedFileName))
                    {
                        returnLicences.Add(new Licence
                        {
                            LicenceNumber = licenceNumber,
                            Status = LicenceStatus.NotFound
                        });
                        
                        continue;
                    }

                    if (!relatedFileName.Contains('/'))
                    {
                        relatedFileName = $"{pdfFolder}{relatedFileName}";
                    }
                    
                    var relatedFileMatches = await pdfDataExtractorService.GetMatchesAsync(
                        relatedFileName,
                        new LookupConfiguration(LabelConfiguration.GetLabels(), licenceNumberMapping),
                        previouslyParsedPaths,
                        processRunId);

                    var licence = ToLicence(
                        relatedFileMatches,
                        impoundmentLicenceNumbers,
                        deadLicenceNumbers,
                        liveLicenceNumbers);
                    
                    returnLicences.Add(licence);
                }
            }
        }
        
        foreach (var linkedLicence in primaryLicence.LinkedLicences)
        {
            // Already found it
            if (returnLicences.Any(returnLicence => returnLicence.LicenceNumber == linkedLicence.LicenceNumber))
            {
                continue;
            }
            
            if (!licenceNumberMapping.TryGetValue(linkedLicence.LicenceNumber!, out var relatedFileName))
            {
                returnLicences.Add(new Licence
                {
                    LicenceNumber = linkedLicence.LicenceNumber,
                    Status = LicenceStatus.NotFound
                });
                        
                continue;
            }
            
            if (!relatedFileName.Contains('/'))
            {
                relatedFileName = $"{pdfFolder}{relatedFileName}";
            }
            
            var relatedFileMatches = await pdfDataExtractorService.GetMatchesAsync(
                relatedFileName,
                new LookupConfiguration(LabelConfiguration.GetLabels(), licenceNumberMapping),
                previouslyParsedPaths,
                processRunId);
                    
            var licence = ToLicence(
                relatedFileMatches,
                impoundmentLicenceNumbers,
                deadLicenceNumbers,
                liveLicenceNumbers);
            
            returnLicences.Add(licence);
        }

        return returnLicences;
    }

    private static bool Contains4DigitWord(string? input, out int matchedYear, out int yearPosition)
    {
        matchedYear = -1;
        yearPosition = -1;

        if (input == null)
        {
            return false;
        }

        var match = FourDigitYearRegex().Match(input);
        if (!match.Success)
        {
            return false;
        }
        
        if (input.Contains(match.Value, StringComparison.InvariantCultureIgnoreCase))
        {
            matchedYear = int.Parse(match.Value);
            yearPosition = input.IndexOf(match.Value, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }

        return false;
    }
    
    private static TimePeriod? GetTimePeriod(LabelGroupResult? datePurpose)
    {
        if (datePurpose == null)
        {
            return null;
        }
        
        var value = datePurpose.Text?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        
        var parts = value
            .Replace(" and ending on ", " to ")
            .Split(" to ");
        var startDate = parts[0]
            .Replace("beginning on ", string.Empty);
        
        return new TimePeriod
        {
            StartDate = startDate,
            EndDate = parts.Length > 1 ? parts[1] : null,
            PeriodType = AbstractionPeriodType.SetPeriod,
            Inclusive = true
        };
    }

    private static List<LinkedLicence> GetAdditionalInformationLinkedLicences(List<LabelGroupResult> matches)
    {
        var additional = matches
            .FirstOrDefault(result => result.LabelGroupName == "Additional");

        if (additional == null)
        {
            return [];
        }
        
        return additional.SubResults
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "AdditionalLinkedLicenceNumber")
            .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
            .Select(linkedLicenceNumber => new LinkedLicence
            {
                LicenceNumber = linkedLicenceNumber,
                ContainedIn = [
                    new LinkedLicenceSection
                    {
                        SectionName = LinkedLicenceSectionNames.AdditionalInformation,
                        LinkReason = GetLinkReason([additional], linkedLicenceNumber)
                    }
                ]
            })
            .ToList();
    }

    private static List<LinkedLicenceWithPageNumber> GetAllDocumentLinkedLicences(List<LabelGroupResult> matches)
    {
        var generalLinkedLicenceNumbers = matches
            .Where(result => result.LabelGroupName == "LinkedLicenceNumber")
            .ToList();

        if (generalLinkedLicenceNumbers.Count == 0)
        {
            return [];
        }

        var returnList = new List<LinkedLicenceWithPageNumber>();

        foreach (var generalLinkedLicenceNumber in generalLinkedLicenceNumbers)
        {
            var linkedLicenceNumber = generalLinkedLicenceNumber.Text?.FirstOrDefault()?.Text;
            
            returnList.Add(new LinkedLicenceWithPageNumber
            {
                LicenceNumber = linkedLicenceNumber,
                ContainedIn =
                [
                    new LinkedLicenceSection
                    {
                        SectionName = LinkedLicenceSectionNames.Purposes,
                        LinkReason = GetLinkReason([generalLinkedLicenceNumber], linkedLicenceNumber)
                    }
                ],
                PageNumber = generalLinkedLicenceNumber.PageNumber
            });
        }
        
        return returnList;
    }
    
    private static List<LinkedLicence> GetLicenceHistoryLinkedLicences(List<LabelGroupResult> matches)
    {
        var licenceHistorySection = matches
            .FirstOrDefault(result => result.LabelGroupName == "LicenceHistory");

        if (licenceHistorySection == null)
        {
            return [];
        }

        var returnList = licenceHistorySection.SubResults
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "LicenceHistoryLinkedLicenceNumber")
            .Select(linkedLicenceNumber =>
            {
                var lln = linkedLicenceNumber.Text?.FirstOrDefault()?.Text;
                
                return new LinkedLicence
                {
                    LicenceNumber = lln,
                    ContainedIn =
                    [
                        new LinkedLicenceSection
                        {
                            SectionName = LinkedLicenceSectionNames.LicenceHistory,
                            LinkReason = GetLinkReason([licenceHistorySection], lln)
                        }
                    ]
                };
            })
            .ToList();
        
        return returnList;
    }
    
    private static List<LinkedLicence> GetPurposesLinkedLicences(List<LabelGroupResult> matches)
    {
        var purposeSection = matches
            .FirstOrDefault(result => result.LabelGroupName == "Purpose");

        if (purposeSection == null)
        {
            return [];
        }

        var sections = purposeSection
            .SubResults
            .Where(ps => ps.MatchedLabel?.Name == "PurposePointGroup")
            .SelectMany(ppg => ppg.SubResults.Where(ppgs => ppgs.MatchedLabel?.Name == "Purpose"))
            .ToList();

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
                    .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
                    .Select(linkedLicenceNumber => new LinkedLicence
                    {
                        LicenceNumber = linkedLicenceNumber,
                        ContainedIn = [
                            new LinkedLicenceSection
                            {
                                SectionName = LinkedLicenceSectionNames.Purposes,
                                LinkReason = GetLinkReason(sections, linkedLicenceNumber)
                            }
                        ]
                    })
                    .ToList());
            }
        }
        
        return returnList;
    }
    
    private static List<LinkedLicence> GetPointsLinkedLicences(List<LabelGroupResult> matches)
    {
        var pointsSection = matches
            .FirstOrDefault(result => result.LabelGroupName == "Points");

        if (pointsSection == null)
        {
            return [];
        }

        var sections = pointsSection
            .SubResults
            .Where(ps => ps.MatchedLabel?.Name == "PointPurposeGroup")
            .SelectMany(ppg => ppg.SubResults.Where(ppgs => ppgs.MatchedLabel?.Name == "Point"))
            .ToList();

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
                    .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
                    .Select(linkedLicenceNumber => new LinkedLicence
                    {
                        LicenceNumber = linkedLicenceNumber,
                        ContainedIn = [
                            new LinkedLicenceSection
                            {
                                SectionName = LinkedLicenceSectionNames.Purposes,
                                LinkReason = GetLinkReason(sections, linkedLicenceNumber)
                            }
                        ]
                    })
                    .ToList());
            }
        }
        
        return returnList;
    }
    
    private static List<LinkedLicence> GetRecordsLinkedLicences(List<LabelGroupResult> matches)
    {
        var records = matches
            .FirstOrDefault(result => result.LabelGroupName == "Records");

        if (records?.SubResults == null || records.SubResults.Count == 0)
        {
            return [];
        }

        var sections = records.SubResults
            .Where(sub => sub.MatchedLabel?.Name == "RecordPoint")
            .ToList();
        
        return records
            .SubResults
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "RecordsLinkedLicenceNumber")
            .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
            .Select(linkedLicenceNumber => new LinkedLicence
            {
                LicenceNumber = linkedLicenceNumber,
                ContainedIn = [
                    new LinkedLicenceSection
                    {
                        SectionName = LinkedLicenceSectionNames.Records,
                        LinkReason = GetLinkReason(
                            sections.Count > 0 ? sections : [records],
                            linkedLicenceNumber)
                    }
                ]
            })
            .ToList();
    }
    
    private static List<LinkedLicence> GetFurtherConditionsLinkedLicences(List<LabelGroupResult> matches)
    {
        var furtherConditions = matches
            .FirstOrDefault(result => result.LabelGroupName == "FurtherConditions");

        if (furtherConditions == null)
        {
            return [];
        }

        var sections = furtherConditions.SubResults
            .Where(sub => sub.MatchedLabel?.Name == "FurtherConditionsPoint")
            .ToList();
        
        return furtherConditions.SubResults
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "FCLinkedLicenceNumber")
            .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
            .Select(linkedLicenceNumber => new LinkedLicence
            {
                LicenceNumber = linkedLicenceNumber,
                ContainedIn = [new LinkedLicenceSection
                {
                    SectionName = LinkedLicenceSectionNames.FurtherConditions,
                    LinkReason = GetLinkReason(sections, linkedLicenceNumber)
                }]
            })
            .ToList();
    }
    
    private static List<LinkedLicence> GetFurtherProvisionsLinkedLicences(List<LabelGroupResult> matches)
    {
        var furtherProvisions = matches
            .FirstOrDefault(result => result.LabelGroupName == "FurtherProvisions");

        if (furtherProvisions == null)
        {
            return [];
        }
        
        return furtherProvisions.SubResults
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "FurtherProvisionsLinkedLicenceNumber")
            .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
            .Select(linkedLicenceNumber => new LinkedLicence
            {
                LicenceNumber = linkedLicenceNumber,
                ContainedIn = [new LinkedLicenceSection
                {
                    SectionName = LinkedLicenceSectionNames.FurtherProvisions,
                    LinkReason = GetLinkReason([furtherProvisions], linkedLicenceNumber)
                }]
            })
            .ToList();
    }
    
    private static string? GetLinkReason(List<LabelGroupResult> sections, string? linkedLicenceNumber)
    {
        foreach (var section in sections)
        {
            var text = string.Join('\n', section.Text!.Select(t => t.Text));

            if (string.IsNullOrEmpty(linkedLicenceNumber) || !text.Contains(linkedLicenceNumber))
            {
                continue;
            }
            
            // TODO split down Additional information by heading to get this level of reasoning
            /*if (text.Contains("lapsed licence", StringComparison.InvariantCultureIgnoreCase))
            {
                return "LapsedLicence";
            }*/
            
            if (text.Contains("discharge and re-abstraction", StringComparison.InvariantCultureIgnoreCase))
            {
                return "DischargeAndReabstractionCondition";
            }
            
            if (text.Contains("simultaneous discharge", StringComparison.InvariantCultureIgnoreCase))
            {
                return "SimultaneousDischargeCondition";
            }
            
            if (text.Contains("simultaneous abstraction", StringComparison.InvariantCultureIgnoreCase))
            {
                return "SimultaneousAbstractionCondition";
            }
            
            if (text.Contains("simultaneous compensatory discharge", StringComparison.InvariantCultureIgnoreCase))
            {
                return "SimultaneousCompensatoryDischargeCondition";
            }
            
            if (text.Contains("compensatory discharge", StringComparison.InvariantCultureIgnoreCase))
            {
                return "CompensatoryDischargeCondition";
            }
            
            if (text.Contains("read in conjunction", StringComparison.InvariantCultureIgnoreCase))
            {
                return "ReadInConjunction";
            }
            
            if (text.Contains("The donor licence was", StringComparison.InvariantCultureIgnoreCase))
            {
                return "DonorLicence";
            }
            
            if (text.Contains("used in conjunction", StringComparison.InvariantCultureIgnoreCase)
                || text.Contains("use in conjunction", StringComparison.InvariantCultureIgnoreCase)) // misspelling
            {
                return "UsedInConjunction";
            }
            
            if (text.Contains("aggregate conditions", StringComparison.InvariantCultureIgnoreCase))
            {
                return "AggregateConditions";
            }
            
            if (text.Contains("emergency circumstances", StringComparison.InvariantCultureIgnoreCase))
            {
                return "EmergencyCircumstances";
            }
            
            if (text.Contains("Dewatering Discharge", StringComparison.InvariantCultureIgnoreCase))
            {
                return "DewateringDischargeCondition";
            }
            
            if (text.Contains("when added to", StringComparison.InvariantCultureIgnoreCase))
            {
                return "WhenAddedTo";
            }
            
            if (text.Contains("subsequent abstraction", StringComparison.InvariantCultureIgnoreCase))
            {
                return "SubsequentAbstraction";
            }
            
            if (text.Contains("readings", StringComparison.InvariantCultureIgnoreCase)
                && text.Contains("discharged", StringComparison.InvariantCultureIgnoreCase)
                && text.Contains("augmentation", StringComparison.InvariantCultureIgnoreCase))
            {
                return "ReadingsDischargedAugmentationCondition";
            }
            
            if (text.Contains("aggregate", StringComparison.InvariantCultureIgnoreCase))
            {
                return "AggregateCondition";
            }
        }

        return null;
    }
    
    private static (Aggregate[] aggregates, AbstractionLimitGroup[] indiviudal) GetAbstractionLimits(
        List<LabelGroupResult> matches,
        string? licenceNumber,
        string? licenceVersionId,
        PointOfAbstraction[] allPoints,
        PurposeOfAbstraction[] allPurposes)
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

            if (limitPointTable != null)
            {
                var tableLines = limitPointTable.Text!;

                foreach (var tableLine in tableLines)
                {
                    var words = tableLine.Text.Split(' ');
                    var abstractionPoint = words[0];
                    var hourlyQuantity = double.Parse(words[1]);
                    var dailyQuantity = double.Parse(words[2]);
                    var yearlyQuantity = double.Parse(words[3]);
                    var instantRate = double.Parse(words[4]);

                    var lineAbstractionLimitGroup = new AbstractionLimitGroup
                    {
                        Limits =
                        [
                            new()
                            {
                                Value = hourlyQuantity,
                                PeriodType = LimitPeriodType.PerHour,
                                Units = "cubic metres",
                                Points = [new() { Id = abstractionPoint }]
                            },
                            new()
                            {
                                Value = dailyQuantity,
                                PeriodType = LimitPeriodType.PerDay,
                                Units = "cubic metres",
                                Points = [new() { Id = abstractionPoint }]
                            },
                            new()
                            {
                                Value = yearlyQuantity,
                                PeriodType = LimitPeriodType.PerYear,
                                Units = "cubic metres",
                                Points = [new() { Id = abstractionPoint }]
                            },
                            new()
                            {
                                Value = instantRate,
                                PeriodType = LimitPeriodType.PerSecond,
                                Units = "litres",
                                Points = [new() { Id = abstractionPoint }]
                            }
                        ]
                    };
                    
                    individualGroups.Add(lineAbstractionLimitGroup);
                }
                
                allIndividualGroups.AddRange(individualGroups);
                continue;
            }

            var textSuggestsIsAggregate = abstractionLimitPointSub.Text?
                .Any(t => t.Text.Contains("The aggregate quantity")) == true;
                
            var siblings = abstractionLimitPointSub.SubResults;
            var datePurposes = siblings
                .Where(x => x.MatchedLabel?.Name == "DatePurposeRough")
                .ToList();

            var shouldAddGroups = true;
            
            if (datePurposes.Count >= 1)
            {
                individualGroups.Add(new AbstractionLimitGroup
                {
                    Limits = []
                });
                
                foreach (var datePurpose in datePurposes)
                {
                    individualGroups.Add(new AbstractionLimitGroup
                    {
                        TimePeriod = GetTimePeriod(datePurpose),
                        Limits = []
                    });
                }
            }
            else if (allIndividualGroups.Count == 0 && individualGroups.Count == 0)
            {
                individualGroups.Add(new AbstractionLimitGroup
                {
                    Limits = []
                });
            }
            else if (individualGroups.Count == 0)
            {
                shouldAddGroups = false;
                individualGroups.Add(allIndividualGroups[0]);
            }
            
            var valueResults = siblings
                .Where(sibling => !string.IsNullOrEmpty(sibling.MatchedLabel?.RelatedName))
                .ToList();
            
            var linkedLicenceNumbers = siblings
                .Where(sibling => sibling.MatchedLabel?.Name == "LinkedLicenceNumber")
                .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
                .Select(linkedLicenceNumber =>
                {
                    var condition = (Condition?)null; // TODO
                        
                    var linkedLicenceFilename = siblings
                        .FirstOrDefault(sibling =>
                            sibling.MatchedLabel?.Name == "LinkedLicenceFilename")?
                        .Text?
                        .FirstOrDefault()?
                        .Text;

                    return new LinkedLicence
                    {
                        LicenceNumber = linkedLicenceNumber,
                        Filename = linkedLicenceFilename,
                        Condition = condition,
                        ContainedIn = [
                            new LinkedLicenceSection
                            {
                                SectionName = LinkedLicenceSectionNames.AbstractionLimits,
                                LinkReason = GetLinkReason([abstractionLimitPointSub], linkedLicenceNumber)
                            }
                        ]
                    };
                })
                .ToList();

            var hasLinkedLicenceNumber = linkedLicenceNumbers.Count > 0;
            var aggregateLimits = new List<AggregateAbstractionLimit>();
                
            var purposeCondition = siblings
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PurposeCondition");
                    
            var purposeConditionSub = purposeCondition?
                .SubResults
                .Where(x => x.MatchedLabel?.Name == "PurposeConditionSub")
                .ToList();
                    
            var limitPurposes = purposeConditionSub?.Count > 0 ?
                purposeConditionSub.Select(pcs =>
                    new Purpose { Id = pcs.Text!.First().Text }).ToList()
                : null;
                    
            var pointCondition = siblings
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition");

            var pointConditionSub = pointCondition?
                .SubResults
                .Where(x => x.MatchedLabel?.Name == "PointConditionSub")
                .ToList();
                    
            var limitPoints = pointConditionSub?.Count > 0 ?
                pointConditionSub.Select(pcs =>
                    new Point { Id = pcs.Text!.First().Text }).ToList()
                : null;

            var dict = new Dictionary<string, int>();
            
            foreach (var valueResult in valueResults)
            {
                if (!double.TryParse(valueResult.Text?.FirstOrDefault()?.Text, out var number))
                {
                    continue;
                }

                if (!dict.TryAdd(valueResult.MatchedLabel?.RelatedName!, 0))
                {
                    dict[valueResult.MatchedLabel?.RelatedName!] += 1;
                }

                var allUnits = siblings?
                    .Where(sibling =>
                        sibling.MatchedLabel?.Name == valueResult.MatchedLabel?.RelatedName)
                    .ToList();

                var unitPosition = dict[valueResult.MatchedLabel?.RelatedName!];
                
                var units = allUnits!.Count > unitPosition ? allUnits[unitPosition]
                    .Text?
                    .FirstOrDefault()?
                    .Text : null;

                var text = valueResult.MatchedLabel?.Text?.FirstOrDefault()?.Text;
                    
                var abstractionLimit = new AggregateAbstractionLimit
                {
                    PeriodType = ToLimitPeriodType(text),
                    Value = number,
                    Units = units,
                    Points = limitPoints?.ToArray(),
                    Purposes = limitPurposes?.ToArray()
                };

                if (hasLinkedLicenceNumber || textSuggestsIsAggregate)
                {
                    aggregateLimits.Add(abstractionLimit);
                    continue;
                }

                if ((limitPoints == null || limitPoints.Count < 2)
                    && (limitPurposes == null || limitPurposes.Count < 2))
                {
                    var pos = GetPositionRelativeToDateLines(datePurposes, valueResult);

                    var individualGroup = individualGroups[pos];
                    individualGroup.Limits.Add(abstractionLimit);
                }
                else
                {
                    aggregateLimits.Add(abstractionLimit);
                }
            }

            if (shouldAddGroups)
            {
                allIndividualGroups.AddRange(individualGroups);
            }

            if (aggregateLimits.Count == 0)
            {
                continue;
            }
                
            var pointsLoop = aggregateLimits.First().Points;
            var purposesLoop = aggregateLimits.First().Purposes;
            var timeCutoff = (TimeCutoff?)null; // TODO
            var timePeriod = GetTimePeriod(
                siblings?.FirstOrDefault(s => s.MatchedLabel?.Name == "DateOnly"));
            
            var aggregate = new Aggregate
            {
                LicenceNumber = licenceNumber,
                LicenceVersionId = licenceVersionId,
                PrimaryType = linkedLicenceNumbers.Count >= 1
                    ? PrimaryType.LicenceToLicence
                    : PrimaryType.InLicence,
                NaldType = GetNaldType(),
                AggregateSetId = PositionConstants.ReplacementMarker,
                LinkedLicences = linkedLicenceNumbers.Count > 0 ? linkedLicenceNumbers.ToArray() : null,
                Limits = aggregateLimits,
                Points = pointsLoop?.ToArray() ?? [],
                Purposes = purposesLoop?.ToArray() ?? [],
                TimeCutoff = timeCutoff,
                TimePeriod = timePeriod
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
                foreach (var aggregateLimit in aggregateLimits)
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
                foreach (var aggregateLimit in aggregateLimits)
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
    
    private static PeriodOfAbstraction[] GetPeriods(List<LabelGroupResult> matches)
    {
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
                EndDate = periodResults.SubResults[1].Text?.FirstOrDefault()?.Text
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
                PurposeIds = null // TODO set purpose ids and point ids
            });
        }

        return returnList.ToArray();
    }

    private static MeanOfAbstraction[] GetMeansOfAbstraction(List<LabelGroupResult> matches)
    {
        var meansResult = matches.FirstOrDefault(result => result.LabelGroupName == "MeansOfAbstraction");
        var returnList = new List<MeanOfAbstraction>();

        if (meansResult == null)
        {
            return returnList.ToArray();
        }
        
        foreach (var meanResult in meansResult.SubResults)
        {
            var textWithoutNumber = meanResult.SubResults.FirstOrDefault(
                    x => x.MatchedLabel?.Name == "TextWithoutNumber")?
                .Text?
                .Select(t => t.Text);

            var meanId = meanResult.SubResults.FirstOrDefault(
                x => x.MatchedLabel?.Name == "MeanId");            
            
            var units = meanResult.SubResults.FirstOrDefault(
                x => x.MatchedLabel?.Name == "PerSecondUnitsMeans");

            var value = meanResult.SubResults.FirstOrDefault(
                x => x.MatchedLabel?.Name == "PerSecondValueMeans");
            
            if (textWithoutNumber == null && meanId == null)
            {
                continue;
            }
                
            var text = textWithoutNumber != null
                ? string.Join('\n', textWithoutNumber)
                : null;
            
            var number = meanId?.Text?.FirstOrDefault()?.Text;
            //var id = double.TryParse(number, out var numberResult) ? numberResult : (double?)null;

            var value1 = value?.Text?.FirstOrDefault()?.Text;
            var value2 = double.TryParse(value1, out var valueResult) ? valueResult : (double?)null;

            var periodType = LimitPeriodType.Unknown;

            if (text?.Contains("second", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                periodType = LimitPeriodType.PerSecond;
            }
            
            returnList.Add(new MeanOfAbstraction
            {
                Id = number,
                Description = text,
                AbstractionLimit = value2 != null ? new AbstractionLimit
                {
                    PeriodType = periodType,
                    Units = units?.Text?.FirstOrDefault()?.Text,
                    Value = value2
                } : null
            });
        }

        return returnList.ToArray();
    }
    
    private static PointOfAbstraction[] GetPoints(List<LabelGroupResult> matches)
    {
        var pointsResults = matches.FirstOrDefault(result => result.LabelGroupName == "Points");
        var returnList = new List<PointOfAbstraction>();

        if (pointsResults == null)
        {
            return returnList.ToArray();
        }
        
        foreach (var pointPurposeGroup in pointsResults.SubResults)
        {
            var purposeGroupName = pointPurposeGroup.SubResults
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PurposeGroupName");

            var purposeIds = purposeGroupName?.SubResults
                .Where(x => x.MatchedLabel?.Name == "PurposeGroupSub")
                .Select(x => x.Text?.FirstOrDefault()?.Text)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToArray();
            
            var points = pointPurposeGroup.SubResults
                .Where(x => x.MatchedLabel?.Name == "Point");

            foreach (var point in points)
            {
                var pointNumber = point.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PointPointNumber");

                var number = pointNumber?.Text?.FirstOrDefault()?.Text;

                var tLines = point.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PointTextWithoutPurposeAndPoint")?
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
                
                var pointTable = point.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PointTable");

                if (pointTable != null)
                {
                    var tableLines = pointTable.Text!;

                    foreach (var tableLine in tableLines)
                    {
                        var words = tableLine.Text.Split(' ');
                        var subId = words[0]; // e.g. A, D, E
                        
                        returnList.Add(new PointOfAbstraction
                        {
                            Description = tableLine.Text,
                            Id = $"{number} {subId}", // e.g 2.1 - A
                            PurposeIds = purposeIds,
                            TimeCutoff = timeCutoff
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
                    description = description.Substring(2);
                }
                
                returnList.Add(new PointOfAbstraction
                {
                    Description = description,
                    Id = number,
                    PurposeIds = purposeIds,
                    TimeCutoff = timeCutoff
                });
            }
        }

        return returnList.ToArray();
    }

    private static PurposeOfAbstraction[] GetPurposes(List<LabelGroupResult> matches)
    {
        var purposeResults = matches.FirstOrDefault(result => result.LabelGroupName == "Purpose");
        var returnList = new List<PurposeOfAbstraction>();

        if (purposeResults == null)
        {
            return returnList.ToArray();
        }
        
        foreach (var purposePointGroup in purposeResults.SubResults)
        {
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
                var purposeNumber = purpose.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PurposeNumber");
                
                var tLines = purpose.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "TextWithoutPoints")?
                    .Text?
                    .Select(t => t.Text)
                    .ToArray();

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
                                TimeCutoff = timeCutoff
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
                                TimeCutoff = timeCutoff
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
                                TimeCutoff = timeCutoff
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
                                TimeCutoff = timeCutoff
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
                                TimeCutoff = timeCutoff
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
                    TimeCutoff = timeCutoff
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
            _ => throw new NotSupportedException($"Unknown limit period type '{text}'")
        };
    }
    
    // ReSharper disable once IdentifierTypo
    private static string? GetNaldType()
    {
        return null;
    }
    
    public static List<LicenceSet> AddAdditionalLicenceSets(
        List<IReadOnlyList<LicenceSet>> licenceSetGroups,
        HashSet<string> impoundmentLicenceNumbers,
        HashSet<string> deadLicenceNumbers,
        HashSet<string> liveLicenceNumbers)
    {        
        var distinctLicenceSets = AsDistinctLicenceSets(licenceSetGroups);
        
        distinctLicenceSets.AddRange(AddMissingBackLinks(
            licenceSetGroups,
            true,
            impoundmentLicenceNumbers,
            deadLicenceNumbers,
            liveLicenceNumbers));

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
            var licenceSetsForLicence = GetAllLicenceSetsForLicence(
                licence.LicenceNumber!,
                distinctLicenceSets);

            var updatedLicenceSetIds = AddImplicitAndExplicitLicenceSets(licence, licenceSetsForLicence);
            updatedLicenceSetIds = AddEncompassingLicenceSets(licence, distinctLicenceSets, updatedLicenceSetIds);

            licence.LicenceSets = updatedLicenceSetIds.ToArray();
        }
    }
    
    private static List<LicenceSet> GetAllLicenceSetsForLicence(string licenceNumber, IReadOnlyList<LicenceSet> licenceSets)
    {
        var returnList = new List<LicenceSet>();

        foreach (var licenceSet in licenceSets)
        {
            if (licenceSet.Licences.All(l => l.LicenceNumber != licenceNumber))
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
                        .All(ll => distinctLicenceSet.Licences.Any(l => ll.LicenceNumber == l.LicenceNumber));

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
                .All(l => licence1.LicenceNumber == l.LicenceNumber
                    || licence1.LinkedLicences.Select(ll => ll.LicenceNumber).Contains(l.LicenceNumber));

            if (!allLinkedLicenceOfLicence)
            {
                continue;
            }

            var allLinkedLicenceOfLicenceExplicit = licenceSetForLicence.Licences
                .All(l => licence1.LicenceNumber == l.LicenceNumber
                      || licence1.LinkedLicences.Where(ll => ll.ContainedIn?.Any(ci =>
                              ci.SectionName == LinkedLicenceSectionNames.ImplicitBackLink) != true)
                          .Select(ll => ll.LicenceNumber).Contains(l.LicenceNumber));

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