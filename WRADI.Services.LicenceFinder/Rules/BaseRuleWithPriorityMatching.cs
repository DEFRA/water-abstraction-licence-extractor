using System.Collections.Generic;
using System.Linq;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.Services.LicenceFinder.Helpers;

namespace WRADI.Services.LicenceFinder.Rules;

/// <summary>
/// Abstract base class for license matching rules that use priority-based matching
/// Provides common functionality for dynamic rule naming and duplicate tracking
/// </summary>
public abstract class BaseRuleWithPriorityMatching : ILicenceMatchingRule
{
    private string? _dynamicRuleName;
    private bool _hasDuplicates;

    public string RuleName => _dynamicRuleName ?? GetDefaultRuleName();
    public abstract int Priority { get; }
    public bool HasDuplicates => _hasDuplicates;

    /// <summary>
    /// Gets the default rule name when no dynamic rule name is set
    /// </summary>
    /// <returns>The default rule name for this rule type</returns>
    protected abstract string GetDefaultRuleName();

    /// <summary>
    /// Finds a matching DMS record based on priority matching logic
    /// </summary>
    /// <param name="permitNumber"></param>
    /// <param name="dmsLookups">Pre-built lookup dictionaries for fast searching</param>
    /// <returns>Matching DMS record or null if no match found</returns>
    public DmsExtract? FindMatch(
        string? permitNumber,
        DmsLookupIndexes dmsLookups)
    {
        // Reset state for new search
        _dynamicRuleName = null;
        _hasDuplicates = false;

        // Input validation
        if (string.IsNullOrWhiteSpace(permitNumber))
        {
            return null;
        }

        // Get matching records using the specific rule's logic
        var matchingRecords = GetMatchingRecords(permitNumber, dmsLookups);

        if (!matchingRecords.Any())
        {
            return null;
        }

        // Apply priority matching logic
        var priorityResult = RuleHelpers.FindPriorityMatch(matchingRecords, GetRuleBaseName());

        // Update state based on results
        _dynamicRuleName = priorityResult.ruleName;
        _hasDuplicates = priorityResult.hasDuplicate;

        return priorityResult.dmsExtract;
    }

    /// <summary>
    /// Gets the base name used for constructing dynamic rule names
    /// </summary>
    /// <returns>The base rule name</returns>
    protected abstract string GetRuleBaseName();

    /// <summary>
    /// Gets the matching DMS records based on the specific rule's logic
    /// Each derived class implements its own matching strategy
    /// </summary>
    /// <param name="permitNumber"></param>
    /// <param name="dmsLookups">Pre-built lookup dictionaries for fast searching</param>
    /// <returns>Collection of matching DMS records</returns>
    protected abstract List<DmsExtract> GetMatchingRecords(
        string permitNumber,
        DmsLookupIndexes dmsLookups);
}