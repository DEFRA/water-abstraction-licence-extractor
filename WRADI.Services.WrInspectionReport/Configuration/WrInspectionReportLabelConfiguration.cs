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

    // The "grid template" layout prints "Calibration: Conformance: Flow verification: Meter
    // verification:" as one label row, with the four answers on the row directly below (see
    // Calibration/Conformance/FlowVerification/MeterVerification below). On a meaningful
    // fraction of real documents the same-column/next-line match for one of these four lands
    // on a NEIGHBOURING field's row instead of its own - e.g. Calibration's grid alternate
    // capturing "Maintenance:" or "Frequency:" as if it were the answer. Adding these as
    // IgnoreBlockIfContains rejects the whole match outright when that happens, so the field
    // ends up genuinely unmatched (blank) instead of silently showing another field's label
    // text as if it were real data - a strictly better outcome even though it doesn't recover
    // the actual answer. Measured against the 789-file real corpus: without this, 43-47% of
    // these four fields showed a leaked label instead of a real value.
    private static readonly List<string> VerificationGridSiblingLeakTerms =
    [
        "Calibration:", "Conformance:", "Flow verification:", "Meter verification:",
        "Maintenance:", "Frequency:", "Spot Check Result", "General comments"
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
            // "Records" sometimes renders as "R ecords" (a space after the R - the same PDF
            // kerning quirk the Records field's own TextStart already has to work around
            // below). Without it as an alternate end marker here, the same-line column walk
            // never recognises the boundary and sweeps all the way to "...N/A" at the end of
            // the row - which then wins over the real "In Order" answer earlier in the swept
            // text, since Possibilities checks "N/A" before "In".
            ("MeansOfAbstraction", GetInOrderField(
                "Means of abstraction",
                "MeansOfAbstraction",
                "Records",
                additionalEndTexts: ["R ecords"])),
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
            // "Site address (if different): X | Email:" sits on one row (two columns) on a
            // meaningful fraction of real documents - without bounding the same-line walk
            // there, it sweeps past "Email:" and that label ends up glued onto the end of the
            // captured address (206/789 real corpus files affected). Same shape as the
            // NameAndAddress/Telephone No fix above.
            ("SiteAddress", TextToFindIsBetweenLabels("Site address (if different)", "Met with", "SiteAddress", 1, LimitTo.SameColumn, additionalSameLineEndTexts: ["Email"])),
            ("InspectionClass", TextToFindIsBetweenLabels("Inspection Class", "Telephone No", "InspectionClass", 1, LimitTo.SameColumn)),
            ("TelephoneNumber", TextToFindIsBetweenLabels("Telephone No", "Email", "TelephoneNumber", 2, LimitTo.SameColumn)),
            ("Position", TextToFindIsBetweenLabels("Position", "Inspection Date", "Position", 1, LimitTo.SameColumn)),
            ("Time", TextAfterLabel("Time", "Time", 0)),
            // "Name and address: | Telephone No:" sits on one row (two columns) - without
            // bounding the same-line walk there, it sweeps past "Telephone No:" and the
            // phone number's own label ends up as a bogus extra first line of the address.
            ("NameAndAddress", TextToFindIsBetweenLabels("Name and address", "Site address", "NameAndAddress", 7, LimitTo.SameColumn, additionalSameLineEndTexts: ["Telephone No"])),
            // Every alternate within a group keeps the group's own name (matching the
            // single-alternate fields below) - the converter looks results up by exact matched
            // label name, so an alternate named differently from its group (e.g. "MeterMakeT6")
            // is invisible to the converter even when it wins and captures a correct value.
            // T6 only - a "Meter Name" row (own line, value on the same row) sits directly
            // above "Meter Make" in this template's vertical field grid. Present on ~4-5% of
            // the real corpus (34/789 pages) and not captured by any existing label group.
            ("MeterName", TextToFindIsBetweenLabels("Meter Name", "Meter Make", "MeterName", 1, LimitTo.SameColumn)), // T6 template only
            ("MeterMake", [
                // "Meter make: X Serial number: Y Reading: Z" is one row - Reading: is the
                // real (possibly multi-line) end marker, but Serial number: is the immediate
                // same-line neighbour and needs to bound the same-line column walk too,
                // otherwise it sweeps straight through into the serial number.
                ..TextToFindIsBetweenLabels("Meter make", "Reading:", "MeterMake", 1, LimitTo.SameColumn, requireTextToClaimGroup: true, additionalSameLineEndTexts: ["Serial number"]), // Existing template
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
            // A fourth layout beyond New/Existing/T6: "Calibration: Conformance: Flow
            // verification: Meter verification:" as one row of labels, with the four
            // answers ("Yes No Yes Yes") on the row directly below, same columns. The
            // anchor row-grouping keeps that value row separate from the label row (see
            // PdfPigNoOcrDataExtractorService), so a SameColumn/NextLinesToFetch:1 alternate
            // finds it correctly - it just also needs bounding on the label's own row so the
            // same-line column walk doesn't sweep the next field's label in before it ever
            // gets to the next line.
            ("Calibration", [
                ..TextAfterLabel("Calibration", "Calibration", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Conformance"), // New template
                ..TextToFindIsBetweenLabels("Calibration", "Conformance", "Calibration", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Calibration", "Verification", "Calibration", 1, LimitTo.SameColumn, requireTextToClaimGroup: true, additionalSameLineEndTexts: ["Conformance"], ignoreBlockIfContains: [..VerificationGridSiblingLeakTerms, "Certificate"]) // T6 template
            ]),
            ("Verification", TextToFindIsBetweenLabels("Verification", "Spot Check Result", "Verification", 1, LimitTo.SameColumn)), // T6 template only
            ("SpotCheckResult", TextToFindIsBetweenLabels("Spot Check Result", "General comments", "SpotCheckResult", 1, LimitTo.SameColumn, [new("–")])), // T6 template only
            ("Conformance", [
                ..TextAfterLabel("Conformance", "Conformance", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Flow verification"), // New template
                ..TextToFindIsBetweenLabels("Conformance", "Flow verification", "Conformance", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Conformance", "Flow verification", "Conformance", 1, LimitTo.SameColumn, requireTextToClaimGroup: true, ignoreBlockIfContains: VerificationGridSiblingLeakTerms) // Grid template (label row + value row below)
            ]),
            ("FlowVerification", [
                ..TextAfterLabel("Flow verification", "FlowVerification", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Meter verification"), // New template
                ..TextToFindIsBetweenLabels("Flow verification", "Meter verification", "FlowVerification", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Flow verification", "Meter verification", "FlowVerification", 1, LimitTo.SameColumn, requireTextToClaimGroup: true, ignoreBlockIfContains: VerificationGridSiblingLeakTerms) // Grid template (label row + value row below)
            ]),
            ("MeterVerification", [
                ..TextAfterLabel("Meter verification", "MeterVerification", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Maintenance"), // New template
                ..TextToFindIsBetweenLabels("Meter verification", "record", "MeterVerification", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Meter verification", "record", "MeterVerification", 1, LimitTo.SameColumn, requireTextToClaimGroup: true, ignoreBlockIfContains: VerificationGridSiblingLeakTerms) // Grid template (label row + value row below)
            ]),
            ("WhereKept", TextAfterLabel("Where kept", "WhereKept", 0)),
            // "Form sent to: | Date:" on one row (two columns), with the actual recipient
            // on the row below, same column as "Form sent to:".
            // "Form sent to: | Date:" on one row (two columns), with the actual recipient
            // on the row below, same column as "Form sent to:". Routed via
            // TextToFindIsBetweenLabels rather than TextAfterLabel/LabelIsBeforeTextToFind -
            // that position's handler doesn't correctly follow through to the next-line,
            // same-column value here for reasons not fully root-caused; every other
            // "value on the row below" fix this session went through
            // TextToFindIsBetweenLabels instead, and that same swap fixes this too.
            ("FormSentTo", TextToFindIsBetweenLabels("Form sent to", "Date", "FormSentTo", 1, LimitTo.SameColumn)),
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
                // LimitTo.SameColumn (rather than the WholeLine default) matters beyond
                // just this match: WholeLine's capture mechanism flattens the row's real,
                // already-correctly-split columns ("Maintenance:" | "Yes" / "Frequency:" |
                // "Daily" / "By" | "whom:" | "JP") into one synthetic single-column blob.
                // SubLabels below rely on TextAfterLabel, which requires its own text to
                // start a column - against a flattened blob only the very first sub-label
                // can ever satisfy that, so the other four silently never match at all.
                // SameColumn preserves the row's real column boundaries instead.
                LimitTo = LimitTo.SameColumn,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1,
                Name = name,
                IncludeStartLabelText = true,
                // Two layouts share this row: an older "Y: N:" tick-box style, and a plainer
                // "Maintenance: Yes Frequency: Daily By whom: JP" style with the answer as a
                // literal word. The plain-word sub-label (index 0) needs its own end bound
                // (endText: "Frequency") or it swallows the rest of the line unbounded,
                // leaving nothing for the Frequency/ByWhom sub-labels to match against -
                // same shape as the same-line-column-walk fixes elsewhere in this file, just
                // via TextAfterLabel's own endText rather than LimitTo.SameColumn.
                SubLabels =
                [
                    name == "MaintenanceLine"
                        ? TextAfterLabel("Maintenance:", $"{name}Maintenance", 0, endText: "Frequency")[0]
                        : TextAfterLabel("Readings taken:", $"{name}ReadingsTaken", 0, endText: "Frequency")[0],
                    name == "MaintenanceLine"
                        ? TextToFindIsBetweenLabels("Maintenance:", "N:", $"{name}MaintenanceYes", 0, LimitTo.WholeLine, possibilities: [new("✓"), new("X")])[0]
                        : TextToFindIsBetweenLabels("Readings taken:", "N:", $"{name}ReadingsTakenYes", 0, LimitTo.WholeLine, possibilities: [new("✓"), new("X")])[0],
                    name == "MaintenanceLine"
                        ? TextToFindIsBetweenLabels("N:", "Frequency:", $"{name}MaintenanceNo", 0, LimitTo.WholeLine, possibilities: [new("✓"), new("X")])[0]
                        : TextToFindIsBetweenLabels("N:", "Frequency:", $"{name}ReadingsTakenNo", 0, LimitTo.WholeLine, possibilities: [new("✓"), new("X")])[0],
                    TextAfterLabel("Frequency:", $"{name}Frequency", 0, endText: "By whom")[0],
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
        List<string>? additionalSameLineEndTexts = null,
        List<string>? ignoreBlockIfContains = null)
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
                RequireTextToClaimGroup = requireTextToClaimGroup,
                IgnoreBlockIfContains = ignoreBlockIfContains
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
    
    private static List<LabelToMatch> GetInOrderField(
        string text,
        string labelName,
        string? endText = null,
        List<string>? additionalEndTexts = null)
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
                    ? [
                        new(endText) { LineMustStartWith = true },
                        ..(additionalEndTexts ?? []).Select(t => new TextToMatch(t) { LineMustStartWith = true }),
                        new("[END_OF_BLOCK]")
                    ]
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

