using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Models.Configuration;

namespace WRADI.DocumentType.WrInspectionReport.Configuration;

public static class WrInspectionReportLabelConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels() =>
    [
        RuleSourceOfSupply(),
        RulePointOfAbstraction(),
        RuleMeansOfAbstraction(),
        RulePurposes(),
        RulePeriod(),
        RuleQuantities(),
        RuleMeansOfMeasurement(),
        RuleRecords(),
        RuleProvisionOfInformation(),
        RuleSpecialConditions(),
        RuleLand(),
        RuleChargingFactors(),
        RuleOtherProvisions(),
        RuleLicenceNumber(),
        RuleMetWith(),
        RuleInspectingOfficer(),
        RuleSiteAddress(),
        RuleInspectionClass(),
        RuleTelephoneNumber(),
        RulePosition(),
        RuleTime(),
        RuleNameAndAddress(),
        RuleMeterName(),
        RuleMeterMake(),
        RuleSerialNumber(),
        RuleMeterAssetNumber(),
        RuleReading(),
        RuleFlowRate(),
        RuleUnits(),
        RuleOther(),
        RuleCertificatesOfRecords(),
        RuleDateOfCertification(),
        RuleCalibration(),
        RuleVerification(),
        RuleSpotCheckResult(),
        RuleConformance(),
        RuleFlowVerification(),
        RuleMeterVerification(),
        RuleWhereKept(),
        RuleFormSentTo(),
        RuleDate(),
        RuleDocumentTemplateVersion(),
        RuleDocumentHeader(),
        RuleTemplateMarkerT4(),
        RuleTemplateMarkerT6(),
        RuleTemplateMarkerT7(),
        RuleTemplateMarkerImpounding(),
        RuleTemplateMarkerBaselineComments(),
        RuleTemplateMarkerAlternateComments(),
        RuleGeneralComments(),
        RuleMaintenanceLine(),
        RuleReadingsTakenLine(),
        RuleInspectionDate(),
        RuleEmail()
    ];
    
    // Real documents mark these checkbox-style fields with whatever the scanner/typist
    // used - tick, cross, or a Unicode box glyph - not just the Y/N the field nominally
    // asks for. Tried in this order; a genuinely-unticked box (☐) is itself a real answer
    // ("not confirmed"), not a missing one, so it's listed alongside the others rather
    // than treated as blank.
    //
    // ExceptWhenInsideWord on every entry - a stray lowercase "n" inside any ordinary English
    // word ("condition", "manufacturer", "accordance", "necessary") would otherwise win the
    // match before the algorithm ever reaches a real tick/cross that may also be present in the
    // same text.
    private static readonly List<TextToMatch> CheckboxMarkPossibilities =
    [
        new("Y") { ExceptWhenInsideWord = true },
        new("N") { ExceptWhenInsideWord = true },
        new("✓") { ExceptWhenInsideWord = true },
        new("☑") { ExceptWhenInsideWord = true },
        new("☒") { ExceptWhenInsideWord = true },
        new("☐") { ExceptWhenInsideWord = true },
        new("X") { ExceptWhenInsideWord = true },
        new("x") { ExceptWhenInsideWord = true }
    ];

    // The "grid template" layout prints "Calibration: Conformance: Flow verification: Meter
    // verification:" as one label row, with no dedicated value row for these four fields -
    // the row directly below is consistently "Maintenance:"'s own row. IgnoreBlockIfContains
    // rejects a match whose captured column contains a recognisable sibling label;
    // SkipNextLineWhenStartsWith("Maintenance") (set per field below) rejects the whole
    // next-line candidate before any column is picked from it. Either way the field ends up
    // genuinely unmatched (blank) instead of silently showing another field's data.
    private static readonly List<string> VerificationGridSiblingLeakTerms =
    [
        "Calibration:", "Conformance:", "Flow verification:", "Meter verification:",
        "Maintenance:", "Frequency:", "Spot Check Result", "General comments"
    ];

    // The LicenceProvisions grid's shared "In Order / Not In Order / blank" answer shape -
    // every InOrder-sourced field uses this identical list. Order is load-bearing: paired-
    // checkbox alternates ("☑ ☐" etc.) must precede the single-glyph ones below them, or a bare
    // "☒" possibility would win a .First() match against "☒ ☐" before the position-based
    // paired check gets a chance. The four Private Use Area entries are Wingdings-style tick
    // glyphs, written as \u escapes rather than literal glyphs so they survive editing/rendering
    // intact - confirmed present in the real corpus by scanning all 789 real PDFs' extracted text
    // directly (2026-09-08): U+F0FC (638 occurrences/81 docs), U+F061 (87/11), U+F050 (192/36),
    // U+F072 (4/1). These four were previously present as `new("")` (a genuinely empty string,
    // not the intended glyph - lost at some point before this comment's own claim about them was
    // ever verified) - an empty TextToMatch.Text always matches (MatchesPossibility's
    // text.Contains("") is trivially true), so every one of these four real answers, plus every
    // plain "Y"/"N" answer sitting after them in this list, was silently resolving to Blank
    // instead of a real InOrder/NotInOrder verdict. Fixed by restoring the actual codepoints.
    private static readonly List<TextToMatch> InOrderPossibilities =
    [
        new("☑ ☐") { ExceptWhenInsideWord = true },
        new("☒ ☐") { ExceptWhenInsideWord = true },
        new("☐ ☑") { ExceptWhenInsideWord = true },
        new("☐ ☒") { ExceptWhenInsideWord = true },
        new("☐ ☐") { ExceptWhenInsideWord = true },
        new("N/A") { ExceptWhenInsideWord = true },
        new("NI") { ExceptWhenInsideWord = true }, // "not inspected" - a genuine distinct answer, not a typo
        new("Not") { ExceptWhenInsideWord = true },
        new("In") { ExceptWhenInsideWord = true },
        new("✓") { ExceptWhenInsideWord = true },
        new("✔") { ExceptWhenInsideWord = true },
        new("√") { ExceptWhenInsideWord = true },
        new("🗸") { ExceptWhenInsideWord = true },
        new("") { ExceptWhenInsideWord = true },
        new("") { ExceptWhenInsideWord = true },
        new("") { ExceptWhenInsideWord = true },
        new("") { ExceptWhenInsideWord = true },
        new("X") { ExceptWhenInsideWord = true },
        new("☒") { ExceptWhenInsideWord = true },
        new("×") { ExceptWhenInsideWord = true },
        new("Y") { ExceptWhenInsideWord = true }, // T6 template uses Y/N instead of In/Not/tick/cross
        new("N") { ExceptWhenInsideWord = true },
        // Catch-all, tried last: a genuinely blank tick field must still survive as a match so
        // the converter can classify it as Blank rather than discarding the whole result.
        new("")
    ];

    // ---- LicenceProvisions grid (InOrder-shaped fields) ----
    // Grid layout confirmed against real documents (row groupings, left-to-right):
    //   Row 1: Source of supply | Quantities | Land
    //   Row 2: Point of abstraction | Means of measurement | Charging factors
    //   Row 3: Means of abstraction | Records | Other provisions
    //   Row 4: Purposes | Provision of information
    //   Row 5: Period | Special conditions
    // Bounding each field to its row-neighbour keeps the same-line column walk from pulling the
    // next field's label text into this field's captured value.

    private static (string, List<LabelToMatch>) RuleSourceOfSupply() =>
        (WrInspectionReportFieldNames.SourceOfSupply, [WrRule.InOrder("Source of supply", InOrderPossibilities, "Quantities").Named(WrInspectionReportFieldNames.SourceOfSupply).Build()]);

    private static (string, List<LabelToMatch>) RulePointOfAbstraction() =>
        (WrInspectionReportFieldNames.PointOfAbstraction, [WrRule.InOrder("Point of abstraction", InOrderPossibilities, "Means of measurement").Named(WrInspectionReportFieldNames.PointOfAbstraction).Build()]);

    // "Records" sometimes renders letter-kerned - without these as alternate end markers, the
    // same-line column walk never recognises the boundary and sweeps all the way to "...N/A" at
    // the end of the row, which then wins over the real "In Order" answer.
    private static (string, List<LabelToMatch>) RuleMeansOfAbstraction() =>
        (WrInspectionReportFieldNames.MeansOfAbstraction, [
            WrRule.InOrder("Means of abstraction", InOrderPossibilities, "Records").Named(WrInspectionReportFieldNames.MeansOfAbstraction)
                .AlsoEndsAtLineStart("R ecords", "R e cords", "R e c ords", "R e c o rds", "R e c o r ds", "R e c o r d s")
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RulePurposes() =>
        (WrInspectionReportFieldNames.Purposes, [WrRule.InOrder("Purpose(s)", InOrderPossibilities, "Provision of information").Named(WrInspectionReportFieldNames.Purposes).Build()]);

    private static (string, List<LabelToMatch>) RulePeriod() =>
        (WrInspectionReportFieldNames.Period, [WrRule.InOrder("Period", InOrderPossibilities, "Special conditions").Named(WrInspectionReportFieldNames.Period).Build()]);

    private static (string, List<LabelToMatch>) RuleQuantities() =>
        (WrInspectionReportFieldNames.Quantities, [WrRule.InOrder("Quantities", InOrderPossibilities, "Land").Named(WrInspectionReportFieldNames.Quantities).Build()]);

    private static (string, List<LabelToMatch>) RuleMeansOfMeasurement() =>
        (WrInspectionReportFieldNames.MeansOfMeasurement, [WrRule.InOrder("Means of measurement", InOrderPossibilities, "Charging factors").Named(WrInspectionReportFieldNames.MeansOfMeasurement).Build()]);

    // "Records" renders with progressively wider letter-kerning on 340/789 real corpus docs
    // (43%) - only 7 distinct literal patterns cover all occurrences, so literal alternates are
    // sufficient here rather than a whitespace-tolerant matching engine change.
    private static (string, List<LabelToMatch>) RuleRecords() =>
        (WrInspectionReportFieldNames.Records, [
            WrRule.InOrder("R ecords", InOrderPossibilities, "Other provisions").Named(WrInspectionReportFieldNames.Records)
                .AlsoStartsWith("R e cords", "R e c ords", "R e c o rds", "R e c o r ds", "R e c o r d s")
                .Build()
        ]);

    // "Measurement details" is the section header that always follows the whole grid - a safe,
    // distant bound for each of these five fields regardless of which one is last on its row.
    private static (string, List<LabelToMatch>) RuleProvisionOfInformation() =>
        (WrInspectionReportFieldNames.ProvisionOfInformation, [WrRule.InOrder("Provision of information", InOrderPossibilities, "Measurement details").Named(WrInspectionReportFieldNames.ProvisionOfInformation).Build()]);

    // BoundByOtherLabels fixes a fabricated "InOrder" value caused by the same-line column walk
    // sweeping in an unrelated field with no positional bound.
    private static (string, List<LabelToMatch>) RuleSpecialConditions() =>
        (WrInspectionReportFieldNames.SpecialConditions, [
            WrRule.InOrder("Special conditions", InOrderPossibilities, "Measurement details").Named(WrInspectionReportFieldNames.SpecialConditions).BoundByOtherLabels().Build()
        ]);

    private static (string, List<LabelToMatch>) RuleLand() =>
        (WrInspectionReportFieldNames.Land, [WrRule.InOrder("Land (only if specified)", InOrderPossibilities, "Measurement details").Named(WrInspectionReportFieldNames.Land).Build()]);

    private static (string, List<LabelToMatch>) RuleChargingFactors() =>
        (WrInspectionReportFieldNames.ChargingFactors, [WrRule.InOrder("Charging factors", InOrderPossibilities, "Measurement details").Named(WrInspectionReportFieldNames.ChargingFactors).Build()]);

    private static (string, List<LabelToMatch>) RuleOtherProvisions() =>
        (WrInspectionReportFieldNames.OtherProvisions, [WrRule.InOrder("Other provisions (specify below)", InOrderPossibilities, "Measurement details").Named(WrInspectionReportFieldNames.OtherProvisions).Build()]);

    // ---- Header / address block ----

    private static (string, List<LabelToMatch>) RuleLicenceNumber() =>
        (WrInspectionReportFieldNames.LicenceNumber, [
            WrRule.Between("Licence No. (or Application No. or GIC No. etc.)", "Inspection Class").Named(WrInspectionReportFieldNames.LicenceNumber)
                .NextLines(1).RequireTextToClaimGroup()
                .AlsoEndsAt("Name and address", "Name / address")
                .Build(), // Long form
            WrRule.Between("Licence No", "Inspection Class").Named(WrInspectionReportFieldNames.LicenceNumber)
                .NextLines(1).RequireTextToClaimGroup()
                .AlsoEndsAt("Name and address", "Name / address")
                // Longest/most-specific literal first: "(or Application No. or GIC No." (no
                // "etc") is a literal prefix of the "etc.)" variants, so it must be tried last.
                .Remove([
                    new("(or Application No. or GIC No. etc.)"),
                    new("(or Application No. or GIC No. Etc)"),
                    new("(or Application No. or GIC No. etc)"),
                    new("(or Application No. or GIC No. etc."),
                    new("(or Application No. or GIC No)"),
                    new("(or Application No. or GIC No.")
                ])
                .Build() // Short form ("Licence No." / "Licence No:")
        ]);

    private static (string, List<LabelToMatch>) RuleMetWith() =>
        (WrInspectionReportFieldNames.MetWith, [WrRule.After("Met with").Named(WrInspectionReportFieldNames.MetWith).Build()]);

    private static (string, List<LabelToMatch>) RuleInspectingOfficer() =>
        (WrInspectionReportFieldNames.InspectingOfficer, [WrRule.After("Inspecting Officer").Named(WrInspectionReportFieldNames.InspectingOfficer).Build()]);

    private static (string, List<LabelToMatch>) RuleSiteAddress() =>
        (WrInspectionReportFieldNames.SiteAddress, [
            WrRule.Between("Site address (if different)", "Met with").Named(WrInspectionReportFieldNames.SiteAddress)
                .NextLines(10)
                .AlsoEndsAt("Email", "Inspecting Officer")
                .AlsoStartsWith("Site address (if different from above)")
                .SkipNextLineWhenStartsWith("Desktop Review", "Desktop:", "Site Visit: Desktop", "Liaised with")
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleInspectionClass() =>
        (WrInspectionReportFieldNames.InspectionClass, [
            WrRule.Between("Inspection Class", "Telephone No").Named(WrInspectionReportFieldNames.InspectionClass).NextLines(1)
                .AlsoEndsAt(
                    "T e l e p h o n e N o", "T e l e p h o n e No", "T e le p h o n e No",
                    "Telepho n e N o", "T e lephone No", "T e l e phone No", "Email")
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleTelephoneNumber() =>
        (WrInspectionReportFieldNames.TelephoneNumber, [
            WrRule.Between("Telephone No", "Email").Named(WrInspectionReportFieldNames.TelephoneNumber).NextLines(2)
                .AlsoStartsWith(
                    "T e l e p h o n e N o", "T e l e p h o n e No", "T e le p h o n e No",
                    "Telepho n e N o", "T e lephone No", "T e l e phone No", "T e l N o")
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RulePosition() =>
        (WrInspectionReportFieldNames.Position, [WrRule.Between("Position", "Inspection Date").Named(WrInspectionReportFieldNames.Position).NextLines(1).Build()]);

    private static (string, List<LabelToMatch>) RuleTime() =>
        (WrInspectionReportFieldNames.Time, [
            WrRule.After("Time").Named(WrInspectionReportFieldNames.Time)
                .AlsoStartsWithLoose("Time:").Build()
        ]);

    private static (string, List<LabelToMatch>) RuleNameAndAddress() =>
        (WrInspectionReportFieldNames.NameAndAddress, [
            WrRule.Between("Name and address", "Site address").Named(WrInspectionReportFieldNames.NameAndAddress).NextLines(10)
                .AlsoEndsAt(
                    "Telephone No", "Email",
                    "T e l e p h o n e N o", "T e l e p h o n e No", "T e le p h o n e No",
                    "Telepho n e N o", "T e lephone No", "T e l e phone No", "T e l N o")
                .Build(), // Existing template
            WrRule.After("Name / address").Named(WrInspectionReportFieldNames.NameAndAddress).Build(), // Water Company template
            WrRule.After("Name & address").Named(WrInspectionReportFieldNames.NameAndAddress).Build(), // Water Company template
            WrRule.After("Permit holder name and address").Named(WrInspectionReportFieldNames.NameAndAddress).NextLines(1)
                .Remove([new("Telephone No:")]).Build() // Permit holder template
        ]);

    // ---- Meter / measurement details ----

    // Widened from 1 (single line) to fit a multi-meter table's full column of "Point N, <site>:
    // <value>" lines - see WrInspectionReportSchemaConverter's Meters-splitting logic. Safe to
    // widen: TextToFindIsBetweenLabels' same-line walk stops as soon as it hits its own end
    // boundary regardless of how many lines are available, so a single-meter document (which
    // already hits that boundary within 1 line) behaves identically; this only gives a
    // multi-meter document's walk enough room to reach its real end boundary instead of running
    // out of fetched lines first. Left at 1 on rules with no TextEnd bound at all (plain After()
    // alternates) - widening those wouldn't be meaningful without a real boundary to stop at.
    private const int MeterTableNextLines = 10;

    private static (string, List<LabelToMatch>) RuleMeterName() =>
        (WrInspectionReportFieldNames.MeterName, [WrRule.Between("Meter Name", "Meter Make").Named(WrInspectionReportFieldNames.MeterName).NextLines(MeterTableNextLines).Build()]); // T6 template only

    private static (string, List<LabelToMatch>) RuleMeterMake() =>
        (WrInspectionReportFieldNames.MeterMake, [
            WrRule.Between("Meter make", "Reading:").Named(WrInspectionReportFieldNames.MeterMake).NextLines(MeterTableNextLines).RequireTextToClaimGroup()
                .AlsoEndsAt("Serial number", "Meter Serial No", "Serial no").Build(), // Existing template
            WrRule.Between("Meter Make", "Meter Serial Number").Named(WrInspectionReportFieldNames.MeterMake).NextLines(MeterTableNextLines).RequireTextToClaimGroup()
                .AlsoEndsAt("Meter Serial No").Build() // T6 template
        ]);

    private static (string, List<LabelToMatch>) RuleSerialNumber() =>
        (WrInspectionReportFieldNames.SerialNumber, [
            WrRule.After("Serial number").Named(WrInspectionReportFieldNames.SerialNumber).RequireTextToClaimGroup().Build(), // Existing template
            WrRule.Between("Meter Serial Number", "Meter Asset Number").Named(WrInspectionReportFieldNames.SerialNumber)
                .NextLines(MeterTableNextLines).RequireTextToClaimGroup().Build(), // T6 template
            WrRule.Between("Serial number", "Units").Named(WrInspectionReportFieldNames.SerialNumber)
                .NextLines(MeterTableNextLines).RequireTextToClaimGroup().Build() // Baseline two-column table
        ]);

    private static (string, List<LabelToMatch>) RuleMeterAssetNumber() =>
        (WrInspectionReportFieldNames.MeterAssetNumber, [
            WrRule.Between("Meter Asset Number", "Meter Reading").Named(WrInspectionReportFieldNames.MeterAssetNumber).NextLines(MeterTableNextLines).Build(), // T6 template
            WrRule.After("Asset no:").Named(WrInspectionReportFieldNames.MeterAssetNumber).Build(), // Existing template
            WrRule.After("Asset number:").Named(WrInspectionReportFieldNames.MeterAssetNumber).Build() // Existing template
        ]);

    // "Reading" is a literal string prefix of the unrelated sibling label "Readings taken:" -
    // requiring the colon disambiguates both that collision and "Reading, RG8 7BB" (a town name).
    private static (string, List<LabelToMatch>) RuleReading() =>
        (WrInspectionReportFieldNames.Reading, [
            WrRule.After("Reading:").Named(WrInspectionReportFieldNames.Reading).RequireTextToClaimGroup().Build(), // Existing template
            WrRule.Between("Meter Reading", "Flow Rate").Named(WrInspectionReportFieldNames.Reading).NextLines(MeterTableNextLines).RequireTextToClaimGroup().Build(), // T6 template
            // AlsoEndsAt markers added alongside the MeterTableNextLines widening - when the
            // meter table's own "Units" header genuinely never reappears (a blank/N-A table),
            // the wider window otherwise bled straight into the next form section instead of
            // stopping (measured: HallucinationRate 11%->39% before these were added).
            WrRule.Between("Reading:", "Units").Named(WrInspectionReportFieldNames.Reading).NextLines(MeterTableNextLines).RequireTextToClaimGroup()
                .SkipNextLineWhenStartsWith("Other")
                .AlsoEndsAt("Other:-", "Certificates or records available for", "Date of certificate", "Meter verification").Build() // Baseline two-column table
        ]);

    private static (string, List<LabelToMatch>) RuleFlowRate() =>
        (WrInspectionReportFieldNames.FlowRate, [WrRule.Between("Flow Rate", "Calibration").Named(WrInspectionReportFieldNames.FlowRate).NextLines(MeterTableNextLines).Build()]); // T6 template only

    private static (string, List<LabelToMatch>) RuleUnits() =>
        (WrInspectionReportFieldNames.Units, [
            WrRule.After("Units").Named(WrInspectionReportFieldNames.Units).Build(), // Existing template
            // "Flow Rate" is T6-only and never appears on a T1 form at all, so on T1 documents
            // this alternate's real end boundary never fires and the widened window otherwise
            // bled into the next form section - same fix and same measured cause as Reading's
            // AlsoEndsAt above.
            WrRule.Between("Units", "Flow Rate").Named(WrInspectionReportFieldNames.Units).NextLines(MeterTableNextLines).RequireTextToClaimGroup()
                .AlsoEndsAt("Other:-", "Certificates or records available for", "Date of certificate", "Meter verification").Build() // T6 template
        ]);

    private static (string, List<LabelToMatch>) RuleOther() =>
        (WrInspectionReportFieldNames.Other, [WrRule.After("Other:").Named(WrInspectionReportFieldNames.Other).Build()]);

    private static (string, List<LabelToMatch>) RuleCertificatesOfRecords() =>
        (WrInspectionReportFieldNames.CertificatesOfRecords, [WrRule.After("Certificates or records available for").Named(WrInspectionReportFieldNames.CertificatesOfRecords).Build()]);

    private static (string, List<LabelToMatch>) RuleDateOfCertification() =>
        (WrInspectionReportFieldNames.DateOfCertification, [
            WrRule.Between("Date of certificate or", "By whom").Named(WrInspectionReportFieldNames.DateOfCertification).NextLines(1)
                .Remove([new("record:"), new("Conformance:")]).Build()
        ]);

    // A fourth layout beyond New/Existing/T6: "Calibration: Conformance: Flow verification:
    // Meter verification:" as one label row, answers on the row below in the same columns.
    private static (string, List<LabelToMatch>) RuleCalibration() =>
        (WrInspectionReportFieldNames.Calibration, [
            WrRule.After("Calibration").Named(WrInspectionReportFieldNames.Calibration).NextLines(1).RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")]).EndsAt("Conformance").Build(), // New template
            WrRule.Between("Calibration", "Conformance").Named(WrInspectionReportFieldNames.Calibration).WholeLine()
                .RequireTextToClaimGroup().Possibilities(CheckboxMarkPossibilities).Build(), // Existing template
            WrRule.Between("Calibration", "Verification").Named(WrInspectionReportFieldNames.Calibration).NextLines(1).RequireTextToClaimGroup()
                .AlsoEndsAt("Conformance")
                .IgnoreIfContains([..VerificationGridSiblingLeakTerms, "Certificate"])
                .SkipNextLineWhenStartsWith("Maintenance").Build() // T6 / Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleVerification() =>
        (WrInspectionReportFieldNames.Verification, [WrRule.Between("Verification", "Spot Check Result").Named(WrInspectionReportFieldNames.Verification).NextLines(1).Build()]); // T6 template only

    private static (string, List<LabelToMatch>) RuleSpotCheckResult() =>
        (WrInspectionReportFieldNames.SpotCheckResult, [
            WrRule.Between("Spot Check Result", "General comments").Named(WrInspectionReportFieldNames.SpotCheckResult).NextLines(1)
                .Remove([new("–")]).Build()
        ]); // T6 template only

    private static (string, List<LabelToMatch>) RuleConformance() =>
        (WrInspectionReportFieldNames.Conformance, [
            WrRule.After("Conformance").Named(WrInspectionReportFieldNames.Conformance).NextLines(1).RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")]).EndsAt("Flow verification").Build(), // New template
            WrRule.Between("Conformance", "Flow verification").Named(WrInspectionReportFieldNames.Conformance).WholeLine()
                .RequireTextToClaimGroup().Possibilities(CheckboxMarkPossibilities).Build(), // Existing template
            WrRule.Between("Conformance", "Flow verification").Named(WrInspectionReportFieldNames.Conformance).NextLines(1)
                .RequireTextToClaimGroup().IgnoreIfContains([..VerificationGridSiblingLeakTerms])
                .SkipNextLineWhenStartsWith("Maintenance").Build() // Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleFlowVerification() =>
        (WrInspectionReportFieldNames.FlowVerification, [
            WrRule.After("Flow verification").Named(WrInspectionReportFieldNames.FlowVerification).NextLines(1).RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")]).EndsAt("Meter verification").Build(), // New template
            WrRule.Between("Flow verification", "Meter verification").Named(WrInspectionReportFieldNames.FlowVerification).WholeLine()
                .RequireTextToClaimGroup().Possibilities(CheckboxMarkPossibilities).Build(), // Existing template
            WrRule.Between("Flow verification", "Meter verification").Named(WrInspectionReportFieldNames.FlowVerification).NextLines(1)
                .RequireTextToClaimGroup().IgnoreIfContains([..VerificationGridSiblingLeakTerms])
                .SkipNextLineWhenStartsWith("Maintenance").Build() // Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleMeterVerification() =>
        (WrInspectionReportFieldNames.MeterVerification, [
            WrRule.After("Meter verification").Named(WrInspectionReportFieldNames.MeterVerification).NextLines(1).RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")]).EndsAt("Maintenance").Build(), // New template
            WrRule.Between("Meter verification", "record").Named(WrInspectionReportFieldNames.MeterVerification).WholeLine()
                .RequireTextToClaimGroup().Possibilities(CheckboxMarkPossibilities).Build(), // Existing template
            WrRule.Between("Meter verification", "record").Named(WrInspectionReportFieldNames.MeterVerification).NextLines(1)
                .RequireTextToClaimGroup().IgnoreIfContains([..VerificationGridSiblingLeakTerms])
                .SkipNextLineWhenStartsWith("Maintenance").Build() // Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleWhereKept() =>
        (WrInspectionReportFieldNames.WhereKept, [WrRule.After("Where kept").Named(WrInspectionReportFieldNames.WhereKept).Build()]);

    private static (string, List<LabelToMatch>) RuleFormSentTo() =>
        (WrInspectionReportFieldNames.FormSentTo, [WrRule.Between("Form sent to", "Date").Named(WrInspectionReportFieldNames.FormSentTo).NextLines(1).Build()]);

    private static (string, List<LabelToMatch>) RuleDate() =>
        (WrInspectionReportFieldNames.Date, [WrRule.After("Date:").Named(WrInspectionReportFieldNames.Date).Build()]);

    private static (string, List<LabelToMatch>) RuleDocumentTemplateVersion() =>
        (WrInspectionReportFieldNames.DocumentTemplateVersion, [WrRule.After("Document Template Version:").Named(WrInspectionReportFieldNames.DocumentTemplateVersion).Build()]);

    private static (string, List<LabelToMatch>) RuleDocumentHeader() =>
        (WrInspectionReportFieldNames.DocumentHeader, [WrRule.After("Form WR - ").Named(WrInspectionReportFieldNames.DocumentHeader).Build()]);

    // ---- Template-family markers (presence checks, not value extraction) ----

    private static (string, List<LabelToMatch>) RuleTemplateMarkerT4() =>
        (WrInspectionReportFieldNames.TemplateMarkerT4, TemplateMarker("Permit holder name and address", WrInspectionReportFieldNames.TemplateMarkerT4));

    private static (string, List<LabelToMatch>) RuleTemplateMarkerT6() =>
        (WrInspectionReportFieldNames.TemplateMarkerT6, TemplateMarker(
            "Meter Name", WrInspectionReportFieldNames.TemplateMarkerT6,
            additionalTextStarts: ["Calibration Certificate", "Verification Certificate"]));

    private static (string, List<LabelToMatch>) RuleTemplateMarkerT7() =>
        (WrInspectionReportFieldNames.TemplateMarkerT7, TemplateMarker("Water Company", WrInspectionReportFieldNames.TemplateMarkerT7));

    private static (string, List<LabelToMatch>) RuleTemplateMarkerImpounding() =>
        (WrInspectionReportFieldNames.TemplateMarkerImpounding, TemplateMarker("Point of Impoundment", WrInspectionReportFieldNames.TemplateMarkerImpounding));

    private static (string, List<LabelToMatch>) RuleTemplateMarkerBaselineComments() =>
        (WrInspectionReportFieldNames.TemplateMarkerBaselineComments, TemplateMarker(
            "General comments, details / dates of occupation changes, actions required etc.",
            WrInspectionReportFieldNames.TemplateMarkerBaselineComments));

    private static (string, List<LabelToMatch>) RuleTemplateMarkerAlternateComments() =>
        (WrInspectionReportFieldNames.TemplateMarkerAlternateComments, TemplateMarker(
            "Introduction", WrInspectionReportFieldNames.TemplateMarkerAlternateComments,
            additionalTextStarts: [
                "Re-inspection", "Notes and Actions", "Further Conditions", "Actions/Recommendations",
                "General comments / background", "General comments / relevant background",
                "General / relevant background", "General comments, background"
            ]));

    // The baseline heading alone only covers 61% of the real corpus - "Actions"/"Summary" are
    // deliberately kept as valid anchors (a different, longer-form report template) but
    // excluded from Remove: they're common enough to legitimately recur as a genuine
    // sub-heading later in the same captured block, and stripping them there silently corrupts
    // real narrative content (confirmed via the ground-truth harness).
    private static (string, List<LabelToMatch>) RuleGeneralComments() =>
        (WrInspectionReportFieldNames.GeneralComments, [
            WrRule.Between("General comments, details / dates of occupation changes, actions required etc.", "Form sent to")
                .Named(WrInspectionReportFieldNames.GeneralComments).WholeLine().NextLines(100)
                .AlsoEndsAt("Customer charter") // fixed appeal-process boilerplate on longer-form documents - never genuine comments content
                .AlsoStartsWith(
                    "Introduction", "Re-inspection", "Notes and Actions", "Further Conditions",
                    "Actions/Recommendations", "General comments / background",
                    "General comments / relevant background", "General / relevant background",
                    "General comments, background", "Actions", "Summary")
                .ExceptFromRemove("Actions", "Summary")
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleMaintenanceLine() =>
        (WrInspectionReportFieldNames.MaintenanceLine, MaintenanceLine("Maintenance:", "Readings taken", WrInspectionReportFieldNames.MaintenanceLine));

    private static (string, List<LabelToMatch>) RuleReadingsTakenLine() =>
        (WrInspectionReportFieldNames.ReadingsTakenLine, MaintenanceLine("Readings taken:", "Where Kept", WrInspectionReportFieldNames.ReadingsTakenLine));

    // Tried and reverted (2026-09-09): adding a loose "Date:" alternate to catch the case where
    // "Inspection Date:" wraps onto two lines ("Inspection" / "Date: ...") - confirmed on
    // wr51__73417g0068__... and wr51__an0340003001r01__... (the golden set's own only
    // InspectionDate Miss). Measured net regression against the golden set: recall 98%->40%
    // (54 Hit->22 Hit, 32 newly Wrong) - bare "Date:" is too ambiguous against this document's
    // other "Date:" occurrences (e.g. "Date of certificate or record:") and started winning over
    // the correct match on documents where the primary rule already worked fine. Needs a
    // genuinely two-line-aware match (the wrapped "Inspection" line immediately preceding the
    // "Date:" line), not a same-line loose text search - AlsoStartsWithLoose only relaxes the
    // column requirement, it doesn't span line boundaries. Not attempted further this session.
    private static (string, List<LabelToMatch>) RuleInspectionDate() =>
        (WrInspectionReportFieldNames.InspectionDate, [
            WrRule.Between("Inspection Date:", "Quantities").Named(WrInspectionReportFieldNames.InspectionDate).NextLines(2)
                .AlsoEndsAt("Time:", "Inspecting Officer").Build()
        ]);

    private static (string, List<LabelToMatch>) RuleEmail() =>
        (WrInspectionReportFieldNames.Email, [WrRule.Between("Email", "Position:").Named(WrInspectionReportFieldNames.Email).NextLines(1).Build()]);

    // A pure presence check, not a value extraction - used for the WrTemplateType marker
    // fields. Kept outside the Rule fluent surface deliberately: it's a genuinely different
    // shape (IncludeStartLabelText guarantees a non-empty match whenever the marker is found,
    // even with nothing meaningful following it on the page).
    private static List<LabelToMatch> TemplateMarker(string text, string labelName, List<string>? additionalTextStarts = null)
    {
        return
        [
            new LabelToMatch
            {
                TextStart =
                [
                    new(text) { ColumnMustStartWith = true },
                    ..(additionalTextStarts ?? []).Select(t => new TextToMatch(t) { ColumnMustStartWith = true })
                ],
                TextEnd = [new("[END_OF_BLOCK]")],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                LimitTo = LimitTo.WholeLine,
                IncludeStartLabelText = true,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Name = labelName
            }
        ];
    }

    // A compound row (Maintenance:/Readings taken: plus Y/N-or-word answer, Frequency, By whom
    // sub-fields) - kept outside the Rule fluent surface deliberately, since it's structurally
    // more complex than an ordinary rule (five SubLabels sharing one parent row), not less.
    // SubLabels themselves are still built via Rule, for the same construction consistency as
    // every other field.
    private static List<LabelToMatch> MaintenanceLine(string textStart, string textEnd, string name)
    {
        return
        [
            new LabelToMatch
            {
                TextStart = [new(textStart) { LineMustStartWith = true }],
                TextEnd = [new(textEnd) { LineMustStartWith = true }, new("[END_OF_BLOCK]")],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                // SameColumn preserves the row's real column boundaries (Maintenance:/Yes /
                // Frequency:/Daily / By whom:/JP) - WholeLine would flatten them into one blob,
                // and the SubLabels below (via Rule.After) rely on each one starting its own
                // column.
                LimitTo = LimitTo.SameColumn,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1,
                Name = name,
                IncludeStartLabelText = true,
                SubLabels =
                [
                    (name == WrInspectionReportFieldNames.MaintenanceLine
                        ? WrRule.After("Maintenance:").Named($"{name}Maintenance").EndsAt("Frequency")
                        : WrRule.After("Readings taken:").Named($"{name}ReadingsTaken").EndsAt("Frequency")).Build(),
                    (name == WrInspectionReportFieldNames.MaintenanceLine
                        ? WrRule.Between("Maintenance:", "N:").Named($"{name}MaintenanceYes").WholeLine().Possibilities([new("✓"), new("X")])
                        : WrRule.Between("Readings taken:", "N:").Named($"{name}ReadingsTakenYes").WholeLine().Possibilities([new("✓"), new("X")])).Build(),
                    (name == WrInspectionReportFieldNames.MaintenanceLine
                        ? WrRule.Between("N:", "Frequency:").Named($"{name}MaintenanceNo").WholeLine().Possibilities([new("✓"), new("X")])
                        : WrRule.Between("N:", "Frequency:").Named($"{name}ReadingsTakenNo").WholeLine().Possibilities([new("✓"), new("X")])).Build(),
                    WrRule.After("Frequency:").Named($"{name}Frequency").EndsAt("By whom").Build(),
                    WrRule.After("By whom:").Named($"{name}ByWhom").Build()
                ]
            }
        ];
    }
}
