namespace WALE.ProcessFile.Services.Enums;

public enum LabelPosition
{
    ApplicableToAll,
    LabelIsBeforeTextToFind,
    LabelIsAfterTextToFind,
    LabelIsInMiddleOfTextToFind,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
    ContractIsSuccession,
    TextToFindIsBetweenLabels,
    RelatedCategoryPosition,
    Split
}