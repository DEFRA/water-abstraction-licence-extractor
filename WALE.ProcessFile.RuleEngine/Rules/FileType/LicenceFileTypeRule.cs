using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;

namespace WALE.ProcessFile.RuleEngine.Rules.FileType;

/// <summary>
/// Rule to identify schedule files based on content containing "Change in" terms
/// </summary>
public class LicenceFileTypeRule : IRule<FileTypeResult>
{
    public string RuleName => "LicenseFileType";
    public int Priority => 100;

    private readonly string[] _scheduleTerms = { "SCHEDULE OF CONDITIONS" };

    public bool CanApply(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        return _scheduleTerms.Any(term => 
            content.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public FileTypeResult Apply(string content)
    {
        var matchedTerms = _scheduleTerms
            .Where(term => content.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new FileTypeResult
        {
            FileType = "Schedule",
            Confidence = 0.9,
            IdentifiedByRule = RuleName,
            MatchedTerms = matchedTerms,
            Metadata = new Dictionary<string, object>
            {
                ["MatchCount"] = matchedTerms.Count,
                ["ContentLength"] = content.Length
            }
        };
    }
}
