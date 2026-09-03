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
    // verification:" as one label row. The comment on this alternate originally assumed the
    // four answers sit on the row directly below - checked against several real documents (via
    // the PdfPig cache text directly) and that's not what's there: the row immediately below
    // this label row is consistently "Maintenance:"'s own row (its own "Y:"/"N:" sub-fields),
    // with no dedicated value row for these four fields in between at all. The same-column/
    // next-line match, having nothing genuine to find, latches onto whichever of Maintenance's
    // columns is nearest in X to each field's own label - e.g. Calibration ends up reading
    // "Maintenance:" or "Frequency:" itself, and Conformance/FlowVerification end up reading
    // Maintenance's own "Y:"/"N:" (sometimes merged with its own tick mark into something that
    // even LOOKS like a plausible compound answer, e.g. "Y: ✓ N:", but isn't - it's still
    // Maintenance's row, not this field's).
    //
    // Two complementary guards handle this:
    //  - IgnoreBlockIfContains (below) rejects the match after a column has already been
    //    picked, if its content contains a recognisable sibling label - the right guard when
    //    the picked column names another field (e.g. "Frequency:").
    //  - ExcludeNextLineIfFirstColumnStartsWith("Maintenance"), set individually on each of the
    //    four alternates, rejects the whole next-line candidate before any column is picked
    //    from it, if that row's own leading column is "Maintenance:" - the guard needed for
    //    Conformance/FlowVerification/MeterVerification, since a leaked "Y:"/"N:" (bare or
    //    merged with a tick) doesn't contain any sibling label text and so wouldn't otherwise
    //    be caught, and can't safely be blocked by content alone without also rejecting this
    //    same field's genuine compound answer if that shape is ever found elsewhere.
    //
    // Either way, the field ends up genuinely unmatched (blank) instead of silently showing
    // another field's data as if it were a real answer - a strictly better outcome even though
    // it doesn't recover the actual value. Measured against the 789-file real corpus.
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
            // "Inspection Class:" is the real (bounded) end marker, but on 197/789 real
            // corpus files the next-line same-column fetch also swept in the row below -
            // "Name and address: <holder>" - because that row happens to share the licence
            // number's own column X position. additionalSameLineEndTexts feeds into
            // GetTextBetween's cross-line end-tag check too (not just same-row bounding), so
            // listing the variant wordings here (seen across templates, per TemplateSpec)
            // stops that row's content from being swept in as part of the licence number.
            ("LicenceNumber", [
                ..TextToFindIsBetweenLabels(
                    "Licence No. (or Application No. or GIC No. etc.)",
                    "Inspection Class",
                    "LicenceNumber",
                    1,
                    LimitTo.SameColumn,
                    requireTextToClaimGroup: true,
                    additionalSameLineEndTexts: ["Name and address", "Name / address"]), // Long form
                ..TextToFindIsBetweenLabels(
                    "Licence No",
                    "Inspection Class",
                    "LicenceNumber",
                    1,
                    LimitTo.SameColumn,
                    requireTextToClaimGroup: true,
                    additionalSameLineEndTexts: ["Name and address", "Name / address"]) // Short form ("Licence No." / "Licence No:")
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
            // Same issue for "Email:", which sits on the header row for some templates and
            // otherwise bleeds into the address block's last captured line (mirrors the same
            // sibling-label leak already fixed for SiteAddress). NextLinesToFetch bumped from
            // 7 to 10: real addresses regularly wrap to 8 lines (business name + 5-6 address
            // lines + postcode on its own line) and 7 was silently dropping the final line -
            // usually the postcode.
            ("NameAndAddress", [
                ..TextToFindIsBetweenLabels("Name and address", "Site address", "NameAndAddress", 10, LimitTo.SameColumn, additionalSameLineEndTexts: ["Telephone No", "Email"]), // Existing template
                // "Water Company" template: label and value are one line, e.g. "Name / address:
                // Sutton and East Surrey Water PLC, London Road, Redhill, RH1 1LJ" - no separate
                // value block on subsequent lines, so the between-labels walk above never finds
                // anything (it only looks at lines after the label's own line). Seen with both
                // "/" and "&" between "Name" and "address".
                ..TextAfterLabel("Name / address", "NameAndAddress", 0), // Water Company template
                ..TextAfterLabel("Name & address", "NameAndAddress", 0), // Water Company template
                // Another distinct template: labelled "Permit holder name and address:" instead
                // of "Name and address:", value starts on the label's own line and wraps onto
                // exactly one continuation line, with "Telephone No:" appended straight after the
                // value on the label's own line (same-line sibling, no line break) rather than
                // sitting in its own column - stripped via Remove since TextAfterLabel's endText
                // bound only matches text that starts a line, which "Telephone No:" here doesn't.
                ..TextAfterLabel("Permit holder name and address", "NameAndAddress", 1, additionalRemoves: [new TextToMatch("Telephone No:")]) // Permit holder template
            ]),
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
                ..TextToFindIsBetweenLabels("Calibration", "Verification", "Calibration", 1, LimitTo.SameColumn, requireTextToClaimGroup: true, additionalSameLineEndTexts: ["Conformance"], ignoreBlockIfContains: [..VerificationGridSiblingLeakTerms, "Certificate"], excludeNextLineIfFirstColumnStartsWith: ["Maintenance"]) // T6 template
            ]),
            ("Verification", TextToFindIsBetweenLabels("Verification", "Spot Check Result", "Verification", 1, LimitTo.SameColumn)), // T6 template only
            ("SpotCheckResult", TextToFindIsBetweenLabels("Spot Check Result", "General comments", "SpotCheckResult", 1, LimitTo.SameColumn, [new("–")])), // T6 template only
            ("Conformance", [
                ..TextAfterLabel("Conformance", "Conformance", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Flow verification"), // New template
                ..TextToFindIsBetweenLabels("Conformance", "Flow verification", "Conformance", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Conformance", "Flow verification", "Conformance", 1, LimitTo.SameColumn, requireTextToClaimGroup: true, ignoreBlockIfContains: VerificationGridSiblingLeakTerms, excludeNextLineIfFirstColumnStartsWith: ["Maintenance"]) // Grid template (label row + value row below)
            ]),
            ("FlowVerification", [
                ..TextAfterLabel("Flow verification", "FlowVerification", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Meter verification"), // New template
                ..TextToFindIsBetweenLabels("Flow verification", "Meter verification", "FlowVerification", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Flow verification", "Meter verification", "FlowVerification", 1, LimitTo.SameColumn, requireTextToClaimGroup: true, ignoreBlockIfContains: VerificationGridSiblingLeakTerms, excludeNextLineIfFirstColumnStartsWith: ["Maintenance"]) // Grid template (label row + value row below)
            ]),
            ("MeterVerification", [
                ..TextAfterLabel("Meter verification", "MeterVerification", 1, possibilities: [new("Yes"), new("No")], requireTextToClaimGroup: true, endText: "Maintenance"), // New template
                ..TextToFindIsBetweenLabels("Meter verification", "record", "MeterVerification", 0, LimitTo.WholeLine, possibilities: CheckboxMarkPossibilities, requireTextToClaimGroup: true), // Existing template
                ..TextToFindIsBetweenLabels("Meter verification", "record", "MeterVerification", 1, LimitTo.SameColumn, requireTextToClaimGroup: true, ignoreBlockIfContains: VerificationGridSiblingLeakTerms, excludeNextLineIfFirstColumnStartsWith: ["Maintenance"]) // Grid template (label row + value row below)
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
        List<string>? ignoreBlockIfContains = null,
        List<string>? excludeNextLineIfFirstColumnStartsWith = null)
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
                IgnoreBlockIfContains = ignoreBlockIfContains,
                ExcludeNextLineIfFirstColumnStartsWith = excludeNextLineIfFirstColumnStartsWith
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
                    // ExceptWhenInsideWord: these are short enough ("In", "N", "X"...) that
                    // they're common substrings of unrelated text - most often a neighbouring
                    // field's own label swept in by a separate next-line column-matching bug
                    // (e.g. "Point of abstraction:" coincidentally contains "in"). Without this,
                    // that produces a fabricated InOrder/NotInOrder verdict instead of an honest
                    // gap.
                    new TextToMatch("N/A") { ExceptWhenInsideWord = true },
                    new TextToMatch("Not") { ExceptWhenInsideWord = true },
                    new TextToMatch("In") { ExceptWhenInsideWord = true },
                    new TextToMatch("✓") { ExceptWhenInsideWord = true },
                    // Same tick, different glyph: real WR51 PDFs render the "in order" mark
                    // with whichever tick character the originating Word/export toolchain
                    // happened to use, not consistently ✓. Confirmed by scanning the full real
                    // corpus for every symbol appearing directly after a LicenceProvisions
                    // label - each of these appears exclusively in that tick position, never
                    // near "X"/negative language: ✔ (206 occurrences), √ (286), 🗸 (30), plus
                    // four embedded Wingdings-style Private Use Area glyphs (U+F0FC/391,
                    // U+F061/76, U+F050/61, U+F072/4) - the same font family behind Wingdings'
                    // very well-known "tick mark" mapping. Before this fix, any document
                    // using one of these instead of ✓ returned a completely empty
                    // LicenceProvisions grid (all 8 fields DidntMatch/Blank), not just a wrong
                    // mark on one field - across ~1,100 real occurrences corpus-wide.
                    new TextToMatch("✔") { ExceptWhenInsideWord = true },
                    new TextToMatch("√") { ExceptWhenInsideWord = true },
                    new TextToMatch("🗸") { ExceptWhenInsideWord = true },
                    new TextToMatch("") { ExceptWhenInsideWord = true },
                    new TextToMatch("") { ExceptWhenInsideWord = true },
                    new TextToMatch("") { ExceptWhenInsideWord = true },
                    new TextToMatch("") { ExceptWhenInsideWord = true },
                    new TextToMatch("X") { ExceptWhenInsideWord = true },
                    // Same "not in order" cross, different glyph - same evidence-gathering
                    // approach as the tick variants above (☒ 52 occurrences, × 6).
                    new TextToMatch("☒") { ExceptWhenInsideWord = true },
                    new TextToMatch("×") { ExceptWhenInsideWord = true },
                    new TextToMatch("Y") { ExceptWhenInsideWord = true }, // T6 template uses Y/N instead of In/Not/tick/cross
                    new TextToMatch("N") { ExceptWhenInsideWord = true },
                    // Catch-all, tried last: a genuinely blank tick field (very common - not every
                    // provision gets marked) must still survive as a match so the converter can
                    // classify it as Blank, rather than the whole result being discarded and
                    // becoming indistinguishable from the label never being found at all. Left
                    // unflagged - "inside word" isn't a meaningful question for an empty string,
                    // and it's already handled separately (see RestrictToPossibility's zero-lines
                    // fallback).
                    new TextToMatch("")
                ]
            }
        ];
    }
}

