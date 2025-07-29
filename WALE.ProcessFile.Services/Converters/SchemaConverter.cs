using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Converters;

public static class SchemaConverter
{
    public static Licence ToLicence(MatchesResult matchesResult)
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

        var abstractionLimitsSection = matches
            .FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        var abstractionLimitPoints = abstractionLimitsSection?
            .SubResults?
            .Where(res => res.MatchedLabel?.Name == "AbstractionLimitPoint")
            .ToList();

        var isSinglePoint = abstractionLimitPoints?.Count == 1;

        var abstractionLimitPointSubs = abstractionLimitPoints?
            .Where(x => x.SubResults != null)
            .SelectMany(x => x.SubResults!)
            .Where(res => res.MatchedLabel?.Name == "AbstractionLimitPointSub")
            .ToList();

        var aggregates = isSinglePoint ? new List<Aggregate>() : [];
        var individual = isSinglePoint ? new List<AbstractionLimit>() : [];        
        
        if (abstractionLimitPointSubs != null)
        {
            foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
            {
                var siblings = abstractionLimitPointSub.SubResults;
                
                var valueResults = siblings?
                    .Where(y => !string.IsNullOrEmpty(y.MatchedLabel?.RelatedName))
                    .ToList();

                if (valueResults != null)
                {
                    foreach (var valueResult in valueResults)
                    {
                        var number = double.Parse(valueResult.Text!.FirstOrDefault()?.Text!);
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
                
                        individual.Add(abstractionLimit);
                    }
                }
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
            LicenceVersion = new LicenceVersion
            {
                EffectiveDate = effectiveDate,
                ExpiryDate = expiryDate,
                IssueDate = dateOfIssue,
                OriginalIssueDate = dateOfOriginalIssue
            },
        };
    }
}