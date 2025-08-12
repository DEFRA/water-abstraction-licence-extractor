using System.Text.Json;
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

    private static Licence ToLicence(MatchesResult matchesResult)
    {
        var matches = matchesResult.Matches;

        if (matches == null)
        {
            throw new Exception("No match object exists to convert");
        }

        var licenceHolder = matches
            .FirstOrDefault(result => result.LabelGroupName == "Company")?
            .Text?
            .FirstOrDefault()?
            .Text;        
        
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
                    
                    var abstractionLimit = new AggregateAbstractionLimit
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
                var timeCutoff = (TimeLimited?)null; // TODO
                var timePeriod = (TimePeriod?)null; // TODO
                
                var aggregate = new Aggregate
                {
                    LicenceNumber = licenceNumber,
                    LicenceVersionId = licenceVersion.LicenceVersionId,
                    PrimaryType = !string.IsNullOrEmpty(licenceNumber)
                        ? PrimaryType.LicenceToLicence
                        : PrimaryType.InLicence,
                    SubType = pointsLoop.Count > 0 ? SubType.PointToPoint : null,
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
        
        var means = GetMeansOfAbstraction(matches);

        if (means.FirstOrDefault()?.Limit?.Value != null)
        {
            var meanLimit = JsonSerializer.Deserialize<AbstractionLimit>(
                JsonSerializer.Serialize(means.First().Limit!))!;

            meanLimit.ImplicitLimit = true;
            individual.Add(meanLimit);
        }
        
        var limits = new AbstractionLimits
        {
            Aggregates = aggregates.ToArray(),
            Individual = individual.ToArray()
        };
        
        return new Licence
        {
            Filename = matchesResult.Filename,
            LicenceNumber = licenceNumber,
            LicenceHolder = licenceHolder,
            LicenceVersion = licenceVersion,
            MeansOfAbstraction = means,
            Points = GetPoints(matches),
            Purposes = GetPurposes(matches),
            PeriodsOfAbstraction = GetPeriods(matches),
            DefinitionOfYear = GetDefinitionOfYear(matches),
            AbstractionLimits = limits
        };
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
            var id = double.TryParse(number, out var numberResult) ? numberResult : (double?)null;
            
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
                Id = id,
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
            var id = double.TryParse(number, out var numberResult) ? numberResult : (double?)null;

            var value1 = value?.Text?.FirstOrDefault()?.Text;
            var value2 = double.TryParse(value1, out var valueResult) ? valueResult : (double?)null;

            var periodType = LimitPeriodType.Unknown;

            if (text?.Contains("second", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                periodType = LimitPeriodType.PerSecond;
            }
            
            returnList.Add(new MeanOfAbstraction
            {
                Id = id,
                Description = text,
                Limit = value2 != null ? new AbstractionLimit
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
        
        foreach (var pointResult in pointsResults.SubResults)
        {
            var pointPointNumber = pointResult.SubResults
                .FirstOrDefault(x => x.MatchedLabel?.Name == "PointPointNumber");
            
            var allTextWithoutNumber = pointResult.SubResults
                .FirstOrDefault(x => x.MatchedLabel?.Name == "TextWithoutPurposeAndPoint")?
                .Text?
                .Select(t => t.Text)
                .ToArray();

            if (allTextWithoutNumber == null && pointPointNumber == null)
            {
                continue;
            }

            var description = allTextWithoutNumber != null
                ? string.Join('\n', allTextWithoutNumber)
                : null;
            
            var number = pointPointNumber?.Text?.FirstOrDefault()?.Text;
            var id = double.TryParse(number, out var numberResult) ? numberResult : (double?)null;
            
            returnList.Add(new PointOfAbstraction
            {
                Description = description,
                Id = id
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
                var purposeNumber = purposePointGroup.SubResults
                    .FirstOrDefault(x => x.MatchedLabel?.Name == "PurposeNumber");
                
                var allTextWithoutNumber = purposePointGroup.SubResults
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
                var id = double.TryParse(number, out var numberResult) ? numberResult : (double?)null;
                
                returnList.Add(new PurposeOfAbstraction
                {
                    Id = id,
                    Description = description
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
            "in total" => LimitPeriodType.InTotal,
            _ => throw new NotSupportedException($"Unknown limit period type '{text}'")
        };
    }
    
    private static string? GetNaldType()
    {
        return null;
    }
}