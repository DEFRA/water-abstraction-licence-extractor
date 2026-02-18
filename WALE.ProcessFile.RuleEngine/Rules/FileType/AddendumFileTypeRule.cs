using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.Helpers;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.RuleEngine.Rules.FileType;

/// <summary>
/// Rule to identify addendum files based on content containing "this addendum" terms
/// </summary>
public class AddendumFileTypeRule : IRule<FileTypeResult>
{
    public string RuleName => "AddendumFileType";
    public string? Region { get; set; }
    public int Priority => 100;

    private readonly string[] _addendumTerms = { "Please keep this addendum with" };

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "Addendum").Any() == true;
    }

    public FileTypeResult Apply(MatchesResult content)
    {
        var matchedTerms = content.Matches?
            .Where(m => m.LabelGroupName == "Addendum")
            .ToList();

        return new FileTypeResult
        {
            FileType = "Addendum",
            Confidence = 0.9,
            IdentifiedByRule = RuleName,
            MatchedTerms = matchedTerms?.SelectMany(m => m.Text?.Select(t => t.Text)!).ToList()!,
            DateOfIssue = Date.DateFormatConsistent(RuleSharedHelper.ExtractDateOfIssue(content)),
            LicenceNumber = RuleSharedHelper.ExtractLicenceNumber(content),
            Metadata = new Dictionary<string, object>
            {
                ["MatchCount"] = matchedTerms?.Count ?? 0
            }
        };
    }
}