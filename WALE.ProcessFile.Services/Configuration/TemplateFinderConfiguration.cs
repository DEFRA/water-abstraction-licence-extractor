using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Configuration;

public static class TemplateFinderConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("Issuer", GetIssuerLabels()),
            ("Variation", GetVariationLabels()),
        ];
    }
    private static List<LabelToMatch> GetIssuerLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Issuer",
                Format = "Text",
                Text =
                [
                    new("Yorkshire.* River Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*Yorkshire.* Water Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*Northumbrian.* River.* Authority")
                    {
                    IsRegularExpression = true
                    },
                    new(".*Northumbrian.* Water.* Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*National.* River.* Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*National.* Water.* Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*Environment.* Agency")
                    {
                        IsRegularExpression = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ActuallyLabel,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetVariationLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Variation",
                Format = "Text",
                Text = 
                [
                    new("Variation"),
                    new("Superseded")
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost,
                IncludeStartLabelText = true
            }
        ];
    }
}