using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Tests.Helper;

public static class GeneralTestsHelper
{
    public static List<LabelGroupResult> ExcludeSomeMatches(List<LabelGroupResult> matches)
    {
        return matches.Where(m =>
            m.LabelGroupName != "LinkedLicenceNumber"
            && m.LabelGroupName != "ReasonsForConditions"
            && m.LabelGroupName != "ScheduleOfConditionsA"
            && m.LabelGroupName != "ScheduleOfConditionsB").ToList();
    }
}