namespace WRADI.DocumentType.WrInspectionReport.Enums;

// T1 and T2 share an identical field and label set in the spec and cannot be told apart from the
// extracted text, so both classify as T1 - there is deliberately no T2 value. Impounding is a
// different licence type, kept separate so it is never folded into T1. NonStandardNarrative is
// T1-shaped but its general comments heading differs or is absent; added once T1 was landing at
// 86% of real documents against the client's own ~60% expectation. V3 is a structurally different
// form with its own label configuration.
public enum WrTemplateType
{
    Unknown,
    V3,
    T1,
    T4,
    T6,
    T7,
    Impounding,
    NonStandardNarrative
}