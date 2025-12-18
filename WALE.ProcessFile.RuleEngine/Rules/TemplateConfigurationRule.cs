using WALE.ProcessFile.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.Services.Configuration;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class TemplateConfigurationRule : IRule<TemplateFinderResult>
{
    private readonly LookupConfiguration _lookupConfiguration;
    private readonly List<(string LabelGroupName, List<LabelToMatch> Labels)> _configuration;
    private readonly string _templateType;
    private readonly int _pagesThresholdForLicence;

    public string RuleName => $"TemplateConfigurationRule_{_templateType}";
    public int Priority => 1;

    /// <summary>
    /// Initializes a new instance of TemplateConfigurationRule
    /// </summary>
    /// <param name="lookupConfiguration">The lookup configuration containing labels for included, excluded, and variation groups</param>
    /// <param name="templateType">The template type this rule identifies</param>
    /// <param name="priority">The priority of this rule (default: 100)</param>
    public TemplateConfigurationRule(
        LookupConfiguration lookupConfiguration, 
        string templateType = "Unknown", 
        int pagesThresholdForLicence = 0)
    {
        _lookupConfiguration = lookupConfiguration ?? throw new ArgumentNullException(nameof(lookupConfiguration));
        _configuration = lookupConfiguration.Labels;
        _templateType = templateType;
        _pagesThresholdForLicence = pagesThresholdForLicence;
    }

    public bool CanApply(MatchesResult content)
    {
        // Get the included and excluded label group names from configuration
        var includedLabelGroups = _configuration.Where(c => c.LabelGroupName.Equals("Included", StringComparison.OrdinalIgnoreCase)).ToList();
        var excludedLabelGroups = _configuration.Where(c => c.LabelGroupName.Equals("Excluded", StringComparison.OrdinalIgnoreCase)).ToList();

        // Check if any included patterns are found
        bool hasIncluded = false;
        if (includedLabelGroups.Any())
        {
            hasIncluded = includedLabelGroups.Any(group => 
                group.Labels.Any(label => AreAnyTextPatternsFound(label, content)));
        }

        // Check if any excluded patterns are found
        bool hasExcluded = false;
        if (excludedLabelGroups.Any())
        {
            hasExcluded = excludedLabelGroups.Any(group => 
                group.Labels.Any(label => AreAnyTextPatternsFound(label, content)));
        }

        // Rule applies if we have included patterns and no excluded patterns
        return hasIncluded && !hasExcluded;
    }

    public TemplateFinderResult Apply(MatchesResult content)
    {
        // Determine if it's a variation based on variation labels
        var variationLabelGroups = _configuration.Where(c => c.LabelGroupName.Equals("Variation", StringComparison.OrdinalIgnoreCase)).ToList();
        bool hasVariation = false;

        if (variationLabelGroups.Any())
        {
            hasVariation = variationLabelGroups.Any(group => 
                group.Labels.Any(label => AreAnyTextPatternsFound(label, content)));
        }

        // Determine template based on template type and characteristics
        var template = DetermineTemplate("Licence", content.NumberOfPages, hasVariation);

        return new TemplateFinderResult
        {
            TemplateType = _templateType,
            Template = template
        };
    }

    /// <summary>
    /// Checks if any text patterns from a label are found in the content
    /// </summary>
    /// <param name="label">The label containing patterns to match</param>
    /// <param name="content">The content to search in</param>
    /// <returns>True if any patterns are found</returns>
    private bool AreAnyTextPatternsFound(LabelToMatch label, MatchesResult content)
    {
        if (label.Text == null || !label.Text.Any())
            return false;

        // Check if any of the text patterns match the content
        return content.Matches?.Any(match => 
            match.Text.Any(text => 
                label.Text.Any(pattern => 
                    text.Text.Contains(pattern.Text, StringComparison.OrdinalIgnoreCase)))) == true;
    }

    /// <summary>
    /// Determines the template name based on template type, page count, and variation status
    /// </summary>
    /// <param name="templateType">The base template type</param>
    /// <param name="pageCount">Number of pages in the document</param>
    /// <param name="hasVariation">Whether variations were detected</param>
    /// <returns>The determined template name</returns>
    private string DetermineTemplate(string templateType, int pageCount, bool hasVariation)
    {
        var template = templateType;

        // Add variation suffix if variations are detected
        if (hasVariation || pageCount > _pagesThresholdForLicence)
        {
            template += " And Variation";
        }

        return template;
    }
}
