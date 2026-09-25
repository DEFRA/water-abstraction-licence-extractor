using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Models.Configuration;

namespace WRADI.DocumentType.WrInspectionReport.Configuration;

public static class WrInspectionReportTextBasedLabelConfiguration
{
    public static void ConfigurationPropertiesToSet(LookupConfiguration lookupConfiguration)
    {
        lookupConfiguration.LineHeight = 6;
        lookupConfiguration.MinimumRowsForDigital = 30;
        lookupConfiguration.HorizontalGapBetweenColumns = 15;
        lookupConfiguration.InferMissingColumns = true;
    }

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
    
    // Real documents mark checkboxes with whatever the scanner/typist used (tick, cross, or a
    // box glyph), not just Y/N - an unticked box (☐) is itself a real answer, not a missing one.
    // ExceptWhenInsideWord everywhere: a stray lowercase "n" inside an ordinary word
    // ("condition", "necessary") would otherwise win before a real tick/cross is reached.
    private static readonly List<TextToMatch> CheckboxMarkPossibilities =
    [
        new("Y") { ExceptWhenInsideWord = true },
        new("N") { ExceptWhenInsideWord = true },
        new("✓") { ExceptWhenInsideWord = true },
        new("☑") { ExceptWhenInsideWord = true },
        new("☒") { ExceptWhenInsideWord = true },
        new("☐") { ExceptWhenInsideWord = true },
        new("X") { ExceptWhenInsideWord = true },
        new("x") { ExceptWhenInsideWord = true },
        new("") { ExceptWhenInsideWord = true } // Wingdings-style tick glyph (U+F0D6) - see InOrderPossibilities
    ];

    // The grid template prints all four field labels as one row with no dedicated value row -
    // the row below is "Maintenance:"'s own. IgnoreIfContains/SkipNextLineWhenStartsWith
    // ("Maintenance") reject a captured value that's really a sibling label, leaving it blank.
    private static readonly List<string> VerificationGridSiblingLeakTerms =
    [
        "Calibration:",
        "Conformance:",
        "Flow verification:",
        "Meter verification:",
        "Maintenance:",
        "Frequency:",
        "Spot Check Result",
        "General comments"
    ];

