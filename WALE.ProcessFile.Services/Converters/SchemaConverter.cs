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
        
        var licenceNumber = matches!
            .FirstOrDefault(result => result.LabelGroupName == "LicenceNumber")?
            .Text?
            .FirstOrDefault()?
            .Text;

        var effectiveDate = (DateTime?)null;
        var expiryDate = (DateTime?)null;

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
        
        var aggregates = new List<Aggregate>();
        var individual = new List<AbstractionLimit>();
        
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
                ExpiryDate = expiryDate
            },
        };
    }
}