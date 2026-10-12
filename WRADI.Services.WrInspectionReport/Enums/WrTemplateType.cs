namespace WRADI.DocumentType.WrInspectionReport.Enums;

// Matches the template families named in the client's own TemplateSpec_v5.0.xlsx (sheets T1,
// T2, T4, T6, T7 - T3/T5 don't exist in that spec, not a gap here). T1 and T2 share an
// identical field/label set in the spec and are not distinguishable from the extracted text
// alone (checked: the one candidate discriminator, Email appearing before vs after Telephone
// No, is present in under 1% of the real corpus) - classified together as T1, which is what the
// client is actually prioritising. Impounding is a genuinely different licence type (a 3-row
// "Description of inland water to be impounded"/"Point of Impoundment"/"Further Conditions"
// grid, found while hand-labelling the golden set) that doesn't match any of the spec's five
// templates at all - kept as its own value so it's never silently folded into T1.
//
// NonStandardNarrative: not one of the spec's named templates - a document that's otherwise
// T1-shaped (has the standard "Form WR - 51" header and grid) but whose GeneralComments section
// uses a different heading than T1's own literal spec text, or none at all (a narrative that
// just starts directly, or a multi-section long-form report). v1 of this classifier folded all
// of these into T1 by default; tightened in a second pass once cross-checking against the
// hand-labelled golden set's own documentShape tags showed T1 landing at 86% of the corpus
// against the client's own ~60% expectation - see WrInspectionReportSchemaConverter.
// ClassifyTemplate for the exact markers used to detect this bucket, and note that several other
// documentShape tags found while labelling (all_blank_provisions, remote_meeting,
// no_telephone_field, compliance_only, temporal_meter_change, struck_through_measurement_section)
// are deliberately NOT treated as anomalies here - they're just content variation within a
// genuinely T1-shaped document, not a different comments-section structure.
// V3: the redesigned "Assessment report" form (Document Template Version 2026_08_04_V3),
// structurally a different document from every value above rather than another family within the
// same form. It drops the "Form WR - 51" header for "Form WR51", renames nearly every field
// ("Person met with/contacted" for MetWith, "Assessment date" for InspectionDate), splits the old
// combined name-and-address into two fields, adds fields with no v1 equivalent (Licence
// criticality, Site contact number/email, Job title or role, separate inspection start and end
// times), and replaces the provisions grid with a condition-to-outcome list. Its rules therefore
// live in their own standalone configuration rather than filtering the v1 set, because almost no
// label text is shared.
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