namespace WALE.ProcessFile.Models.Enums;

public enum LabelPosition
{
    ApplicableToMost,
    LabelIsBeforeTextToFind,
    LabelIsAfterTextToFind,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
    LabelIsInMiddleOfTextToFind,
    ContractIsSuccession,
    TextToFindIsBetweenLabels,
    RelatedCategoryPosition,
    SplitAtLabel,
    LabelIsActuallyResult
}