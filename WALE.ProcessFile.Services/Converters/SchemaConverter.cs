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
        
        var aggregateSets = new List<AggregateSet>();

        if (aggregates.Count > 0)
        {
            aggregateSets.Add(new AggregateSet
            {
                Aggregates = aggregates.ToArray()
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

    private static MatchesResult ToMatchesResult(LabelGroupResult labelGroupResult)
    {
        var results = new List<LabelGroupResult>();
        results.AddRange(labelGroupResult.SubResults);
        
        return new MatchesResult
        {
            Matches = results
        };
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

        var effectiveDateStr = matches
            .FirstOrDefault(result => result.LabelGroupName == "DateEffective")?
            .Text?
            .FirstOrDefault()?
            .Text;
        
        var dateOfIssueStr = matches
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue")?
            .Text?
            .FirstOrDefault()?
            .Text;

        var dateOfOriginalIssueStr = matches
            .FirstOrDefault(result => result.LabelGroupName == "DateOfOriginalIssue")?
            .Text?
            .FirstOrDefault()?
            .Text;        
        
        var dateOfExpiryStr = matches
            .FirstOrDefault(result => result.LabelGroupName == "DateOfExpiry")?
            .Text?
            .FirstOrDefault()?
            .Text;

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

        var licenceVersion = new LicenceVersion
        {
            EffectiveDate = effectiveDate,
            ExpiryDate = expiryDate,
            IssueDate = dateOfIssue,
            OriginalIssueDate = dateOfOriginalIssue
        };
        
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

        var aggregates = new List<Aggregate>();
        var individual = new List<AbstractionLimit>();    
        
        if (abstractionLimitPointSubs != null)
        {
            foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
            {
                var siblings = abstractionLimitPointSub.SubResults;
                var valueResults = siblings
                    .Where(sibling => !string.IsNullOrEmpty(sibling.MatchedLabel?.RelatedName))
                    .ToList();

                var linkedLicenceNumbers = siblings
                    .Where(sibling => sibling.MatchedLabel?.Name == "LinkedLicenceNumber")
                    .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
                    .Select(linkedLicenceNumber =>
                    {
                        var condition = (Condition?)null; // TODO

                        return new LinkedLicence
                        {
                            LicenceNumber = linkedLicenceNumber,
                            Condition = condition
                        };
                    })
                    .ToList();

                var hasLinkedLicenceNumber = linkedLicenceNumbers.Count > 0;
                var aggregateLimits = new List<AbstractionLimit>();
                
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

                    var limitPoint = (Point?)null;
                    var limitPurpose = (Purpose?)null;
                    
                    var abstractionLimit = new AbstractionLimit
                    {
                        PeriodType = ToLimitPeriodType(valueResult.MatchedLabel?.Text?.FirstOrDefault()),
                        Value = number,
                        Units = units,
                        Point = limitPoint,
                        Purpose = limitPurpose
                    };

                    if (hasLinkedLicenceNumber)
                    {
                        aggregateLimits.Add(abstractionLimit);
                        continue;
                    }
                    
                    individual.Add(abstractionLimit);  
                }

                if (!hasLinkedLicenceNumber)
                {
                    continue;
                }
                
                var pointsLoop = new List<Point>(); // TODO
                var purposesLoop = new List<Purpose>(); // TODO
                var timeCutoff = (TimeCutoff?)null; // TODO
                var timePeriod = (TimePeriod?)null; // TODO
                
                var aggregate = new Aggregate
                {
                    LicenceNumber = licenceNumber,
                    LicenceVersionId = licenceVersion.LicenceVersionId,
                    PrimaryType = PrimaryType.LicenceToLicence, // TODO
                    SubType = SubType.PointToPoint, // TODO
                    NaldType = GetNaldType(),
                    AggregateSetId = PositionConstants.ReplacementMarker,
                    LinkedLicences = linkedLicenceNumbers.ToArray(),
                    Limits = aggregateLimits.ToArray(),
                    Points = pointsLoop.ToArray(),
                    Purposes = purposesLoop.ToArray(),
                    TimeCutoff = timeCutoff,
                    TimePeriod = timePeriod
                };
                        
                aggregates.Add(aggregate);
            }
        }
        
        var definitionOfYear = (TimePeriod?)null;//new TimePeriod();  // TODO
        
        return new Licence
        {

            Filename = matchesResult.Filename,
            LicenceNumber = licenceNumber,
            LicenceVersion = licenceVersion,
            Points = GetPoints(matches),
            Purposes = GetPurposes(matches),
            PeriodsOfAbstraction = GetPeriods(matches),
            DefinitionOfYear = definitionOfYear,
            AbstractionLimits = new AbstractionLimits
            {
                Aggregates = aggregates.ToArray(),
                Individual = individual.ToArray()
            }
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
            var textWithoutNumber = pointResult.SubResults
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PeriodOfAbstractionPoint")?
                .Text?
                .FirstOrDefault()?
                .Text;

            if (textWithoutNumber == null)
            {
                continue;
            }
                
            returnList.Add(new PeriodOfAbstraction
            {
                 PeriodType = AbstractionPeriodType.SetPeriod,
                 //TODO
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
        
        foreach (var pointResult in pointsResults.SubResults)
        {
            var textWithoutNumber = pointResult.SubResults
                .FirstOrDefault(x => x.MatchedLabel?.Name == "TextWithoutPurposeAndPoint")?
                .Text?
                .FirstOrDefault()?
                .Text;

            if (textWithoutNumber == null)
            {
                continue;
            }
                
            returnList.Add(new PointOfAbstraction
            {
                Description = textWithoutNumber
            });
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
        
        foreach (var purposeResult in purposeResults.SubResults)
        {
            foreach (var purposePointGroup in purposeResult.SubResults)
            {
                var textWithoutNumber = purposePointGroup.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "TextWithoutPoints")?
                    .Text?
                    .FirstOrDefault()?
                    .Text;

                if (textWithoutNumber == null)
                {
                    continue;
                }
                    
                returnList.Add(new PurposeOfAbstraction
                {
                    Description = textWithoutNumber
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
            "per year" => LimitPeriodType.PerYear,
            _ => throw new NotSupportedException($"Unknown limit period type '{text}'")
        };
    }
    
    private static string GetNaldType()
    {
        return string.Empty; // TODO
    }
}