    // The LicenceProvisions grid's shared "In Order / Not In Order / blank" answer shape.
    // Order is load-bearing: paired checkboxes ("☑ ☐" etc.) must precede the single-glyph
    // entries, or a bare "☒" wins .First() before the paired check gets a chance. The PUA
    // entries are Wingdings tick glyphs (\u escapes so they survive editing) - they'd previously
    // decayed to `new("")`, which matches everything, silently resolving real ticks (and every
    // Y/N after them) to Blank.
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
        new("") { ExceptWhenInsideWord = true }, // U+F0D6 - a fifth Wingdings-style tick codepoint
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
        (WrInspectionReportFieldNames.SourceOfSupply, [
            WrFluentRule
                .InOrder("Source of supply", InOrderPossibilities, "Quantities")
                .Named(WrInspectionReportFieldNames.SourceOfSupply)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RulePointOfAbstraction() =>
        (WrInspectionReportFieldNames.PointOfAbstraction, [
            WrFluentRule
                .InOrder("Point of abstraction", InOrderPossibilities, "Means of measurement")
                .Named(WrInspectionReportFieldNames.PointOfAbstraction)
                .FromText()
                .Build()]);

    // "Records" sometimes renders letter-kerned - without these as alternate end markers, the
    // same-line column walk never recognises the boundary and sweeps all the way to "...N/A" at
    // the end of the row, which then wins over the real "In Order" answer.
    private static (string, List<LabelToMatch>) RuleMeansOfAbstraction() =>
        (WrInspectionReportFieldNames.MeansOfAbstraction, [
            WrFluentRule
                .InOrder("Means of abstraction", InOrderPossibilities, "Records")
                .Named(WrInspectionReportFieldNames.MeansOfAbstraction)
                .AlsoEndsAtLineStart("R ecords", "R e cords", "R e c ords", "R e c o rds", "R e c o r ds", "R e c o r d s")
                .FromText()
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RulePurposes() =>
        (WrInspectionReportFieldNames.Purposes, [
            WrFluentRule
                .InOrder("Purpose(s)", InOrderPossibilities, "Provision of information")
                .Named(WrInspectionReportFieldNames.Purposes)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RulePeriod() =>
        (WrInspectionReportFieldNames.Period, [
            WrFluentRule
                .InOrder("Period",InOrderPossibilities, "Special conditions")
                .Named(WrInspectionReportFieldNames.Period)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleQuantities() =>
        (WrInspectionReportFieldNames.Quantities, [
            WrFluentRule
                .InOrder("Quantities", InOrderPossibilities, "Land")
                .Named(WrInspectionReportFieldNames.Quantities)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleMeansOfMeasurement() =>
        (WrInspectionReportFieldNames.MeansOfMeasurement, [
            WrFluentRule
                .InOrder("Means of measurement", InOrderPossibilities, "Charging factors")
                .Named(WrInspectionReportFieldNames.MeansOfMeasurement)
                .FromText()
                .Build()]);

    // "Records" renders with progressively wider letter-kerning on 340/789 real corpus docs
    // (43%) - only 7 distinct literal patterns cover all occurrences, so literal alternates are
    // sufficient here rather than a whitespace-tolerant matching engine change.
    private static (string, List<LabelToMatch>) RuleRecords() =>
        (WrInspectionReportFieldNames.Records, [
            WrFluentRule
                .InOrder("R ecords", InOrderPossibilities, "Other provisions")
                .Named(WrInspectionReportFieldNames.Records)
                .AlsoStartsWith("R e cords", "R e c ords", "R e c o rds", "R e c o r ds", "R e c o r d s")
                .FromText()
                .Build()
        ]);

    // "Measurement details" is the section header that always follows the whole grid - a safe,
    // distant bound for each of these five fields regardless of which one is last on its row.
    private static (string, List<LabelToMatch>) RuleProvisionOfInformation() =>
        (WrInspectionReportFieldNames.ProvisionOfInformation, [
            WrFluentRule
                .InOrder("Provision of information", InOrderPossibilities, "Measurement details")
                .Named(WrInspectionReportFieldNames.ProvisionOfInformation)
                .FromText()
                .Build()]);

    // BoundByOtherLabels fixes a fabricated "InOrder" value caused by the same-line column walk
    // sweeping in an unrelated field with no positional bound.
    private static (string, List<LabelToMatch>) RuleSpecialConditions() =>
        (WrInspectionReportFieldNames.SpecialConditions, [
            WrFluentRule
                .InOrder("Special conditions", InOrderPossibilities, "Measurement details")
                .Named(WrInspectionReportFieldNames.SpecialConditions)
                .BoundByOtherLabels()
                .FromText()
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleLand() =>
        (WrInspectionReportFieldNames.Land, [
            WrFluentRule
                .InOrder("Land (only if specified)", InOrderPossibilities, "Measurement details")
                .Named(WrInspectionReportFieldNames.Land)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleChargingFactors() =>
        (WrInspectionReportFieldNames.ChargingFactors, [
            WrFluentRule
                .InOrder("Charging factors", InOrderPossibilities, "Measurement details")
                .Named(WrInspectionReportFieldNames.ChargingFactors)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleOtherProvisions() =>
        (WrInspectionReportFieldNames.OtherProvisions, [
            WrFluentRule
                .InOrder("Other provisions (specify below)", InOrderPossibilities, "Measurement details")
                .Named(WrInspectionReportFieldNames.OtherProvisions)
                .FromText()
                .Build()]);

    // ---- Header / address block ----

    private static (string, List<LabelToMatch>) RuleLicenceNumber() =>
        (WrInspectionReportFieldNames.LicenceNumber, [
            WrFluentRule
                .Between("Licence No. (or Application No. or GIC No. etc.)", "Inspection Class")
                .Named(WrInspectionReportFieldNames.LicenceNumber)
                .NextLines(1).RequireTextToClaimGroup()
                .AlsoEndsAt("Name and address", "Name / address")
                .FromLetterAndTableFreeText()
                .Build(), // Long form
            WrFluentRule
                .Between("Licence No", "Inspection Class").Named(WrInspectionReportFieldNames.LicenceNumber)
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
                .FromLetterAndTableFreeText()
                .Build() // Short form ("Licence No." / "Licence No:")
        ]);

    // "Met with" has no end bound, so a name wrapping onto the next physical line was
    // structurally unreachable - NextLines(1) alone doesn't help, since the actual winning
    // matcher here (ApplicableToMost's Text branch) ignores nextLines unless this flag is set.
    private static (string, List<LabelToMatch>) RuleMetWith() =>
        (WrInspectionReportFieldNames.MetWith, [
            WrFluentRule
                .After("Met with")
                .Named(WrInspectionReportFieldNames.MetWith)
                .NextLines(1)
                .AllowValueToWrapToNextLine()
                // Guards a document where "Met with" is already complete but the next row's
                // first column happens to be "Inspecting Officer: ..." at the same left margin.
                .SkipNextLineWhenStartsWith("Inspecting Officer")
                .FromLetterAndTableFreeText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleInspectingOfficer() =>
        (WrInspectionReportFieldNames.InspectingOfficer, [
            WrFluentRule
                .After("Inspecting Officer")
                .Named(WrInspectionReportFieldNames.InspectingOfficer)
                .FromLetterAndTableFreeText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleSiteAddress() =>
        (WrInspectionReportFieldNames.SiteAddress, [
            WrFluentRule
                .Between("Site address (if different)", "Met with")
                .Named(WrInspectionReportFieldNames.SiteAddress)
                .NextLines(10)
                .AlsoEndsAt("Email", "Inspecting Officer")
                .AlsoStartsWith("Site address (if different from above)")
                .SkipNextLineWhenStartsWith("Desktop Review", "Desktop:", "Site Visit: Desktop", "Liaised with")
                .FromText()
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleInspectionClass() =>
        (WrInspectionReportFieldNames.InspectionClass, [
            WrFluentRule
                .Between("Inspection Class", "Telephone No")
                .Named(WrInspectionReportFieldNames.InspectionClass).NextLines(1)
                .AlsoEndsAt(
                    "T e l e p h o n e N o", "T e l e p h o n e No", "T e le p h o n e No",
                    "Telepho n e N o", "T e lephone No", "T e l e phone No", "Email")
                .FromLetterAndTableFreeText()
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleTelephoneNumber() =>
        (WrInspectionReportFieldNames.TelephoneNumber, [
            WrFluentRule
                .Between("Telephone No", "Email")
                .Named(WrInspectionReportFieldNames.TelephoneNumber)
                .NextLines(2)
                .AlsoStartsWith(
                    "T e l e p h o n e N o", "T e l e p h o n e No", "T e le p h o n e No",
                    "Telepho n e N o", "T e lephone No", "T e l e phone No", "T e l N o")
                .FromLetterAndTableFreeText()
                .Build(),
            // Plain "Tel No" is a genuinely distinct label wording, not just an OCR-spaced
            // variant of "Telephone No" (see AlsoStartsWith above), so it needs its own
            // alternate. NextLines(0): the value always sits on the label's own row here -
            // reaching further risks sweeping in "Site address"/"Met with" content instead.
            WrFluentRule
                .Between("Tel No", "Site address")
                .Named(WrInspectionReportFieldNames.TelephoneNumber)
                .NextLines(0)
                .FromLetterAndTableFreeText()
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RulePosition() =>
        (WrInspectionReportFieldNames.Position, [
            WrFluentRule
                .Between("Position", "Inspection Date")
                .Named(WrInspectionReportFieldNames.Position)
                .NextLines(1)
                .FromLetterAndTableFreeText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleTime() =>
        (WrInspectionReportFieldNames.Time, [
            WrFluentRule
                .After("Time")
                .Named(WrInspectionReportFieldNames.Time)
                .AlsoStartsWithLoose("Time:")
                .FromLetterAndTableFreeText()
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleNameAndAddress() =>
        (WrInspectionReportFieldNames.NameAndAddress, [
            WrFluentRule
                .Between("Name and address", "Site address")
                .Named(WrInspectionReportFieldNames.NameAndAddress)
                .NextLines(10)
                .AlsoEndsAt(
                    "Telephone No", "Email",
                    "T e l e p h o n e N o", "T e l e p h o n e No", "T e le p h o n e No",
                    "Telepho n e N o", "T e lephone No", "T e l e phone No", "T e l N o")
                .FromLetterAndTableFreeText()
                .Build(), // Existing template
            WrFluentRule
                .After("Name / address")
                .Named(WrInspectionReportFieldNames.NameAndAddress)
                .FromLetterAndTableFreeText()
                .Build(), // Water Company template
            WrFluentRule
                .After("Name & address")
                .Named(WrInspectionReportFieldNames.NameAndAddress)
                .FromLetterAndTableFreeText()
                .Build(), // Water Company template
            WrFluentRule
                .After("Permit holder name and address")
                .Named(WrInspectionReportFieldNames.NameAndAddress)
                .NextLines(1)
                .Remove([new("Telephone No:")])
                .FromLetterAndTableFreeText()
                .Build() // Permit holder template
        ]);

    // ---- Meter / measurement details ----

    // Wide enough to fit a multi-meter table's full column of "Point N, <site>: <value>" lines
    // (see WrInspectionReportSchemaConverter's Meters-splitting logic) - harmless for a
    // single-meter document, since the same-line walk still stops at its own end boundary well
    // before running out of lines. Not used on TextEnd-less (plain After()) rules, which have no
    // boundary to stop at regardless of how many lines are available.
    private const int MeterTableNextLines = 10;

    private static (string, List<LabelToMatch>) RuleMeterName() =>
        (WrInspectionReportFieldNames.MeterName, [
            WrFluentRule
                .Between("Meter Name", "Meter Make")
                .Named(WrInspectionReportFieldNames.MeterName)
                .NextLines(MeterTableNextLines)
                .FromLetterAndTableFreeText()
                .Build()]); // T6 template only

    private static (string, List<LabelToMatch>) RuleMeterMake() =>
        (WrInspectionReportFieldNames.MeterMake, [
            WrFluentRule
                .Between("Meter make", "Reading:")
                .Named(WrInspectionReportFieldNames.MeterMake)
                .NextLines(MeterTableNextLines)
                .RequireTextToClaimGroup()
                .AlsoEndsAt("Serial number", "Meter Serial No", "Serial no")
                .FromText()
                .Build(), // Existing template
            WrFluentRule
                .Between("Meter Make", "Meter Serial Number")
                .Named(WrInspectionReportFieldNames.MeterMake)
                .NextLines(MeterTableNextLines)
                .RequireTextToClaimGroup()
                .AlsoEndsAt("Meter Serial No")
                .FromText()
                .Build() // T6 template
        ]);

    private static (string, List<LabelToMatch>) RuleSerialNumber() =>
        (WrInspectionReportFieldNames.SerialNumber, [
            // AlsoStartsWithLoose: "Meter make: <value> Serial number: N/A" is one
            // undifferentiated column on some real documents, so the column-start-only start
            // text never matches - same shape as Time sharing a row with Inspection Date.
            WrFluentRule
                .After("Serial number")
                .Named(WrInspectionReportFieldNames.SerialNumber)
                .RequireTextToClaimGroup()
                .AlsoStartsWithLoose("Serial number")
                .FromText()
                .Build(), // Existing template
            WrFluentRule
                .Between("Meter Serial Number", "Meter Asset Number")
                .Named(WrInspectionReportFieldNames.SerialNumber)
                .NextLines(MeterTableNextLines)
                .RequireTextToClaimGroup()
                .FromText()
                .Build(), // T6 template
            WrFluentRule
                .Between("Serial number", "Units")
                .Named(WrInspectionReportFieldNames.SerialNumber)
                .NextLines(MeterTableNextLines)
                .RequireTextToClaimGroup()
                .FromText()
                .Build() // Baseline two-column table
        ]);

    private static (string, List<LabelToMatch>) RuleMeterAssetNumber() =>
        (WrInspectionReportFieldNames.MeterAssetNumber, [
            WrFluentRule
                .Between("Meter Asset Number", "Meter Reading")
                .Named(WrInspectionReportFieldNames.MeterAssetNumber)
                .NextLines(MeterTableNextLines)
                .FromLetterAndTableFreeText()
                .Build(), // T6 template
            WrFluentRule
                .After("Asset no:")
                .Named(WrInspectionReportFieldNames.MeterAssetNumber)
                .FromLetterAndTableFreeText()
                .Build(), // Existing template
            WrFluentRule
                .After("Asset number:")
                .Named(WrInspectionReportFieldNames.MeterAssetNumber)
                .FromLetterAndTableFreeText()
                .Build() // Existing template
        ]);

    // "Reading" is a literal string prefix of the unrelated sibling label "Readings taken:" -
    // requiring the colon disambiguates both that collision and "Reading, RG8 7BB" (a town name).
    private static (string, List<LabelToMatch>) RuleReading() =>
        (WrInspectionReportFieldNames.Reading, [
            WrFluentRule
                .After("Reading:")
                .Named(WrInspectionReportFieldNames.Reading)
                .RequireTextToClaimGroup()
                .FromText()
                .Build(), // Existing template
            WrFluentRule
                .Between("Meter Reading", "Flow Rate")
                .Named(WrInspectionReportFieldNames.Reading)
                .NextLines(MeterTableNextLines)
                .RequireTextToClaimGroup()
                .FromText()
                .Build(), // T6 template
            // Added alongside the MeterTableNextLines widening: when the meter table's own
            // "Units" header genuinely never reappears (blank/N-A table), the wider window
            // otherwise bleeds into the next form section.
            WrFluentRule
                .Between("Reading:", "Units")
                .Named(WrInspectionReportFieldNames.Reading)
                .NextLines(MeterTableNextLines)
                .RequireTextToClaimGroup()
                .SkipNextLineWhenStartsWith("Other")
                .AlsoEndsAt("Other:-", "Certificates or records available for", "Date of certificate", "Meter verification")
                .FromText()
                .Build() // Baseline two-column table
        ]);

    private static (string, List<LabelToMatch>) RuleFlowRate() =>
        (WrInspectionReportFieldNames.FlowRate, [
            WrFluentRule
                .Between("Flow Rate", "Calibration")
                .Named(WrInspectionReportFieldNames.FlowRate)
                .NextLines(MeterTableNextLines)
                .FromLetterAndTableFreeText()
                .Build()]); // T6 template only

    private static (string, List<LabelToMatch>) RuleUnits() =>
        (WrInspectionReportFieldNames.Units, [
            WrFluentRule
                .After("Units")
                .Named(WrInspectionReportFieldNames.Units)
                .FromText()
                .Build(), // Existing template
            // "Flow Rate" is T6-only, so on T1 documents this end boundary never fires and the
            // widened window bleeds into the next section - same fix as Reading's AlsoEndsAt above.
            WrFluentRule
                .Between("Units", "Flow Rate")
                .Named(WrInspectionReportFieldNames.Units)
                .NextLines(MeterTableNextLines)
                .RequireTextToClaimGroup()
                .AlsoEndsAt("Other:-", "Certificates or records available for", "Date of certificate", "Meter verification")
                .FromText()
                .Build() // T6 template
        ]);

    private static (string, List<LabelToMatch>) RuleOther() =>
        (WrInspectionReportFieldNames.Other, [
            WrFluentRule
                .After("Other:")
                .Named(WrInspectionReportFieldNames.Other)
                .FromLetterAndTableFreeText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleCertificatesOfRecords() =>
        (WrInspectionReportFieldNames.CertificatesOfRecords, [
            WrFluentRule
                .After("Certificates or records available for")
                .Named(WrInspectionReportFieldNames.CertificatesOfRecords)
                // No end boundary previously - "Date of certificate or" (the NEXT field's own
                // label, wrapped mid-phrase by the same-line column split) sits in the very next
                // column and was being accepted as this field's own value.
                .EndsAt("Date of certificate")
                .FromLetterAndTableFreeText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleDateOfCertification() =>
        (WrInspectionReportFieldNames.DateOfCertification, [
            WrFluentRule
                .Between("Date of certificate or", "By whom")
                .Named(WrInspectionReportFieldNames.DateOfCertification)
                .NextLines(1)
                .Remove([new("record:"), new("Conformance:")])
                .FromText()
                .Build()
        ]);

    // A fourth layout beyond New/Existing/T6: "Calibration: Conformance: Flow verification:
    // Meter verification:" as one label row, answers on the row below in the same columns.
    private static (string, List<LabelToMatch>) RuleCalibration() =>
        (WrInspectionReportFieldNames.Calibration, [
            WrFluentRule
                .After("Calibration")
                .Named(WrInspectionReportFieldNames.Calibration)
                .NextLines(1)
                .RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")]).EndsAt("Conformance")
                .FromText()
                .Build(), // New template
            WrFluentRule
                .Between("Calibration", "Conformance").Named(WrInspectionReportFieldNames.Calibration).WholeLine()
                .RequireTextToClaimGroup()
                .Possibilities(CheckboxMarkPossibilities)
                .FromText()
                .Build(), // Existing template
            WrFluentRule
                .Between("Calibration", "Verification").Named(WrInspectionReportFieldNames.Calibration).NextLines(1).RequireTextToClaimGroup()
                .AlsoEndsAt("Conformance")
                .IgnoreIfContains([..VerificationGridSiblingLeakTerms, "Certificate"])
                .SkipNextLineWhenStartsWith("Maintenance")
                .FromText()
                .Build() // T6 / Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleVerification() =>
        (WrInspectionReportFieldNames.Verification, [
            WrFluentRule
                .Between("Verification", "Spot Check Result")
                .Named(WrInspectionReportFieldNames.Verification)
                .NextLines(1)
                .FromText()
                .Build()]); // T6 template only

    private static (string, List<LabelToMatch>) RuleSpotCheckResult() =>
        (WrInspectionReportFieldNames.SpotCheckResult, [
            WrFluentRule
                .Between("Spot Check Result", "General comments")
                .Named(WrInspectionReportFieldNames.SpotCheckResult)
                .NextLines(1)
                .Remove([new("–")])
                .FromText()
                .Build()
        ]); // T6 template only

    private static (string, List<LabelToMatch>) RuleConformance() =>
        (WrInspectionReportFieldNames.Conformance, [
            WrFluentRule
                .After("Conformance")
                .Named(WrInspectionReportFieldNames.Conformance)
                .NextLines(1)
                .RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")])
                .EndsAt("Flow verification")
                .FromText()
                .Build(), // New template
            WrFluentRule
                .Between("Conformance", "Flow verification")
                .Named(WrInspectionReportFieldNames.Conformance)
                .WholeLine()
                .RequireTextToClaimGroup()
                .Possibilities(CheckboxMarkPossibilities)
                .FromText()
                .Build(), // Existing template
            WrFluentRule
                .Between("Conformance", "Flow verification")
                .Named(WrInspectionReportFieldNames.Conformance)
                .NextLines(1)
                .RequireTextToClaimGroup()
                .IgnoreIfContains([..VerificationGridSiblingLeakTerms])
                .SkipNextLineWhenStartsWith("Maintenance")
                .FromText()
                .Build() // Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleFlowVerification() =>
        (WrInspectionReportFieldNames.FlowVerification, [
            WrFluentRule
                .After("Flow verification")
                .Named(WrInspectionReportFieldNames.FlowVerification)
                .NextLines(1)
                .RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")])
                .EndsAt("Meter verification")
                .FromText()
                .Build(), // New template
            WrFluentRule
                .Between("Flow verification", "Meter verification").Named(WrInspectionReportFieldNames.FlowVerification).WholeLine()
                .RequireTextToClaimGroup()
                .Possibilities(CheckboxMarkPossibilities)
                .FromText()
                .Build(), // Existing template
            WrFluentRule
                .Between("Flow verification", "Meter verification").Named(WrInspectionReportFieldNames.FlowVerification).NextLines(1)
                .RequireTextToClaimGroup()
                .IgnoreIfContains([..VerificationGridSiblingLeakTerms])
                .SkipNextLineWhenStartsWith("Maintenance")
                .FromText()
                .Build() // Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleMeterVerification() =>
        (WrInspectionReportFieldNames.MeterVerification, [
            WrFluentRule
                .After("Meter verification").Named(WrInspectionReportFieldNames.MeterVerification)
                .NextLines(1)
                .RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")])
                .EndsAt("Maintenance")
                .FromText()
                .Build(), // New template
            WrFluentRule
                .Between("Meter verification", "record").Named(WrInspectionReportFieldNames.MeterVerification).WholeLine()
                .RequireTextToClaimGroup()
                .Possibilities(CheckboxMarkPossibilities)
                .FromText()
                .Build(), // Existing template
            WrFluentRule
                .Between("Meter verification", "record").Named(WrInspectionReportFieldNames.MeterVerification).NextLines(1)
                .RequireTextToClaimGroup()
                .IgnoreIfContains([..VerificationGridSiblingLeakTerms])
                .SkipNextLineWhenStartsWith("Maintenance")
                .FromText()
                .Build() // Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleWhereKept() =>
        (WrInspectionReportFieldNames.WhereKept, [
            WrFluentRule
                .After("Where kept")
                .Named(WrInspectionReportFieldNames.WhereKept)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleFormSentTo() =>
        (WrInspectionReportFieldNames.FormSentTo, [
            WrFluentRule
                .Between("Form sent to", "Date")
                .Named(WrInspectionReportFieldNames.FormSentTo)
                .NextLines(1)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleDate() =>
        (WrInspectionReportFieldNames.Date, [
            WrFluentRule
                .After("Date:")
                .Named(WrInspectionReportFieldNames.Date)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleDocumentTemplateVersion() =>
        (WrInspectionReportFieldNames.DocumentTemplateVersion, [
            WrFluentRule
                .After("Document Template Version:")
                .Named(WrInspectionReportFieldNames.DocumentTemplateVersion)
                .FromText()
                .Build()]);

    private static (string, List<LabelToMatch>) RuleDocumentHeader() =>
        (WrInspectionReportFieldNames.DocumentHeader, [
            WrFluentRule
                .After("Form WR - ")
                .Named(WrInspectionReportFieldNames.DocumentHeader)
                .FromText()
                .Build()]);

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

    // The baseline heading alone only covers 61% of the real corpus, hence the "Actions"/
    // "Summary" alternates. Deliberately excluded from Remove: they can legitimately recur as a
    // genuine sub-heading later in the same captured block, and stripping them there corrupts
    // real narrative content.
    private static (string, List<LabelToMatch>) RuleGeneralComments() =>
        (WrInspectionReportFieldNames.GeneralComments, [
            WrFluentRule
                .Between(
                    "General comments, details / dates of occupation changes, actions required etc.",
                    "Form sent to")
                .Named(WrInspectionReportFieldNames.GeneralComments)
                .WholeLine()
                .NextLines(100)
                .AlsoEndsAt("Customer charter") // fixed appeal-process boilerplate on longer-form documents - never genuine comments content
                .AlsoStartsWith(
                    "Introduction",
                    "Re-inspection",
                    "Notes and Actions",
                    "Further Conditions",
                    "Actions/Recommendations",
                    "General comments / background",
                    "General comments / relevant background",
                    "General / relevant background",
                    "General comments, background",
                    "Actions",
                    "Summary")
                .ExceptFromRemove("Actions", "Summary")
                .FromText()
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleMaintenanceLine() =>
        (WrInspectionReportFieldNames.MaintenanceLine,
            MaintenanceLine("Maintenance:", "Readings taken",
                WrInspectionReportFieldNames.MaintenanceLine));

    private static (string, List<LabelToMatch>) RuleReadingsTakenLine() =>
        (WrInspectionReportFieldNames.ReadingsTakenLine,
            MaintenanceLine("Readings taken:", "Where Kept",
                WrInspectionReportFieldNames.ReadingsTakenLine));

    private static (string, List<LabelToMatch>) RuleInspectionDate() =>
        (WrInspectionReportFieldNames.InspectionDate, [
            // Both Require* calls let a blank/incomplete match fall through to the fallback
            // below instead of permanently claiming the group.
            WrFluentRule
                .Between("Inspection Date:", "Quantities")
                .Named(WrInspectionReportFieldNames.InspectionDate)
                .NextLines(2)
                .AlsoEndsAt("Time:", "Inspecting Officer")
                .RequireTextToClaimGroup()
                .RequireCompleteDateToClaimGroup()
                .FromText()
                .Build(),
            // Fallback for a squeezed layout where "Inspection Date:" wraps onto its own row
            // with "Inspecting Officer: ... Time: ..." landing between label and value. Anchors
            // on "Inspecting Officer" (a bare "Inspection" anchor also matches "Inspection
            // report"/"Inspection Class:") and walks WholeLine to "Licence provisions" to span
            // the wrap. "Date:" is stripped, not anchored on - it collides with "Date of
            // certificate or record:" elsewhere on the page, and was measured to regress recall
            // 98%->40% when tried directly.
            WrFluentRule
                .Between("Inspecting Officer", "Licence provisions")
                .Named(WrInspectionReportFieldNames.InspectionDate)
                .PreviousLines(1)
                .NextLines(3)
                .WholeLine()
                .Remove([new TextToMatch("Inspection Date:"), new TextToMatch("Inspection"), new TextToMatch("Date:")])
                .RequireTextToClaimGroup()
                .FromText()
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleEmail() =>
        (WrInspectionReportFieldNames.Email, [
            WrFluentRule
                .Between("Email", "Position:")
                .Named(WrInspectionReportFieldNames.Email)
                .NextLines(1)
                .FromLetterAndTableFreeText()
                .Build()]);

    // A pure presence check, not a value extraction - used for the WrTemplateType marker
    // fields. Kept outside the Rule fluent surface deliberately: it's a genuinely different
    // shape (IncludeStartLabelText guarantees a non-empty match whenever the marker is found,
    // even with nothing meaningful following it on the page).
    private static List<LabelToMatch> TemplateMarker(
        string text,
        string labelName,
        List<string>? additionalTextStarts = null)
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
                Name = labelName,
                LayoutExtractor = LayoutExtractor.LetterBased
            }
        ];
    }

    // A compound row (Maintenance:/Readings taken: plus Y/N, Frequency, By whom sub-fields) -
    // kept outside the Rule fluent surface since it's structurally more complex, not less (five
    // SubLabels sharing one parent row); the SubLabels themselves still use Rule.
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
                        ? WrFluentRule
                            .After("Maintenance:")
                            .Named($"{name}Maintenance")
                            .EndsAt("Frequency")
                            .FromText()
                        : WrFluentRule
                            .After("Readings taken:")
                            .Named($"{name}ReadingsTaken")
                            .EndsAt("Frequency")
                            .FromText())
                            .Build(),
                    (name == WrInspectionReportFieldNames.MaintenanceLine
                        ? WrFluentRule
                            .Between("Maintenance:", "N:")
                            .Named($"{name}MaintenanceYes")
                            .WholeLine()
                            .Possibilities([new("✓"), new(""), new("X")])
                            .FromText()
                        : WrFluentRule
                            .Between("Readings taken:", "N:")
                            .Named($"{name}ReadingsTakenYes")
                            .WholeLine()
                            .Possibilities([new("✓"), new(""), new("X")])
                            .FromText()
                        ).Build(),
                    (name == WrInspectionReportFieldNames.MaintenanceLine
                        ? WrFluentRule
                            .Between("N:", "Frequency:")
                            .Named($"{name}MaintenanceNo")
                            .WholeLine()
                            .Possibilities([new("✓"), new(""), new("X")])
                            .FromText()
                        : WrFluentRule
                            .Between("N:", "Frequency:")
                            .Named($"{name}ReadingsTakenNo")
                            .WholeLine()
                            .Possibilities([new("✓"), new(""), new("X")])
                            .FromText()
                        ).Build(),
                    WrFluentRule
                        .After("Frequency:")
                        .Named($"{name}Frequency")
                        .EndsAt("By whom")
                        .FromText()
                        .Build(),
                    WrFluentRule
                        .After("By whom:")
                        .Named($"{name}ByWhom")
                        .FromText()
                        .Build()
                ]
            }
        ];
    }
}
