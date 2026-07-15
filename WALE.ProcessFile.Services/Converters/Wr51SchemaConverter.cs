using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Enums.Wr51;
using WALE.ProcessFile.Services.Models.OutputSchema.Wr51;

namespace WALE.ProcessFile.Services.Converters;

public static class Wr51SchemaConverter
{
    public static async Task<Wr51Form> ToFormAsync(MatchesResult matchesResult)
    {
        return new Wr51Form
        {
            Metadata = new Wr51FormMetadata
            {
                DocumentTemplateVerison = GetDocumentTemplateVersion(matchesResult),
                Filename = matchesResult.Filename,
                IsScan = matchesResult.ScannedFile
            },
            // TODO a bunch more fields
            SourceOfSupply = GetInOrderStatus(matchesResult, "SourceOfSupply"),
            Purposes = GetInOrderStatus(matchesResult, "Purposes"),
            PointOfAbstraction = GetInOrderStatus(matchesResult, "PointOfAbstraction"),
            SpecialConditions = GetInOrderStatus(matchesResult, "SpecialConditions"),
            ChargingFactors = GetInOrderStatus(matchesResult, "ChargingFactors"),
            Land = GetInOrderStatus(matchesResult, "Land"),
            MeansOfAbstraction = GetInOrderStatus(matchesResult, "MeansOfAbstraction"),
            MeansOfMeasurement = GetInOrderStatus(matchesResult, "MeansOfMeasurement"),
            ProvisionOfInformation = GetInOrderStatus(matchesResult, "ProvisionOfInformation"),
            Quantities = GetInOrderStatus(matchesResult, "Quantities"),
            Records = GetInOrderStatus(matchesResult, "Records"),
            OtherProvisions = GetInOrderStatus(matchesResult, "OtherProvisions"),
            Period = GetInOrderStatus(matchesResult, "Period"),
            // TODO a bunch more fields
            Maintenance = new Wr51FormMaintenance
            {
                Maintenance = "", // TODO
                Frequency = "",
                ByWhom = ""
            },
            ReadingsTaken = new Wr51FormReadingsTaken
            {
                ReadingsTaken = "",
                Frequency = "",
                ByWhom = ""
            },
            // TODO a bunch more fields
        };
    }

    private static string? GetDocumentTemplateVersion(MatchesResult matchesResult)
    {
        return matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == "DocumentTemplateVersion")?
            .Text?
            .FirstOrDefault()?
            .Text;
    }
    
    private static InOrderStatus GetInOrderStatus(MatchesResult matchesResult,  string name)
    {
        var labelGroupResult = matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name);
        
        if (labelGroupResult == null)
        {
            return InOrderStatus.DidntMatch;
        }

        if (labelGroupResult.Text == null || labelGroupResult.Text.Count == 0)
        {
            return InOrderStatus.Blank;
        }

        var text = string.Join(" ", labelGroupResult.Text.Select(t => t.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            return InOrderStatus.Blank;
        }

        if (text.Equals("in", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.InOrder;
        }
        
        if (text.Equals("not", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotInOrder;
        }
        
        if (text.Equals("n/a", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotApplicable;
        }
        
        return InOrderStatus.Unknown;
    }
}