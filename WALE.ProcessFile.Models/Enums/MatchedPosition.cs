namespace WALE.ProcessFile.Models.Enums;

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