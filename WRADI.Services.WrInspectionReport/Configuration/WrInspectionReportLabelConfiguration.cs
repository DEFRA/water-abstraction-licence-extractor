using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WRADI.DocumentType.WrInspectionReport.Configuration;

public class WrInspectionReportLabelConfiguration
{
    // Real documents mark these checkbox-style fields with whatever the scanner/typist
    // used - tick, cross, or a Unicode box glyph - not just the Y/N the field nominally
    // asks for. Tried in this order; a genuinely-unticked box (☐) is itself a real answer
    // ("not confirmed"), not a missing one, so it's listed alongside the others rather
    // than treated as blank.
    private static readonly List<TextToMatch> CheckboxMarkPossibilities =
    [
        new("Y"), new("N"),
        new("✓"), new("☑"), new("☒"), new("☐"),
        new("X"), new("x")
    ];

    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            // Grid layout confirmed against real documents (row groupings, left-to-right):
            //   Row 1: Source of supply | Quantities | Land
            //   Row 2: Point of abstraction | Means of measurement | Charging factors
            //   Row 3: Means of abstraction | Records | Other provisions
            //   Row 4: Purposes | Provision of information
            //   Row 5: Period | Special conditions
            // Bounding each field to its row-neighbour keeps the same-line column walk from
            // pulling the next field's label text into this field's captured value. The last
            // field on each row is left unbounded (defaults to end-of-block).
            ("SourceOfSupply", GetInOrderField("Source of supply", "SourceOfSupply", "Quantities")),
            ("PointOfAbstraction", GetInOrderField("Point of abstraction", "PointOfAbstraction", "Means of measurement")),
            ("MeansOfAbstraction", GetInOrderField("Means of abstraction", "MeansOfAbstraction", "Records")),
            ("Purposes", GetInOrderField("Purpose(s)", "Purposes", "Provision of information")),
            ("Period", GetInOrderField("Period", "Period", "Special conditions")),
            ("Quantities", GetInOrderField("Quantities", "Quantities", "Land")),
            ("MeansOfMeasurement", GetInOrderField("Means of measurement", "MeansOfMeasurement", "Charging factors")),
            ("Records", GetInOrderField("R ecords", "Records", "Other provisions")),
            ("ProvisionOfInformation", GetInOrderField("Provision of information", "ProvisionOfInformation")),
            ("SpecialConditions", GetInOrderField("Special conditions", "SpecialConditions")),
            ("Land", GetInOrderField("Land (only if specified)", "Land")),
            ("ChargingFactors", GetInOrderField("Charging factors", "ChargingFactors")),
            ("OtherProvisions", GetInOrderField("Other provisions (specify below)", "OtherProvisions")),
            // Two distinct label wordings seen on real documents: the long parenthetical
            // form, and a plain "Licence No." (or "Licence No:") short form used on a
            // meaningful minority of documents. Both put the number in the same place
            // relative to the label, so only the label text itself differs.
            ("LicenceNumber", [
                ..TextToFindIsBetweenLabels(
                    "Licence No. (or Application No. or GIC No. etc.)",
                    "Inspection Class",
                    "LicenceNumber",
                    1,
                    LimitTo.SameColumn,
                    requireTextToClaimGroup: true), // Long form
                ..TextToFindIsBetweenLabels(
                    "Licence No",
                    "Inspection Class",
                    "LicenceNumber",
                    1,
                    LimitTo.SameColumn,
                    requireTextToClaimGroup: true) // Short form ("Licence No." / "Licence No:")
            ]),
            ("MetWith", TextAfterLabel("Met with", "MetWith", 0)),
            ("InspectingOfficer", TextAfterLabel("Inspecting Officer", "InspectingOfficer", 0)),
            ("SiteAddress", TextToFindIsBetweenLabels("Site address (if different)", "Met with", "SiteAddress", 1, LimitTo.SameColumn)),
            ("InspectionClass", TextToFindIsBetweenLabels("Inspection Class", "Telephone No", "InspectionClass", 1, LimitTo.SameColumn)),
            ("TelephoneNumber", TextToFindIsBetweenLabels("Telephone No", "Email", "TelephoneNumber", 2, LimitTo.SameColumn)),
            ("Position", TextToFindIsBetweenLabels("Position", "Inspection Date", "Position", 1, LimitTo.SameColumn)),
            ("Time", TextAfterLabel("Time", "Time", 0)),
            ("NameAndAddress", TextToFindIsBetweenLabels("Name and address", "Site address", "NameAndAddress", 7, LimitTo.SameColumn)),
            // Every alternate within a group keeps the group's own name (matching the
            // single-alternate fields below) - the converter looks results up by exact matched
            // label name, so an alternate named differently from its group (e.g. "MeterMakeT6")
            // is invisible to the converter even when it wins and captures a correct value.
            ("MeterMake", [
                ..TextToFindIsBetweenLabels("Meter make", "Reading:", "MeterMake", 1, LimitTo.SameColumn, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Meter Make", "Meter Serial Number", "MeterMake", 1, LimitTo.SameColumn, requireTextToClaimGroup: true) // T6 template
            ]),
            ("SerialNumber", [
                ..TextAfterLabel("Serial number", "SerialNumber", 0, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Meter Serial Number", "Meter Asset Number", "SerialNumber", 1, LimitTo.SameColumn, requireTextToClaimGroup: true) // T6 template
            ]),
            ("MeterAssetNumber", TextToFindIsBetweenLabels("Meter Asset Number", "Meter Reading", "MeterAssetNumber", 1, LimitTo.SameColumn)), // T6 template only
            ("Reading", [
                ..TextAfterLabel("Reading", "Reading", 0, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Meter Reading", "Flow Rate", "Reading", 1, LimitTo.SameColumn, requireTextToClaimGroup: true) // T6 template
            ]),
            ("FlowRate", TextToFindIsBetweenLabels("Flow Rate", "Calibration", "FlowRate", 1, LimitTo.SameColumn)), // T6 template only
            ("Units", TextAfterLabel("Units", "Units", 0)),
            ("Other", TextAfterLabel("Other:", "Other", 0)),
            ("CertificatesOfRecords", TextAfterLabel("Certificates or records available for", "CertificatesOfRecords", 0)),
            ("DateOfCertification", TextToFindIsBetweenLabels("Date of certificate or", "By whom", "DateOfCertification", 1, LimitTo.SameColumn, [new("record:"), new("Conformance:")])),
            ("Calibration", [
                ..TextAfterLabel("Calibration", "Calibration", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Conformance"), // New template
                ..TextToFindIsBetweenLabels("Calibration", "Conformance", "Calibration", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Calibration", "Verification", "Calibration", 1, LimitTo.SameColumn, requireTextToClaimGroup: true) // T6 template
            ]),
            ("Verification", TextToFindIsBetweenLabels("Verification", "Spot Check Result", "Verification", 1, LimitTo.SameColumn)), // T6 template only
            ("SpotCheckResult", TextToFindIsBetweenLabels("Spot Check Result", "General comments", "SpotCheckResult", 1, LimitTo.SameColumn, [new("–")])), // T6 template only
            ("Conformance", [
                ..TextAfterLabel("Conformance", "Conformance", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Flow verification"), // New template
                ..TextToFindIsBetweenLabels("Conformance", "Flow verification", "Conformance", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true)// Existing template
            ]),
            ("FlowVerification", [
                ..TextAfterLabel("Flow verification", "FlowVerification", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Meter verification"), // New template
                ..TextToFindIsBetweenLabels("Flow verification", "Meter verification", "FlowVerification", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true)// Existing template
            ]),
            ("MeterVerification", [
                ..TextAfterLabel("Meter verification", "MeterVerification", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Maintenance"), // New template
                ..TextToFindIsBetweenLabels("Meter verification", "record", "MeterVerification", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true)// Existing template
            ]),
            ("WhereKept", TextAfterLabel("Where kept", "WhereKept", 0)),
            ("FormSentTo", TextAfterLabel("Form sent to", "FormSentTo", 1)),
            ("Date", TextAfterLabel("Date:", "Date", 0)),
            ("DocumentTemplateVersion", TextAfterLabel("Document Template Version:", "DocumentTemplateVersion", 0)),
            ("DocumentHeader", TextAfterLabel("Form WR - ", "DocumentHeader", 0)),
            ("GeneralComments", TextToFindIsBetweenLabels(
                "General comments, details / dates of occupation changes, actions required etc.",
                "Form sent to",
                "GeneralComments",
                100,
                LimitTo.WholeLine)),
            ("MaintenanceLine", MaintenanceLine("Maintenance:", "Readings taken", "MaintenanceLine")),
            ("ReadingsTakenLine", MaintenanceLine("Readings taken:", "Where Kept", "ReadingsTakenLine")),
            ("InspectionDate", TextToFindIsBetweenLabels(
                "Inspection Date:",
                "Quantities",
                "InspectionDate",
                2,
                LimitTo.SameColumn,
                additionalSameLineEndTexts: ["Time:", "Inspecting Officer"])),
            ("Email", TextToFindIsBetweenLabels("Email", "Position:", "Email", 1, LimitTo.SameColumn)),
        ];
    }
    
    private static List<LabelToMatch> MaintenanceLine(string textStart, string textEnd, string name)
    {
        return
        [
            new LabelToMatch
            {
                TextStart =
                [
                    new(textStart)
                    {
                        LineMustStartWith = true
                    }
                ],
                TextEnd = 
                [
                    new(textEnd) { LineMustStartWith = true},
                    new("[END_OF_BLOCK]")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1,
                Name = name,
                IncludeStartLabelText = true,
                SubLabels =
                [
                    name == "MaintenanceLine"
                        ? TextAfterLabel("Maintenance:", $"{name}Maintenance", 0)[0]
                        : TextAfterLabel("Readings taken:", $"{name}ReadingsTaken", 0)[0],
                    name == "MaintenanceLine"
                        ? TextToFindIsBetweenLabels("Maintenance:", "N:", $"{name}MaintenanceYes", 0, LimitTo.WholeLine, possibilities: [new("✓"), new("X")])[0]
                        : TextToFindIsBetweenLabels("Readings taken:", "N:", $"{name}ReadingsTakenYes", 0, LimitTo.WholeLine, possibilities: [new("✓"), new("X")])[0],
                    name == "MaintenanceLine"
                        ? TextToFindIsBetweenLabels("N:", "Frequency:", $"{name}MaintenanceNo", 0, LimitTo.WholeLine, possibilities: [new("✓"), new("X")])[0]
                        : TextToFindIsBetweenLabels("N:", "Frequency:", $"{name}ReadingsTakenNo", 0, LimitTo.WholeLine, possibilities: [new("✓"), new("X")])[0],
                    TextAfterLabel("Frequency:", $"{name}Frequency", 0)[0],
                    TextAfterLabel("By whom:", $"{name}ByWhom", 0)[0]
                ]
            }
        ];
    }
    
    private static List<LabelToMatch> TextToFindIsBetweenLabels(
        string startText,
        string endText,
        string name,
        int nextLines,
        LimitTo limitTo,
        List<TextToMatch>? additionalRemoves = null,
        List<TextToMatch>? possibilities = null,
        bool requireTextToClaimGroup = false,
        List<string>? additionalSameLineEndTexts = null)
    {
        return
        [
            new LabelToMatch
            {
                TextStart =
                [
                    new(startText)
                    {
                        ColumnMustStartWith = true
                    }
                ],
                TextEnd =
                [
                    new(endText) { LineMustStartWith = true},
                    // Bounds the same-line column walk (LimitTo.SameColumn) so it stops
                    // at the next field's own column instead of sweeping it in as this
                    // field's value - needed when the real end marker (above) is a
                    // distant row header rather than something that ever appears on this
                    // label's own line. See InspectionDate: "Time:" sits in the column
                    // right after "Inspection Date:" on the same row but belongs to a
                    // different field entirely.
                    ..(additionalSameLineEndTexts ?? []).Select(t => new TextToMatch(t)),
                    new("[END_OF_BLOCK]")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = nextLines,
                LimitTo = limitTo,
                Name = name,
                Remove = [
                    new(startText), // TODO not sure why we have to add this, we dont always have to with betweens - probably because of the column limiting
                    ..additionalRemoves ?? []
                ],
                Possibilities = possibilities,
                RequireTextToClaimGroup = requireTextToClaimGroup
            }
        ];
    }
    
    /*private static List<LabelToMatch> TextAfterLabelWithSpecifiedColumn(
        string text,
        string labelName,
        int nextLinesToFetch,
        int columnIndex,
        string[] mustContain)
    {
        var label = TextAfterLabel(text, labelName, nextLinesToFetch, []);
        label[0].LimitTo = LimitTo.SpecifiedColumn;
        label[0].LimitToColumnIndex = columnIndex;
        label[0].MustContain = mustContain;
        
        return label;
    }*/
    
    private static List<LabelToMatch> TextAfterLabel(
        string text,
        string labelName,
        int nextLinesToFetch,
        List<TextToMatch>? additionalRemoves = null,
        List<TextToMatch>? possibilities = null,
        bool requireTextToClaimGroup = false,
        string? endText = null)
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    new(text)
                    {
                        ColumnMustStartWith = true
                    }
                ],
                // LimitTo.SameColumn without a TextEnd walks to the end of the line with no
                // bound - fine for a field that's alone on its line, but on a densely packed
                // row (e.g. "Calibration: Conformance: Flow verification: Meter verification:")
                // it swallows every field after this one. Set endText to bound it to the next
                // field on the same row.
                TextEnd = endText != null ? [new(endText) { LineMustStartWith = true }] : null,
                Position = LabelPosition.LabelIsBeforeTextToFind,
                LimitTo = LimitTo.SameColumn,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = nextLinesToFetch,
                Name = labelName,
                RequireTextToClaimGroup = requireTextToClaimGroup,
                Remove = [
                    new(text),
                    ..additionalRemoves ?? []
                ],
                Possibilities = possibilities
            }
        ];
    }
    
    private static List<LabelToMatch> GetInOrderField(string text, string labelName, string? endText = null)
    {
        return
        [
            new LabelToMatch
            {
                TextStart =
                [
                    new(text) { ColumnMustStartWith = true },
                    new(text.Replace(" ", string.Empty)) { ColumnMustStartWith = true }
                ],
                TextEnd = endText != null
                    ? [new(endText) { LineMustStartWith = true }, new("[END_OF_BLOCK]")]
                    : [new("[END_OF_BLOCK]")],
                // Routed via TextToFindIsBetweenLabels (not LabelIsBeforeTextToFind) - the
                // generic-text path in ApplicableToMost.cs only ever reads the label's own
                // captured column and discards the whole result if nothing is left after
                // removing the label text, which silently swallows every genuinely-blank tick
                // field. This position uses a separate path that doesn't have that gate.
                Position = LabelPosition.TextToFindIsBetweenLabels,
                LimitTo = LimitTo.SameColumn,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1,
                Name = labelName,
                Remove = [
                    new(text) // Gets rid of issue of finding 'in' in 'Points'
                ],
                Possibilities = [
                    new TextToMatch("N/A"),
                    new TextToMatch("Not"),
                    new TextToMatch("In"),
                    new TextToMatch("✓"),
                    new TextToMatch("X"),
                    new TextToMatch("Y"), // T6 template uses Y/N instead of In/Not/tick/cross
                    new TextToMatch("N"),
                    // Catch-all, tried last: a genuinely blank tick field (very common - not every
                    // provision gets marked) must still survive as a match so the converter can
                    // classify it as Blank, rather than the whole result being discarded and
                    // becoming indistinguishable from the label never being found at all.
                    new TextToMatch("")
                ]
            }
        ];
    }
}

