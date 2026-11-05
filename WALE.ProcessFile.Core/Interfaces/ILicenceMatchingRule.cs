using WALE.ProcessFile.Core.Models.Dms;

namespace WALE.ProcessFile.Core.Interfaces;

/// <summary>
/// Interface for implementing license matching rules
/// </summary>
public interface ILicenceMatchingRule
{
    /// <summary>
    /// The name of the rule for identification purposes
    /// </summary>
    string RuleName { get; }
    
    /// <summary>
    /// The flag suggesting if dupes were found
    /// </summary>
    bool HasDuplicates { get; }

    /// <summary>
    /// The priority of the rule (lower numbers have higher priority)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Finds a matching DMS record using optimized lookup dictionaries
    /// </summary>
    /// <param name="permitNumber"></param>
    /// <param name="dmsLookups">Pre-built lookup dictionaries for fast searching</param>
    /// <returns>Matching DMS record or null if no match found</returns>
    DmsExtract? FindMatch(string permitNumber, DmsLookupIndexes dmsLookups);
}