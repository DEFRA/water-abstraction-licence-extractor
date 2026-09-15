using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.Tools._2ndHalf.Configuration;

public static class WqFormLabelConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("SpecialTermsSingleLine", GetSpecialTermLabels()),
            ("SpecialTermsWholeBlock", GetSpecialTermWholeBlockLabels())
        ];
    }
    
    private static List<LabelToMatch> GetSpecialTermLabels()
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    new("If the measured Dry Weather Flow exceeds"),
                    new("The permitted Dry Weather Flow limit is set"),
                    new("For compliance with this permit, the measured Dry Weather Flow"),
                    new("For unusual rainfall to be considered, the operator shall notify")
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                IncludeStartLabelText = true,
                Format = "Text",
                PreviousLinesToFetch = 20,
                NextLinesToFetch = 20,
                Name = "SpecialTermsSingleLine"
            }
        ];
    }
    
    private static List<LabelToMatch> GetSpecialTermWholeBlockLabels()
    {
        return
        [
            new LabelToMatch
            {
                TextStart = 
                [
                    new("If the measured Dry Weather Flow exceeds the permitted Dry Weather Flow limit then the"),
                ],
                TextEnd = [
                    new("as part of the normal specified data returns.")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeStartLabelText = true,
                IncludeWholeLine = true,
                IncludeEndLabelText = true,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 20,
                Name = "SpecialTermsWholeBlock"
            }
        ];
    }
}