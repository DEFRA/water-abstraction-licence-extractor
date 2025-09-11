using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums.OutputSchema;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Converters;

public static class SchemaConverter
{
    public static LicenceSet ToLicenceGroup(MatchesResult matchesResult)
    {
        var licences = new List<Licence>
        {
            ToLicence(matchesResult)
        };

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
                    var linkedLicencesLoop = abstractionLimitPointSub.SubResults
                        .Where(subResult =>
                            subResult.MatchedLabel!.Name == "LinkedLicence")
                        .ToList();

                    foreach (var linkedLicence in linkedLicencesLoop)
                    {
                        var toMatchesResult = ToMatchesResult(linkedLicence);
                        var toLinkedLicence = ToLicence(toMatchesResult);
                        
                        licences.Add(toLinkedLicence);   
                    }
                    
                    var linkedLicencesNumbers = abstractionLimitPointSub.SubResults
                        .Where(subResult =>
                            subResult.MatchedLabel!.Name == "LinkedLicenceNumber")
                        .ToList();

                    foreach (var linkedLicencesNumber in linkedLicencesNumbers)
                    {
                        var text = linkedLicencesNumber.Text?.FirstOrDefault()?.Text;

                        if (licences.Any(licence => licence.LicenceNumber == text))
                        {
                            continue;
                        }
                        
                        licences.Add(new Licence
                        {
                            LicenceNumber = text
                        });
                    }
                }
            }
        }
        
        var aggregates = new List<Aggregate>();

        foreach (var licence in licences)
        {
            aggregates.AddRange(licence.AbstractionLimits.Aggregates);
        }

        var diffAggregates = aggregates
            .GroupBy(x => string.Join(',', x.LinkedLicences.OrderBy(y => y.LicenceNumber)))
            .ToList();
                
        var aggregateSets = new List<AggregateSet>();

        foreach (var diffAggregate in diffAggregates)
        {
            aggregateSets.Add(new AggregateSet
            {
                Aggregates = diffAggregate.ToArray()
            });
        }
        
        var licenceGroup = new LicenceSet
        {
            Licences = licences.ToArray(),
            AggregateSets = aggregateSets.ToArray()
        };

        foreach (var licence in licences)
        {
            var licenceAggregates = licence
                .AbstractionLimits
                .Aggregates;

            _ = licenceAggregates
                .Where(aggregate => aggregate.AggregateSetId == PositionConstants.ReplacementMarker)
                .Select(aggregate => aggregate.AggregateSetId = licenceGroup.LicenceSetId)
                .ToList();
        }
        
        return licenceGroup;
    }

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
        
        var limits = new AbstractionLimits
        {
            Aggregates = aggregates,
            Individual = individual
        };
        
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
            AbstractionLimits = limits
        };
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
        
        var parts = value.Split(" to ");
        
        return new TimePeriod
        {
            StartDate = parts[0],
            EndDate = parts.Length > 1 ? parts[1] : null,
            PeriodType = AbstractionPeriodType.SetPeriod,
            Inclusive = true
        };
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
                        Condition = condition
                    };
                })
                .ToList();

            var hasLinkedLicenceNumber = linkedLicenceNumbers.Count > 0;
            var aggregateLimits = new List<AggregateAbstractionLimit>();
                
            var purposeCondition = siblings?
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PurposeCondition");
                    
            var purposeConditionSub = purposeCondition?
                .SubResults
                .Where(x => x.MatchedLabel?.Name == "PurposeConditionSub")
                .ToList();
                    
            var limitPurposes = purposeConditionSub?.Count > 0 ?
                purposeConditionSub.Select(pcs =>
                    new Purpose { Id = pcs!.Text!.First().Text }).ToList()
                : null;
                    
            var pointCondition = siblings?
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition");

            var pointConditionSub = pointCondition?
                .SubResults
                .Where(x => x.MatchedLabel?.Name == "PointConditionSub")
                .ToList();
                    
            var limitPoints = pointConditionSub?.Count > 0 ?
                pointConditionSub.Select(pcs =>
                    new Point { Id = pcs.Text!.First().Text }).ToList()
                : null;
                
            foreach (var valueResult in valueResults)
            {
                if (!double.TryParse(valueResult.Text?.FirstOrDefault()?.Text, out var number))
                {
                    continue;
                }
                    
                var units = siblings?
                    .FirstOrDefault(sibling =>
                        sibling.MatchedLabel?.Name == valueResult.MatchedLabel?.RelatedName)?
                    .Text?
                    .FirstOrDefault()?
                    .Text;

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
                    var pos = GetPositionRelativeToDateLines(datePurposes, valueResult.LineNumber);

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
                
            var aggregate = new Aggregate
            {
                LicenceNumber = licenceNumber,
                LicenceVersionId = licenceVersionId,
                PrimaryType = linkedLicenceNumbers.Count >= 1
                    ? PrimaryType.LicenceToLicence
                    : PrimaryType.InLicence,
                NaldType = GetNaldType(),
                AggregateSetId = PositionConstants.ReplacementMarker,
                LinkedLicences = linkedLicenceNumbers.ToArray(),
                Limits = aggregateLimits,
                Points = pointsLoop?.ToArray() ?? [],
                Purposes = purposesLoop?.ToArray() ?? [],
                TimeCutoff = timeCutoff,
                TimePeriod = GetTimePeriod(siblings?.FirstOrDefault())
            };

            // If there are no points, purposes or licences specified, then it
            // must mean its relevant to all points and purposes
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
                
            if (aggregate.Points.Length > 0)
            {
                foreach (var aggregateLimit in aggregateLimits)
                {
                    aggregateLimit.Points = null;
                }
            }
                        
            allAggregates.Add(aggregate);
        }

        return (allAggregates.ToArray(), allIndividualGroups.ToArray());
    }

    private static int GetPositionRelativeToDateLines(List<LabelGroupResult>? dateLines, int lineNumber)
    {
        if (dateLines == null || dateLines.Count == 0)
        {
            return 0;
        }

        var match = dateLines
            .OrderBy(matchLineNumber =>
            {
                var diff = matchLineNumber.LineNumber - lineNumber;

                if (0 > diff)
                {
                    return int.MaxValue;
                }

                return diff;
            })
            .First();

        return dateLines.IndexOf(match);
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
        var results = new List<LabelGroupResult>();
        results.AddRange(labelGroupResult.SubResults);
        
        return new MatchesResult
        {
            Matches = results
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
                .FirstOrDefault(x => x.MatchedLabel?.Name == "TextWithoutPurposeAndPoint")?
                .Text?
                .Select(t => t.Text);
            
            if (textWithoutNumber == null && periodPeriodNumber == null)
            {
                continue;
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
                EndDate = endDate
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
                .ToArray() ?? [];
            
            var points = pointPurposeGroup.SubResults
                .Where(x => x.MatchedLabel?.Name == "Point");

            foreach (var point in points)
            {
                var pointNumber = point.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PointPointNumber");

                var allTextWithoutNumber = point.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "TextWithoutPurposeAndPoint")?
                    .Text?
                    .Select(t => t.Text)
                    .ToArray();

                if (allTextWithoutNumber == null)
                {
                    continue;
                }

                var description = string.Join(' ', allTextWithoutNumber);
                var number = pointNumber?.Text?.FirstOrDefault()?.Text;

                returnList.Add(new PointOfAbstraction
                {
                    Description = description,
                    Id = number,
                    PurposeIds = purposeIds
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
                .ToArray() ?? [];
            
            var purposes = purposePointGroup?.SubResults
                .Where(x => x.MatchedLabel!.Name == "Purpose");

            if (purposes == null)
            {
                continue;
            }
            
            foreach (var purpose in purposes)
            {
                var purposeNumber = purpose.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PurposeNumber");
                
                var allTextWithoutNumber = purpose.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "TextWithoutPoints")?
                    .Text?
                    .Select(t => t.Text)
                    .ToArray();

                if (allTextWithoutNumber == null && purposeNumber == null)
                {
                    continue;
                }
                
                var description = allTextWithoutNumber != null
                    ? string.Join('\n', allTextWithoutNumber)
                    : null;
                    
                var number = purposeNumber?.Text?.FirstOrDefault()?.Text;
                
                returnList.Add(new PurposeOfAbstraction
                {
                    Id = number,
                    Description = description,
                    PointIds = pointIds
                });
            }
        }

        return returnList.ToArray();
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
    
    private static string? GetNaldType()
    {
        return null;
    }
}