namespace WALE.ProcessFile.Services.Enums;

public enum LabelPosition
{
    ApplicableToMost,
    Unknown,
    LabelIsBeforeTextToFind,
    LabelIsAfterTextToFind,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
    LabelIsInMiddleOfTextToFind,
    ContractIsSuccession,
    TextToFindIsBetweenLabels,
    RelatedCategoryPosition,
    Split
}