using System.Collections.Generic;
using System.Linq;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.Services.LicenceFinder.Helpers;

namespace WRADI.Services.LicenceFinder.Rules;

/// <summary>
/// Rule that matches NALD license numbers with DMS files in Application or Root folders with license-related names.
/// This rule has higher priority and focuses on official license documents.
/// </summary>
public class ApplicationOrRootFolderMatchRule : BaseRuleWithPriorityMatching
{
    public override int Priority => 2;

    protected override string GetDefaultRuleName() => "Found In Application Or Root Folder";

    protected override string GetRuleBaseName() => "Found In Application Or Root Folder";

    protected override List<DmsExtract> GetMatchingRecords(
        string permitNumber,
        DmsLookupIndexes dmsLookups)
    {
        var lowerCasePermitNumber = permitNumber.ToLowerInvariant();
        
        if (dmsLookups.ByPermitNumber.TryGetValue(lowerCasePermitNumber, out var matches))
        {
            return matches
                .Where(dms => RuleHelpers.IsInApplicationAssociatedDocsFolder(dms.FileUrl))
                .ToList();
        }
        
        return [];
    }
}