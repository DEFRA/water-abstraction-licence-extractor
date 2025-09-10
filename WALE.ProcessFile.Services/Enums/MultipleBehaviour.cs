namespace WALE.ProcessFile.Services.Enums;

public enum MultipleBehaviour
{
    FindSingleInstanceOfLabelWithASingleValue,
    FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
    FindSingleInstanceOfLabelWithMultipleValues,
    FindMultipleInstancesOfLabelWithASingleValuePerLabel,
    FindMultipleInstancesOfLabelWithMultipleValuesPerLabel
}