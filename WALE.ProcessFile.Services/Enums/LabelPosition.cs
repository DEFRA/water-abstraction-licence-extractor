namespace WALE.ProcessFile.Services.Enums;

public enum LabelPosition
{
    ApplicableToMost,
    LabelIsBeforeTextToFind,
    LabelIsAfterTextToFind,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
    ContractIsSuccession,
    TextToFindIsBetweenLabels,
    RelatedCategoryPosition,
    Split
}