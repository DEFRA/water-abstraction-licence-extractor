namespace WALE.ProcessFile.Models.Enums;

public enum MultipleBehaviour
{
    FindSingleInstanceOfLabelWithASingleValue,
    FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
    FindSingleInstanceOfLabelWithMultipleValues,
    FindMultipleInstancesOfLabelWithASingleValuePerLabel,
    FindMultipleInstancesOfLabelWithMultipleValuesPerLabel
}