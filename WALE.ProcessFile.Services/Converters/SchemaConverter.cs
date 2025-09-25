using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums.OutputSchema;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Converters;

public static class SchemaConverter
{
    private static Licence ToLicence(MatchesResult matchesResult)
    {
        var matches = matchesResult.Matches;

        if (matches == null)
        {
            throw new Exception("No match object exists to convert");
        }
        
        var licenceNumber = matches
            .FirstOrDefault(result => result.LabelGroupName == "LicenceNumber")?
            .Text?
            .FirstOrDefault()?
            .Text;

        var effectiveDateStr = DateFormatConsistent(matches
            .FirstOrDefault(result => result.LabelGroupName == "DateEffective")?
            .Text?
            .FirstOrDefault()?
            .Text);

        var dateOfIssueStr = DateFormatConsistent(matches
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue")?
            .Text?
            .FirstOrDefault()?
            .Text);

        var dateOfOriginalIssueStr = DateFormatConsistent(matches
            .FirstOrDefault(result => result.LabelGroupName == "DateOfOriginalIssue")?
            .Text?
            .FirstOrDefault()?
            .Text);

        var dateOfExpiryStr = DateFormatConsistent(matches
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
        
        var (aggregates, individual) = GetAbstractionLimits(
            matches,
            licenceNumber,
            licenceVersion.LicenceVersionId,
            points,
            purposes);

        var linkedLicences = aggregates
            .Where(x => x.LinkedLicences?.Length >= 1)
            .SelectMany(x => x.LinkedLicences!)
            .ToList();

        linkedLicences.AddRange(GetRecordsLinkedLicences(matches));
        linkedLicences.AddRange(GetFurtherConditionsLinkedLicences(matches));
        linkedLicences.AddRange(GetAdditionalInformationLinkedLicences(matches));
        
        linkedLicences = linkedLicences
            .GroupBy(linkedLicence => linkedLicence.LicenceNumber)
            .Select(linkedLicencesGroup =>
            {
                var firstLinkedLicence = linkedLicencesGroup.First();
                var fromSection = new List<string>();

                foreach (var linkedLicence in linkedLicencesGroup)
                {
                    if (linkedLicence.FromSection == null)
                    {
                        continue;
                    }
                    
                    var sectionItems = linkedLicence.FromSection;

                    foreach (var sectionItem in sectionItems)
                    {
                        if (fromSection.Contains(sectionItem))
                        {
                            continue;
                        }
                        
                        fromSection.Add(sectionItem);
                    }
                }
                
                return new LinkedLicence
                {
                    LicenceNumber = firstLinkedLicence.LicenceNumber,
                    Filename = firstLinkedLicence.Filename,
                    Condition = firstLinkedLicence.Condition,
                    FromSection = fromSection.ToArray()
                };
            })
            .Where(linkedLicence => linkedLicence.LicenceNumber != licenceNumber)
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

        var noneSchemaData = new Dictionary<string, object>();

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
        
        return new Licence
        {
            Filename = matchesResult.Filename,
            LicenceNumber = licenceNumber,
            LicenceVersion = licenceVersion,
            MeansOfAbstraction = means,
            Points = points,
            Purposes = purposes,
            PeriodsOfAbstraction = GetPeriods(matches),
            DefinitionOfYear = GetDefinitionOfYear(matches),
            AbstractionLimits = limits,
            LinkedLicences = linkedLicences.ToArray(),
            NoneSchemaData = noneSchemaData
        };
    }
    
    public static async Task<IReadOnlyList<LicenceSet>> ToLicenceSetsAsync(
        MatchesResult matchesResult,
        Dictionary<string, string> fileLicenceMapping,
        IPdfDataExtractorService  pdfDataExtractorService,
        string outputFolder,
        string cacheFolder)
    {
        var returnList = new List<LicenceSet>();
        
        var primaryLicence = ToLicence(matchesResult);
        var previouslyParsedPaths = new List<string> { matchesResult.Filename! };

        var linkedLicences = await GetLinkedLicencesAsync(
            matchesResult,
            primaryLicence,
            fileLicenceMapping,
            pdfDataExtractorService,
            outputFolder,
            cacheFolder,
            previouslyParsedPaths);
        
        var allLicences = new List<Licence>(linkedLicences);
        allLicences.Insert(0, primaryLicence);

        var singleLicenceOnlySet = new LicenceSet
        {
            LicenceSetType = LicenceSetType.SingleLicenceOnly,
            Licences = [primaryLicence],
            AggregateSets = GetAggregateSets([primaryLicence], allLicences)
        };
        
        returnList.Add(singleLicenceOnlySet);
        
        var hasExplicitlyReferencedLicenceSet = allLicences.Count > 1
            || allLicences[0].LicenceNumber != primaryLicence.LicenceNumber;
        
        var explicitlyReferencedLicenceSet = hasExplicitlyReferencedLicenceSet ? new LicenceSet
        {
            LicenceSetType = LicenceSetType.AllLicencesExplicitlyReferenced,
            Licences = allLicences.ToArray(),
            AggregateSets = GetAggregateSets(allLicences, allLicences)
        } : null;

        if (explicitlyReferencedLicenceSet != null)
        {
            returnList.Add(explicitlyReferencedLicenceSet);            
        }

        foreach (var licence in allLicences)
        {
            if (licence.AbstractionLimits.Aggregates != null)
            {
                PopulateAggregateSetIds(licence.AbstractionLimits.Aggregates, allLicences);
            }
            
            AddMissingBackLinks([[explicitlyReferencedLicenceSet ?? singleLicenceOnlySet]], false, allLicences);

            var newLicenceSetIds = new List<string>
            {
                singleLicenceOnlySet.LicenceSetId
            };

            if (explicitlyReferencedLicenceSet != null)
            {
                newLicenceSetIds.Add(explicitlyReferencedLicenceSet.LicenceSetId);
            }
            
            // Add LicenceSetIds to licence
            licence.LicenceSetIds = newLicenceSetIds.ToArray();
        }
        
        return returnList;
    }
    
    public static List<LicenceSet> AddMissingBackLinks(
        IReadOnlyList<IReadOnlyList<LicenceSet>> licenceSetGroups,
        bool addImplicitLicenceSet,
        IReadOnlyList<Licence> allLicences)
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

                    var incomingAndOutgoingLinks = new List<string>(incomingLinks);
                    incomingAndOutgoingLinks.AddRange(outgoingLinks);
                    
                    //...
                    
                    foreach (var incomingLink in incomingLinks)
                    {
                        if (outgoingLinks.Contains(incomingLink)
                            || licence.LinkedLicences.Any(linkedLicence => linkedLicence.LicenceNumber == incomingLink))
                        {
                            continue;
                        }

                        // Back link is missing
                        var linkedLicencesNew = new List<LinkedLicence>(licence.LinkedLicences)
                        {
                            new()
                            {
                                LicenceNumber = incomingLink,
                                FromSection = ["ImplicitBackLink"]
                            }
                        };

                        licence.LinkedLicences = linkedLicencesNew.ToArray();

                        if (!addImplicitLicenceSet)
                        {
                            continue;
                        }

                        var implicitGroupExists = licenceSetGroup.Any(lsg =>
                            lsg.LicenceSetType == LicenceSetType.AllLicencesIncludingImplicitlyReferenced);

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
                            LicenceSetType = LicenceSetType.AllLicencesIncludingImplicitlyReferenced,
                            Licences = implicitLicences.ToArray(),
                            AggregateSets = GetAggregateSets(implicitLicences, allLicencesInSets)
                        };
                        
                        returnList.Add(implicitLicenceSet);

                        var newLicenceSetIds = new List<string>(licence.LicenceSetIds)
                        {
                            implicitLicenceSet.LicenceSetId
                        };
                        
                        licence.LicenceSetIds = newLicenceSetIds.ToArray();
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

    private static List<string> GetLicencesThatReferenceLicence(IEnumerable<Licence> licences, string licenceNumber)
    {
        var returnList = new List<string>();
        
        foreach (var licence in licences)
        {
            if (licence.LicenceNumber == licenceNumber)
            {
                continue;
            }

            if (licence.LinkedLicences.Any(lll => lll.LicenceNumber == licenceNumber))
            {
                returnList.Add(licence.LicenceNumber!);
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
    
    private static AggregateSet[]? GetAggregateSets(IReadOnlyList<Licence> licences, IReadOnlyList<Licence> allLicences)
    {
        var aggregates = new List<Aggregate>();

        foreach (var licence in licences)
        {
            if (licence.AbstractionLimits.Aggregates == null)
            {
                continue;
            }
            
            aggregates.AddRange(licence.AbstractionLimits.Aggregates);
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
        Dictionary<string, string> licenceMapping,
        IPdfDataExtractorService  pdfDataExtractorService,
        string outputFolder,
        string cacheFolder,
        List<string> previouslyParsedPaths)
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
                        subResult.MatchedLabel!.Name == "LinkedLicence")
                    .ToList();

                foreach (var linkedLicenceData in linkedLicencesData)
                {
                    var matches = ToMatchesResult(linkedLicenceData);
                    var linkedLicence = ToLicence(matches);
                        
                    returnLicences.Add(linkedLicence);   
                }
                    
                var linkedLicenceNumbers = abstractionLimitPointSub.SubResults
                    .Where(subResult =>
                        subResult.MatchedLabel!.Name == "LinkedLicenceNumber")
                    .ToList();

                foreach (var linkedLicencesNumberResult in linkedLicenceNumbers)
                {
                    var licenceNumber = linkedLicencesNumberResult.Text?.FirstOrDefault()?.Text;

                    if (licenceNumber == primaryLicence.LicenceNumber
                        || returnLicences.Any(licence => licence.LicenceNumber == licenceNumber))
                    {
                        continue;
                    }
                    
                    if (!licenceMapping.TryGetValue(licenceNumber!, out var relatedFileName))
                    {
                        returnLicences.Add(new Licence
                        {
                            LicenceNumber = licenceNumber,
                            Status = LicenceStatus.NotFound
                        });
                        
                        continue;
                    }
                    
                    var relatedFileMatches = await pdfDataExtractorService.GetMatchesAsync(
                        relatedFileName,
                        new LookupConfiguration(LabelConfiguration.GetLabels(), licenceMapping, outputFolder, cacheFolder),
                        previouslyParsedPaths);
                    
                    returnLicences.Add(ToLicence(relatedFileMatches));
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
            
            if (!licenceMapping.TryGetValue(linkedLicence.LicenceNumber!, out var relatedFileName))
            {
                returnLicences.Add(new Licence
                {
                    LicenceNumber = linkedLicence.LicenceNumber,
                    Status = LicenceStatus.NotFound
                });
                        
                continue;
            }
            
            var relatedFileMatches = await pdfDataExtractorService.GetMatchesAsync(
                relatedFileName,
                new LookupConfiguration(LabelConfiguration.GetLabels(), licenceMapping, outputFolder, cacheFolder),
                previouslyParsedPaths);
                    
            returnLicences.Add(ToLicence(relatedFileMatches));
        }

        return returnLicences;
    }

    private static string? DateFormatConsistent(string? input)
    {
        return input?.Replace(" ", string.Empty)
            .Replace("first", "1", StringComparison.InvariantCultureIgnoreCase)
            .Replace("second", "2", StringComparison.InvariantCultureIgnoreCase)
            .Replace("third", "3", StringComparison.InvariantCultureIgnoreCase)
            .Replace("fourth", "4", StringComparison.InvariantCultureIgnoreCase)
            .Replace("fifth", "5", StringComparison.InvariantCultureIgnoreCase)
            .Replace("sixth", "6", StringComparison.InvariantCultureIgnoreCase)
            .Replace("seventh", "7", StringComparison.InvariantCultureIgnoreCase)
            .Replace("eighth", "8", StringComparison.InvariantCultureIgnoreCase)
            .Replace("ninth", "9", StringComparison.InvariantCultureIgnoreCase)
            .Replace("tenth", "10", StringComparison.InvariantCultureIgnoreCase)
            .Replace("eleventh", "11", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twelfth", "12", StringComparison.InvariantCultureIgnoreCase)
            .Replace("thirteenth", "13", StringComparison.InvariantCultureIgnoreCase)
            .Replace("fourteenth", "14", StringComparison.InvariantCultureIgnoreCase)
            .Replace("fifteenth", "15", StringComparison.InvariantCultureIgnoreCase)
            .Replace("sixteenth", "16", StringComparison.InvariantCultureIgnoreCase)
            .Replace("seventeenth", "17", StringComparison.InvariantCultureIgnoreCase)
            .Replace("eighteenth", "18", StringComparison.InvariantCultureIgnoreCase)
            .Replace("nineteenth", "19", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twentieth", "20", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-first", "21", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-second", "22", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-third", "23", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-fourth", "24", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-fifth", "25", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-sixth", "26", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-seventh", "27", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-eighth", "28", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-ninth", "29", StringComparison.InvariantCultureIgnoreCase)
            .Replace("thirtieth", "30", StringComparison.InvariantCultureIgnoreCase)
            .Replace("thirty-first", "31", StringComparison.InvariantCultureIgnoreCase)
            .Replace("August", "Aug", StringComparison.InvariantCultureIgnoreCase)
            .Replace("DAYOF", string.Empty, StringComparison.InvariantCultureIgnoreCase)
            .Replace("st", string.Empty, StringComparison.InvariantCultureIgnoreCase)
            .Replace("nd", string.Empty, StringComparison.InvariantCultureIgnoreCase)
            .Replace("rd", string.Empty, StringComparison.InvariantCultureIgnoreCase)
            .Replace("th", string.Empty, StringComparison.InvariantCultureIgnoreCase);
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
                FromSection = ["AdditionalInformation"]
            })
            .ToList();
    }
    
    private static List<LinkedLicence> GetRecordsLinkedLicences(List<LabelGroupResult> matches)
    {
        var records = matches
            .FirstOrDefault(result => result.LabelGroupName == "Records");

        if (records?.SubResults == null || records.SubResults.Count == 0)
        {
            return [];
        }

        return records.SubResults
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "RecordsLinkedLicenceNumber")
            .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
            .Select(linkedLicenceNumber => new LinkedLicence
            {
                LicenceNumber = linkedLicenceNumber,
                FromSection = ["Records"]
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

        return furtherConditions.SubResults
            .Where(linkedLicenceNumber => linkedLicenceNumber.MatchedLabel?.Name == "FCLinkedLicenceNumber")
            .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
            .Select(linkedLicenceNumber => new LinkedLicence
            {
                LicenceNumber = linkedLicenceNumber,
                FromSection = ["FurtherConditions"]
            })
            .ToList();
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
                        
                    var filename = siblings
                        .FirstOrDefault(sibling =>
                            sibling.MatchedLabel?.Name == "LinkedLicenceFilename")?
                        .Text?
                        .FirstOrDefault()?
                        .Text;
                        
                    return new LinkedLicence
                    {
                        LicenceNumber = linkedLicenceNumber,
                        Filename = filename,
                        Condition = condition,
                        FromSection = ["AbstractionLimits"]
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

                var tLines = point.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PointTextWithoutPurposeAndPoint")?
                    .Text?
                    .Select(t => t.Text)
                    .ToList();
                
                var tKey = "Up to and Including ";
                
                var allTextWithoutNumber = tLines?
                    .Where(t => !t.StartsWith(tKey, StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();
                
                if (allTextWithoutNumber == null)
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
                
                var description = string.Join(' ', allTextWithoutNumber);
                var number = pointNumber?.Text?.FirstOrDefault()?.Text;

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
            _ => throw new NotSupportedException($"Unknown limit period type '{text}'")
        };
    }
    
    // ReSharper disable once IdentifierTypo
    private static string? GetNaldType()
    {
        return null;
    }
}