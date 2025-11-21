namespace WALE.ProcessFile.Core.Enums;

public enum LabelPosition
{
    ApplicableToMost,
    AfterTextContainsAnotherMatch,
    Unknown,
    LabelIsBeforeTextToFind,
    LabelIsAfterTextToFind,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
    LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
    LabelIsInMiddleOfTextToFind,
    ContractIsSuccession,
    TextToFindIsBetweenLabels,
    RelatedCategoryPosition,
    Split,
    ActuallyLabel
}