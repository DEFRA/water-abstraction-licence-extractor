namespace WALE.ProcessFile.Core.Enums;

public enum MatchedPosition
{
    Unknown,
    NotApplicable,
    NotFound,
    FullyOnSameLine,
    PartiallyOnSameLine,
    OnSameLineSingleWord,
    OnOrNearPreviousLine,
    OnOrNearNextLine,
    EitherSideOfLabel,
    BetweenLabels
}