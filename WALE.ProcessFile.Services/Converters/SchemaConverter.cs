using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums.OutputSchema;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Converters;

public static class SchemaConverter
{
    public static LicenceGroup ToLicenceGroup(MatchesResult matchesResult)
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

                if (abstractionLimitPointSubs == null)
                {
                    continue;
                }

                foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
                {
                    var linkedLicencesLoop = abstractionLimitPointSub.SubResults!
                        .Where(subResult =>
                            subResult.MatchedLabel!.Name == "LinkedLicence")
                        .ToList();

                    foreach (var linkedLicence in linkedLicencesLoop)
                    {
                        var toMatchesResult = ToMatchesResult(linkedLicence);
                        var toLinkedLicence = ToLicence(toMatchesResult);
                        
                        licences.Add(toLinkedLicence);   
                    }
                }
            }
        }
        
        var licenceGroup = new LicenceGroup
        {
            Licences = licences.ToArray(),
            AggregateSets = null
        };

        foreach (var licence in licences)
        {
            var aggregates = licence
                .AbstractionLimits?
                .Aggregates;

            if (aggregates == null)
            {
                continue;
            }

            _ = aggregates
                .Where(aggregate => aggregate.GroupId == PositionConstants.ReplacementMarker)
                .Select(aggregate => aggregate.GroupId = licenceGroup.Id)
                .ToList();
        }
        
        return licenceGroup;
    }

    private static MatchesResult ToMatchesResult(LabelGroupResult labelGroupResult)
    {
        var results = new List<LabelGroupResult>();
        results.AddRange(labelGroupResult.SubResults!);
        
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
            .SubResults?
            .Where(res => res.MatchedLabel?.Name == "AbstractionLimitPoint")
            .ToList();

        var abstractionLimitPointSubs = abstractionLimitPoints?
            .Where(res => res.SubResults != null)
            .SelectMany(res => res.SubResults!)
            .Where(res => res.MatchedLabel?.Name == "AbstractionLimitPointSub")
            .ToList();

        var aggregates = new List<Aggregate>();
        var individual = new List<AbstractionLimit>();    
        
        if (abstractionLimitPointSubs != null)
        {
            foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
            {
                var siblings = abstractionLimitPointSub.SubResults;
                var valueResults = siblings?
                    .Where(sibling => !string.IsNullOrEmpty(sibling.MatchedLabel?.RelatedName))
                    .ToList();

                if (valueResults == null)
                {
                    continue;
                }

                var linkedLicenceNumbers = siblings?
                    .Where(sibling => sibling.MatchedLabel?.Name == "LinkedLicenceNumber")
                    .Select(linkedLicenceNumber => linkedLicenceNumber.Text?.FirstOrDefault()?.Text)
                    .Select(linkedLicenceNumber => new LinkedLicence
                    {
                        LicenceNumber = linkedLicenceNumber,
                        Condition = null
                    })
                    .ToList();

                var hasLinkedLicenceNumber = linkedLicenceNumbers?.Count > 0;
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

                    var abstractionLimit = new AbstractionLimit
                    {
                        Name = valueResult.MatchedLabel?.Text?.FirstOrDefault(),
                        Value = number,
                        Units = units
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
                
                var aggregate = new Aggregate
                {
                    LicenceNumber = licenceNumber,
                    LicenceVersionId = licenceVersion.LicenceVersionId,
                    PrimaryType = PrimaryType.LicenceToLicence,
                    SubType = SubType.PointToPoint,
                    GroupId = PositionConstants.ReplacementMarker,
                    LinkedLicences = linkedLicenceNumbers?.ToArray(),
                    Limits = aggregateLimits.ToArray()
                };
                        
                aggregates.Add(aggregate);
            }
        }
        
        return new Licence
        {
            AbstractionLimits = new AbstractionLimits
            {
                Aggregates = aggregates.ToArray(),
                Individual = individual.ToArray()
            },
            Filename = matchesResult.Filename,
            LicenceNumber = licenceNumber,
            LicenceVersion = licenceVersion
        };
    }
}