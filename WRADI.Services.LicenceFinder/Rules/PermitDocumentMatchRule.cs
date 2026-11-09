using System.Collections.Generic;
using System.Linq;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.Services.LicenceFinder.Helpers;

namespace WRADI.Services.LicenceFinder.Rules;

/// <summary>
/// Rule that matches NALD license numbers with DMS files in PermitDocuments folders with license-related names.
/// This rule has higher priority and focuses on official license documents.
/// </summary>
public class PermitDocumentMatchRule : BaseRuleWithPriorityMatching
{
    public override int Priority => 1;

    protected override string GetDefaultRuleName() => "Found In Permit Documents Folder";

    protected override string GetRuleBaseName() => "Found In Permit Documents Folder";

    protected override List<DmsExtract> GetMatchingRecords(
        string permitNumber,
        DmsLookupIndexes dmsLookups)
    {
        var lowerCasePermitNumber = permitNumber.ToLowerInvariant();

        if (dmsLookups.ByPermitNumber.TryGetValue(lowerCasePermitNumber, out var matches))
        {
            return matches
                .Where(dms => RuleHelpers.IsInPermitDocumentsFolder(dms.FileUrl))
                .ToList();
        }
        
        return [];
    }
}