using System.Collections.Generic;
using WALE.ProcessFile.Core.Models.Dms;

namespace WRADI.Services.LicenceFinder.Rules;

/// <summary>
/// Rule that matches NALD license numbers with DMS files using manual folder mapping.
/// This rule uses the ManualFixExtract data to find documents in specific folders.
/// Has the lowest priority as it's used for manual overrides.
/// </summary>
public class ManualFolderFileNameMatchRule : BaseRuleWithPriorityMatching
{
    public override int Priority => 6; // Lowest priority - used as last resort

    protected override string GetDefaultRuleName() => "Manual Folder Match Fix - In Non-Primary Folder";

    protected override string GetRuleBaseName() => "Manual Folder Match Fix- In Non-Primary Folder";

    protected override List<DmsExtract> GetMatchingRecords(
        string permitNumber,
        DmsLookupIndexes dmsLookups)
    {
        var lowerCasePermitNumber = permitNumber.ToLowerInvariant();
        
        if (dmsLookups.ByManualFixPermitNumber.TryGetValue(lowerCasePermitNumber, out var matches))
        {
            return matches;
        }
        
        return [];
    }
}