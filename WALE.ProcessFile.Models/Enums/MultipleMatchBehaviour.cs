namespace WALE.ProcessFile.Models.Enums;

public enum MultipleMatchBehaviour
{
    FindSingleInstanceOfLabelWithASingleValue,
    FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
    FindSingleInstanceOfLabelWithMultipleValues,
    FindMultipleInstancesOfLabelWithASingleValuePerLabel,
    FindMultipleInstancesOfLabelWithMultipleValuesPerLabel
}