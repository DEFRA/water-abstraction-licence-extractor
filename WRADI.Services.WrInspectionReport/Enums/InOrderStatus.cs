namespace WRADI.DocumentType.WrInspectionReport.Enums;

public enum InOrderStatus
{
    DidntMatch,
    Unknown,
    Blank,
    NotApplicable,
    InOrder,
    NotInOrder,
    // "NI" - a distinct, genuine answer seen on a real WR51 form (85 occurrences across 25
    // documents), meaning the provision was not assessed during this inspection - different
    // from Blank (nothing written) and NotApplicable (the provision doesn't apply to this
    // licence).
    NotInspected
}