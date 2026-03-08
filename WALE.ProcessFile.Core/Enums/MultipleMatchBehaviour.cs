namespace WALE.ProcessFile.Core.Enums;

public enum MultipleMatchBehaviour
{
    FindSingleInstanceOfLabelWithASingleValue,
    FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
    FindSingleInstanceOfLabelWithMultipleValues,
    FindMultipleInstancesOfLabelWithASingleValuePerLabel,
    FindMultipleInstancesOfLabelWithMultipleValuesPerLabel
}