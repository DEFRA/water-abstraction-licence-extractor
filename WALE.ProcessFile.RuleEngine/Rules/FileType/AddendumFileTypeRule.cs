using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;

namespace WALE.ProcessFile.RuleEngine.Rules.FileType;

/// <summary>
/// Rule to identify addendum files based on content containing "this addendum" terms
/// </summary>
public class AddendumFileTypeRule : IRule<FileTypeResult>
{
    public string RuleName => "AddendumFileType";
    public int Priority => 100;

    private readonly string[] _addendumTerms = { "Please keep this addendum with the", "CHANGE OF" };

    public bool CanApply(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        return _addendumTerms.Any(term => 
            content.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public FileTypeResult Apply(string content)
    {
        var matchedTerms = _addendumTerms
            .Where(term => content.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new FileTypeResult
        {
            FileType = "Addendum",
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
