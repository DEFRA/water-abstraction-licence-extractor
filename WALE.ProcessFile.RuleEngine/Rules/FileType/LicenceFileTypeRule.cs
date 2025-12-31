using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.Helpers;

namespace WALE.ProcessFile.RuleEngine.Rules.FileType;

/// <summary>
/// Rule to identify schedule files based on content containing "Change in" terms
/// </summary>
public class LicenceFileTypeRule : IRule<FileTypeResult>
{
    public string RuleName => "LicenceFileType";
    public int Priority => 100;
    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "Licence Header")?.Any() == true;
    }

    public FileTypeResult Apply(MatchesResult content)
    {
        var matchedTerms = content.Matches?
            .Where(m => m.LabelGroupName == "Licence Header")
            .ToList();

        return new FileTypeResult
        {
            FileType = "Licence",
            Confidence = 0.9,
            IdentifiedByRule = RuleName,
            MatchedTerms = matchedTerms?.SelectMany(m => m.Text.Select(t => t.Text))?.ToList(),
            DateOfIssue = RuleSharedHelper.DateFormatConsistent(RuleSharedHelper.ExtractDateOfIssue(content)),
            LicenceNumber = RuleSharedHelper.ExtractLicenceNumber(content),
            Metadata = new Dictionary<string, object>
            {
                ["MatchCount"] = matchedTerms?.Count ?? 0
            }
        };
    }
}
