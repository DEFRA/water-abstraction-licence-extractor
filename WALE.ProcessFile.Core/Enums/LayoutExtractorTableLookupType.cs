namespace WALE.ProcessFile.Core.Enums;

public enum LayoutExtractorTableLookupType
{
    Default, // Default is Grid
    Grid,
    FreeText,

    // Two columns, the label cell naming the field and the cell beside it holding the value.
    KeyValue
}