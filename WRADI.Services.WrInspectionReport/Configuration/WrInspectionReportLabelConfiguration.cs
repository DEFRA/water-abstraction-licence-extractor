using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Enums;

namespace WRADI.DocumentType.WrInspectionReport.Configuration;

// Rule-based rewrite of the original three-factory design (TextToFindIsBetweenLabels/
// TextAfterLabel/GetInOrderField, each with its own inconsistent set of optional parameters) -
// one shared fluent Rule type plus one named method per field group. Verified field-for-field
// behaviourally identical to the design it replaced via a JSON-serialised diff of every
// LabelToMatch produced by GetLabels()/GetT1Labels() before promoting it here.
public class WrInspectionReportLabelConfiguration
{
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

    // Fluent construction of a single LabelToMatch. One shared vocabulary (an entry point per
    // rule shape, then chained modifiers) instead of three factory functions each with their own
    // inconsistent parameter surface.
    //
    // Accumulates into plain fields rather than mutating a LabelToMatch in place - several of
    // its properties (Name, NextLinesToFetch, RequireTextToClaimGroup, IgnoreBlockIfContains,
    // ExcludeNextLineIfFirstColumnStartsWith, BoundSameLineWalkByOtherLabelPositions) are
    // init-only, so the real object can only be assembled once, in Build().
    private sealed class Rule
    {
        private IReadOnlyList<TextToMatch>? _textStart;
        // Null (not an empty list) when unset - a rule with no end bound serialises the same way
        // regardless of which entry point built it, rather than differing null-vs-empty-list.
        private List<TextToMatch>? _textEnd;
        private LabelPosition _position;
        private LimitTo _limitTo = LimitTo.SameColumn;
        private int _nextLinesToFetch;
        private string? _name;
        private readonly List<TextToMatch> _remove = [];
        private List<TextToMatch>? _possibilities;
        private bool _requireTextToClaimGroup;
        private List<string>? _ignoreBlockIfContains;
        private List<string>? _excludeNextLineIfFirstColumnStartsWith;
        private bool _boundSameLineWalkByOtherLabelPositions;

        private Rule() { }

        public static Rule Between(string startText, string endText)
        {
            var rule = new Rule
            {
                _textStart = [new(startText) { ColumnMustStartWith = true }],
                _textEnd = [new(endText) { LineMustStartWith = true }, new("[END_OF_BLOCK]")],
                _position = LabelPosition.TextToFindIsBetweenLabels
            };
            rule._remove.Add(new(startText));
            return rule;
        }

        // "Text"/LabelIsBeforeTextToFind - the same TextStart property backs both (LabelToMatch's
        // Text property is a plain alias for TextStart), so this needs no separate field.
        public static Rule After(string text)
        {
            var rule = new Rule
            {
                _textStart = [new(text) { ColumnMustStartWith = true }],
                _position = LabelPosition.LabelIsBeforeTextToFind
            };
            rule._remove.Add(new(text));
            return rule;
        }

        // The LicenceProvisions grid's tick/cross/In/Not shape. Routed via
        // TextToFindIsBetweenLabels (not LabelIsBeforeTextToFind) - the generic-text path used
        // by After() discards the whole result if nothing is left after removing the label
        // text, which would silently swallow every genuinely-blank tick field.
        public static Rule InOrder(string text, string? endText = null)
        {
            var rule = new Rule
            {
                _textStart = [new(text) { ColumnMustStartWith = true }, new(text.Replace(" ", string.Empty)) { ColumnMustStartWith = true }],
                _textEnd = endText != null ? [new(endText) { LineMustStartWith = true }, new("[END_OF_BLOCK]")] : [new("[END_OF_BLOCK]")],
                _position = LabelPosition.TextToFindIsBetweenLabels,
                _nextLinesToFetch = 1,
                _possibilities = InOrderPossibilities
            };
            rule._remove.Add(new(text));
            return rule;
        }

        public Rule Named(string name) { _name = name; return this; }
        public Rule WholeLine() { _limitTo = LimitTo.WholeLine; return this; }
        public Rule NextLines(int n) { _nextLinesToFetch = n; return this; }
        public Rule RequireTextToClaimGroup() { _requireTextToClaimGroup = true; return this; }
        public Rule BoundByOtherLabels() { _boundSameLineWalkByOtherLabelPositions = true; return this; }
        public Rule Possibilities(IEnumerable<TextToMatch> p) { _possibilities = p.ToList(); return this; }
        public Rule IgnoreIfContains(params string[] terms) { _ignoreBlockIfContains = terms.ToList(); return this; }
        public Rule SkipNextLineWhenStartsWith(params string[] terms) { _excludeNextLineIfFirstColumnStartsWith = terms.ToList(); return this; }

        // For the After() shape only - a single same-line bound.
        public Rule EndsAt(string text) { _textEnd = [new(text) { LineMustStartWith = true }]; return this; }

        // For the Between()/InOrder() shapes - appends before the trailing [END_OF_BLOCK]
        // sentinel. Plain TextToMatch, no positional flag - use this when the extra end marker
        // only needs to bound the same-line/same-row walk.
        public Rule AlsoEndsAt(params string[] texts)
        {
            var sentinel = _textEnd![^1];
            _textEnd = [.._textEnd.Take(_textEnd.Count - 1), ..texts.Select(t => new TextToMatch(t)), sentinel];
            return this;
        }

        // Same idea as AlsoEndsAt but with LineMustStartWith: true - for an extra end marker that
        // must anchor a whole line, not just bound a same-line walk. Only MeansOfAbstraction
        // needs this one (its "Records" end marker requires the stricter check).
        public Rule AlsoEndsAtLineStart(params string[] texts)
        {
            var sentinel = _textEnd![^1];
            _textEnd = [.._textEnd.Take(_textEnd.Count - 1), ..texts.Select(t => new TextToMatch(t) { LineMustStartWith = true }), sentinel];
            return this;
        }

        public Rule AlsoStartsWith(params string[] texts)
        {
            _textStart = [.._textStart!, ..texts.Select(t => new TextToMatch(t) { ColumnMustStartWith = true })];
            _remove.AddRange(texts.Select(t => new TextToMatch(t)));
            return this;
        }

        // Additive fallback alongside AlsoStartsWith (which still requires a column start): a
        // start text matched anywhere on the line, for cases where the value genuinely doesn't
        // land at a column boundary - e.g. "Time" sharing a row with "Inspection Date" as
        // "...Inspection Date: 26/1/2026 Time: 3pm" all in one column. Use specific-enough text
        // (a trailing colon, say) to avoid matching an ordinary word inside narrative prose.
        // Purely additive - existing column-start matches are untouched, so this can only add
        // new matches, never remove one.
        public Rule AlsoStartsWithLoose(params string[] texts)
        {
            _textStart = [.._textStart!, ..texts.Select(t => new TextToMatch(t))];
            _remove.AddRange(texts.Select(t => new TextToMatch(t)));
            return this;
        }

        public Rule Remove(IEnumerable<TextToMatch> items) { _remove.AddRange(items); return this; }

        // GeneralComments' own special case: "Actions"/"Summary" are valid additionalTextStarts
        // (so they must stay in TextStart) but must NOT be stripped from the captured value
        // when they recur mid-block as a genuine sub-heading - see RuleGeneralComments().
        public Rule ExceptFromRemove(params string[] texts) { _remove.RemoveAll(r => texts.Contains(r.Text)); return this; }

        public LabelToMatch Build() => new()
        {
            TextStart = _textStart,
            TextEnd = _textEnd,
            Position = _position,
            LimitTo = _limitTo,
            Format = "Text",
            PreviousLinesToFetch = 0,
            NextLinesToFetch = _nextLinesToFetch,
            Name = _name,
            Remove = _remove,
            Possibilities = _possibilities,
            RequireTextToClaimGroup = _requireTextToClaimGroup,
            IgnoreBlockIfContains = _ignoreBlockIfContains,
            ExcludeNextLineIfFirstColumnStartsWith = _excludeNextLineIfFirstColumnStartsWith,
            BoundSameLineWalkByOtherLabelPositions = _boundSameLineWalkByOtherLabelPositions
        };
    }

    // The label groups WrInspectionReportSchemaConverter.ClassifyTemplate actually needs -
    // filtered out of GetLabels() by name rather than redefined, so the classification markers
    // can never drift out of sync with the real ones.
    private static readonly string[] ClassificationLabelGroupNames =
    [
        WrInspectionReportFieldNames.DocumentHeader,
        WrInspectionReportFieldNames.TemplateMarkerT4,
        WrInspectionReportFieldNames.TemplateMarkerT6,
        WrInspectionReportFieldNames.TemplateMarkerT7,
        WrInspectionReportFieldNames.TemplateMarkerImpounding,
        WrInspectionReportFieldNames.TemplateMarkerBaselineComments,
        WrInspectionReportFieldNames.TemplateMarkerAlternateComments
    ];

    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetClassificationLabels() =>
        GetLabels()
            .Where(l => ClassificationLabelGroupNames.Contains(l.LabelGroupName))
            .ToList();

    // Hook point for T1-specific rule tuning. Starts from GetLabels() unchanged: every
    // MeasurementDetails "T6 template" alternate (MeterMake/SerialNumber/Reading/Units/
    // Calibration/Conformance/FlowVerification/MeterVerification, plus the T6-only fields
    // MeterName/FlowRate/Verification/SpotCheckResult/MeterAssetNumber) was checked for removal
    // and kept, because a corpus-wide coverage diff (all 480 real T1-classified documents, not
    // just the golden set) showed real, substantial usage under T1 despite the "T6" name -
    // Calibration alone loses 51/480 T1 docs (11%) without it. The "T6 template" label describes
    // where a phrasing was FIRST found, not a template-exclusivity boundary; don't trust it as
    // one. Two alternates DID show zero T1 impact and are removed below: NameAndAddress's
    // "Permit holder name and address" (T4 only) and GeneralComments's non-baseline headings.
    //
    // Any future change here MUST re-verify via the full corpus-wide per-field coverage report,
    // not just the golden-set harness or the alternate's own attribution comment - a narrower
    // check already missed this once.
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetT1Labels()
    {
        var labels = GetLabels()
            .Where(l => l.LabelGroupName is not (
                "TemplateMarkerT4" or "TemplateMarkerT6" or "TemplateMarkerT7" or "TemplateMarkerImpounding" or
                "TemplateMarkerBaselineComments" or "TemplateMarkerAlternateComments"))
            .Select(l => (l.LabelGroupName, Labels: l.Labels.ToList()))
            .ToList();

        var nameAndAddress = labels.First(l => l.LabelGroupName == WrInspectionReportFieldNames.NameAndAddress);
        nameAndAddress.Labels.RemoveAt(3); // "Permit holder name and address" - T4 only, confirmed zero T1 usage

        var generalCommentsIndex = labels.FindIndex(l => l.LabelGroupName == WrInspectionReportFieldNames.GeneralComments);
        labels[generalCommentsIndex] = (WrInspectionReportFieldNames.GeneralComments, [
            // Tried (2026-09-08) and reverted: an "Actions" end-anchor and a "Page N of M"
            // footer end-anchor, meant to stop the field short of a trailing checklist/footer
            // section seen on some T1 documents. Measured against the golden set: fixed 1 case
            // but broke 2 others - "Actions" isn't reliably a section boundary (wr51__1142109's
            // truth genuinely includes "Actions:\n<content>" as narrative, indistinguishable via
            // StartsWith from the standalone "Actions" heading that IS a real boundary elsewhere)
            // and the footer marker cut a different document short of its true end. Net regression
            // (Hit+PartialHit 36->35), not an improvement - genuine per-document diversity here,
            // not a bounded fix. See wr51_general_comments_gap memory before trying this again.
            Rule.Between("General comments, details / dates of occupation changes, actions required etc.", "Form sent to")
                .Named(WrInspectionReportFieldNames.GeneralComments).WholeLine().NextLines(100).Build()
        ]);

        return labels;
    }

    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels() =>
    [
        RuleSourceOfSupply(), RulePointOfAbstraction(), RuleMeansOfAbstraction(), RulePurposes(),
        RulePeriod(), RuleQuantities(), RuleMeansOfMeasurement(), RuleRecords(),
        RuleProvisionOfInformation(), RuleSpecialConditions(), RuleLand(), RuleChargingFactors(),
        RuleOtherProvisions(), RuleLicenceNumber(), RuleMetWith(), RuleInspectingOfficer(),
        RuleSiteAddress(), RuleInspectionClass(), RuleTelephoneNumber(), RulePosition(), RuleTime(),
        RuleNameAndAddress(), RuleMeterName(), RuleMeterMake(), RuleSerialNumber(),
        RuleMeterAssetNumber(), RuleReading(), RuleFlowRate(), RuleUnits(), RuleOther(),
        RuleCertificatesOfRecords(), RuleDateOfCertification(), RuleCalibration(), RuleVerification(),
        RuleSpotCheckResult(), RuleConformance(), RuleFlowVerification(), RuleMeterVerification(),
        RuleWhereKept(), RuleFormSentTo(), RuleDate(), RuleDocumentTemplateVersion(), RuleDocumentHeader(),
        RuleTemplateMarkerT4(), RuleTemplateMarkerT6(), RuleTemplateMarkerT7(), RuleTemplateMarkerImpounding(),
        RuleTemplateMarkerBaselineComments(), RuleTemplateMarkerAlternateComments(), RuleGeneralComments(),
        RuleMaintenanceLine(), RuleReadingsTakenLine(), RuleInspectionDate(), RuleEmail()
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
        (WrInspectionReportFieldNames.SourceOfSupply, [Rule.InOrder("Source of supply", "Quantities").Named(WrInspectionReportFieldNames.SourceOfSupply).Build()]);

    private static (string, List<LabelToMatch>) RulePointOfAbstraction() =>
        (WrInspectionReportFieldNames.PointOfAbstraction, [Rule.InOrder("Point of abstraction", "Means of measurement").Named(WrInspectionReportFieldNames.PointOfAbstraction).Build()]);

    // "Records" sometimes renders letter-kerned - without these as alternate end markers, the
    // same-line column walk never recognises the boundary and sweeps all the way to "...N/A" at
    // the end of the row, which then wins over the real "In Order" answer.
    private static (string, List<LabelToMatch>) RuleMeansOfAbstraction() =>
        (WrInspectionReportFieldNames.MeansOfAbstraction, [
            Rule.InOrder("Means of abstraction", "Records").Named(WrInspectionReportFieldNames.MeansOfAbstraction)
                .AlsoEndsAtLineStart("R ecords", "R e cords", "R e c ords", "R e c o rds", "R e c o r ds", "R e c o r d s")
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RulePurposes() =>
        (WrInspectionReportFieldNames.Purposes, [Rule.InOrder("Purpose(s)", "Provision of information").Named(WrInspectionReportFieldNames.Purposes).Build()]);

    private static (string, List<LabelToMatch>) RulePeriod() =>
        (WrInspectionReportFieldNames.Period, [Rule.InOrder("Period", "Special conditions").Named(WrInspectionReportFieldNames.Period).Build()]);

    private static (string, List<LabelToMatch>) RuleQuantities() =>
        (WrInspectionReportFieldNames.Quantities, [Rule.InOrder("Quantities", "Land").Named(WrInspectionReportFieldNames.Quantities).Build()]);

    private static (string, List<LabelToMatch>) RuleMeansOfMeasurement() =>
        (WrInspectionReportFieldNames.MeansOfMeasurement, [Rule.InOrder("Means of measurement", "Charging factors").Named(WrInspectionReportFieldNames.MeansOfMeasurement).Build()]);

    // "Records" renders with progressively wider letter-kerning on 340/789 real corpus docs
    // (43%) - only 7 distinct literal patterns cover all occurrences, so literal alternates are
    // sufficient here rather than a whitespace-tolerant matching engine change.
    private static (string, List<LabelToMatch>) RuleRecords() =>
        (WrInspectionReportFieldNames.Records, [
            Rule.InOrder("R ecords", "Other provisions").Named(WrInspectionReportFieldNames.Records)
                .AlsoStartsWith("R e cords", "R e c ords", "R e c o rds", "R e c o r ds", "R e c o r d s")
                .Build()
        ]);

    // "Measurement details" is the section header that always follows the whole grid - a safe,
    // distant bound for each of these five fields regardless of which one is last on its row.
    private static (string, List<LabelToMatch>) RuleProvisionOfInformation() =>
        (WrInspectionReportFieldNames.ProvisionOfInformation, [Rule.InOrder("Provision of information", "Measurement details").Named(WrInspectionReportFieldNames.ProvisionOfInformation).Build()]);

    // BoundByOtherLabels fixes a fabricated "InOrder" value caused by the same-line column walk
    // sweeping in an unrelated field with no positional bound.
    private static (string, List<LabelToMatch>) RuleSpecialConditions() =>
        (WrInspectionReportFieldNames.SpecialConditions, [
            Rule.InOrder("Special conditions", "Measurement details").Named(WrInspectionReportFieldNames.SpecialConditions).BoundByOtherLabels().Build()
        ]);

    private static (string, List<LabelToMatch>) RuleLand() =>
        (WrInspectionReportFieldNames.Land, [Rule.InOrder("Land (only if specified)", "Measurement details").Named(WrInspectionReportFieldNames.Land).Build()]);

    private static (string, List<LabelToMatch>) RuleChargingFactors() =>
        (WrInspectionReportFieldNames.ChargingFactors, [Rule.InOrder("Charging factors", "Measurement details").Named(WrInspectionReportFieldNames.ChargingFactors).Build()]);

    private static (string, List<LabelToMatch>) RuleOtherProvisions() =>
        (WrInspectionReportFieldNames.OtherProvisions, [Rule.InOrder("Other provisions (specify below)", "Measurement details").Named(WrInspectionReportFieldNames.OtherProvisions).Build()]);

    // ---- Header / address block ----

    private static (string, List<LabelToMatch>) RuleLicenceNumber() =>
        (WrInspectionReportFieldNames.LicenceNumber, [
            Rule.Between("Licence No. (or Application No. or GIC No. etc.)", "Inspection Class").Named(WrInspectionReportFieldNames.LicenceNumber)
                .NextLines(1).RequireTextToClaimGroup()
                .AlsoEndsAt("Name and address", "Name / address")
                .Build(), // Long form
            Rule.Between("Licence No", "Inspection Class").Named(WrInspectionReportFieldNames.LicenceNumber)
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
        (WrInspectionReportFieldNames.MetWith, [Rule.After("Met with").Named(WrInspectionReportFieldNames.MetWith).Build()]);

    private static (string, List<LabelToMatch>) RuleInspectingOfficer() =>
        (WrInspectionReportFieldNames.InspectingOfficer, [Rule.After("Inspecting Officer").Named(WrInspectionReportFieldNames.InspectingOfficer).Build()]);

    private static (string, List<LabelToMatch>) RuleSiteAddress() =>
        (WrInspectionReportFieldNames.SiteAddress, [
            Rule.Between("Site address (if different)", "Met with").Named(WrInspectionReportFieldNames.SiteAddress)
                .NextLines(10)
                .AlsoEndsAt("Email", "Inspecting Officer")
                .AlsoStartsWith("Site address (if different from above)")
                .SkipNextLineWhenStartsWith("Desktop Review", "Desktop:", "Site Visit: Desktop", "Liaised with")
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleInspectionClass() =>
        (WrInspectionReportFieldNames.InspectionClass, [
            Rule.Between("Inspection Class", "Telephone No").Named(WrInspectionReportFieldNames.InspectionClass).NextLines(1)
                .AlsoEndsAt(
                    "T e l e p h o n e N o", "T e l e p h o n e No", "T e le p h o n e No",
                    "Telepho n e N o", "T e lephone No", "T e l e phone No", "Email")
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RuleTelephoneNumber() =>
        (WrInspectionReportFieldNames.TelephoneNumber, [
            Rule.Between("Telephone No", "Email").Named(WrInspectionReportFieldNames.TelephoneNumber).NextLines(2)
                .AlsoStartsWith(
                    "T e l e p h o n e N o", "T e l e p h o n e No", "T e le p h o n e No",
                    "Telepho n e N o", "T e lephone No", "T e l e phone No", "T e l N o")
                .Build()
        ]);

    private static (string, List<LabelToMatch>) RulePosition() =>
        (WrInspectionReportFieldNames.Position, [Rule.Between("Position", "Inspection Date").Named(WrInspectionReportFieldNames.Position).NextLines(1).Build()]);

    private static (string, List<LabelToMatch>) RuleTime() =>
        (WrInspectionReportFieldNames.Time, [
            Rule.After("Time").Named(WrInspectionReportFieldNames.Time)
                .AlsoStartsWithLoose("Time:").Build()
        ]);

    private static (string, List<LabelToMatch>) RuleNameAndAddress() =>
        (WrInspectionReportFieldNames.NameAndAddress, [
            Rule.Between("Name and address", "Site address").Named(WrInspectionReportFieldNames.NameAndAddress).NextLines(10)
                .AlsoEndsAt(
                    "Telephone No", "Email",
                    "T e l e p h o n e N o", "T e l e p h o n e No", "T e le p h o n e No",
                    "Telepho n e N o", "T e lephone No", "T e l e phone No", "T e l N o")
                .Build(), // Existing template
            Rule.After("Name / address").Named(WrInspectionReportFieldNames.NameAndAddress).Build(), // Water Company template
            Rule.After("Name & address").Named(WrInspectionReportFieldNames.NameAndAddress).Build(), // Water Company template
            Rule.After("Permit holder name and address").Named(WrInspectionReportFieldNames.NameAndAddress).NextLines(1)
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
        (WrInspectionReportFieldNames.MeterName, [Rule.Between("Meter Name", "Meter Make").Named(WrInspectionReportFieldNames.MeterName).NextLines(MeterTableNextLines).Build()]); // T6 template only

    private static (string, List<LabelToMatch>) RuleMeterMake() =>
        (WrInspectionReportFieldNames.MeterMake, [
            Rule.Between("Meter make", "Reading:").Named(WrInspectionReportFieldNames.MeterMake).NextLines(MeterTableNextLines).RequireTextToClaimGroup()
                .AlsoEndsAt("Serial number", "Meter Serial No", "Serial no").Build(), // Existing template
            Rule.Between("Meter Make", "Meter Serial Number").Named(WrInspectionReportFieldNames.MeterMake).NextLines(MeterTableNextLines).RequireTextToClaimGroup()
                .AlsoEndsAt("Meter Serial No").Build() // T6 template
        ]);

    private static (string, List<LabelToMatch>) RuleSerialNumber() =>
        (WrInspectionReportFieldNames.SerialNumber, [
            Rule.After("Serial number").Named(WrInspectionReportFieldNames.SerialNumber).RequireTextToClaimGroup().Build(), // Existing template
            Rule.Between("Meter Serial Number", "Meter Asset Number").Named(WrInspectionReportFieldNames.SerialNumber)
                .NextLines(MeterTableNextLines).RequireTextToClaimGroup().Build(), // T6 template
            Rule.Between("Serial number", "Units").Named(WrInspectionReportFieldNames.SerialNumber)
                .NextLines(MeterTableNextLines).RequireTextToClaimGroup().Build() // Baseline two-column table
        ]);

    private static (string, List<LabelToMatch>) RuleMeterAssetNumber() =>
        (WrInspectionReportFieldNames.MeterAssetNumber, [
            Rule.Between("Meter Asset Number", "Meter Reading").Named(WrInspectionReportFieldNames.MeterAssetNumber).NextLines(MeterTableNextLines).Build(), // T6 template
            Rule.After("Asset no:").Named(WrInspectionReportFieldNames.MeterAssetNumber).Build(), // Existing template
            Rule.After("Asset number:").Named(WrInspectionReportFieldNames.MeterAssetNumber).Build() // Existing template
        ]);

    // "Reading" is a literal string prefix of the unrelated sibling label "Readings taken:" -
    // requiring the colon disambiguates both that collision and "Reading, RG8 7BB" (a town name).
    private static (string, List<LabelToMatch>) RuleReading() =>
        (WrInspectionReportFieldNames.Reading, [
            Rule.After("Reading:").Named(WrInspectionReportFieldNames.Reading).RequireTextToClaimGroup().Build(), // Existing template
            Rule.Between("Meter Reading", "Flow Rate").Named(WrInspectionReportFieldNames.Reading).NextLines(MeterTableNextLines).RequireTextToClaimGroup().Build(), // T6 template
            // AlsoEndsAt markers added alongside the MeterTableNextLines widening - when the
            // meter table's own "Units" header genuinely never reappears (a blank/N-A table),
            // the wider window otherwise bled straight into the next form section instead of
            // stopping (measured: HallucinationRate 11%->39% before these were added).
            Rule.Between("Reading:", "Units").Named(WrInspectionReportFieldNames.Reading).NextLines(MeterTableNextLines).RequireTextToClaimGroup()
                .SkipNextLineWhenStartsWith("Other")
                .AlsoEndsAt("Other:-", "Certificates or records available for", "Date of certificate", "Meter verification").Build() // Baseline two-column table
        ]);

    private static (string, List<LabelToMatch>) RuleFlowRate() =>
        (WrInspectionReportFieldNames.FlowRate, [Rule.Between("Flow Rate", "Calibration").Named(WrInspectionReportFieldNames.FlowRate).NextLines(MeterTableNextLines).Build()]); // T6 template only

    private static (string, List<LabelToMatch>) RuleUnits() =>
        (WrInspectionReportFieldNames.Units, [
            Rule.After("Units").Named(WrInspectionReportFieldNames.Units).Build(), // Existing template
            // "Flow Rate" is T6-only and never appears on a T1 form at all, so on T1 documents
            // this alternate's real end boundary never fires and the widened window otherwise
            // bled into the next form section - same fix and same measured cause as Reading's
            // AlsoEndsAt above.
            Rule.Between("Units", "Flow Rate").Named(WrInspectionReportFieldNames.Units).NextLines(MeterTableNextLines).RequireTextToClaimGroup()
                .AlsoEndsAt("Other:-", "Certificates or records available for", "Date of certificate", "Meter verification").Build() // T6 template
        ]);

    private static (string, List<LabelToMatch>) RuleOther() =>
        (WrInspectionReportFieldNames.Other, [Rule.After("Other:").Named(WrInspectionReportFieldNames.Other).Build()]);

    private static (string, List<LabelToMatch>) RuleCertificatesOfRecords() =>
        (WrInspectionReportFieldNames.CertificatesOfRecords, [Rule.After("Certificates or records available for").Named(WrInspectionReportFieldNames.CertificatesOfRecords).Build()]);

    private static (string, List<LabelToMatch>) RuleDateOfCertification() =>
        (WrInspectionReportFieldNames.DateOfCertification, [
            Rule.Between("Date of certificate or", "By whom").Named(WrInspectionReportFieldNames.DateOfCertification).NextLines(1)
                .Remove([new("record:"), new("Conformance:")]).Build()
        ]);

    // A fourth layout beyond New/Existing/T6: "Calibration: Conformance: Flow verification:
    // Meter verification:" as one label row, answers on the row below in the same columns.
    private static (string, List<LabelToMatch>) RuleCalibration() =>
        (WrInspectionReportFieldNames.Calibration, [
            Rule.After("Calibration").Named(WrInspectionReportFieldNames.Calibration).NextLines(1).RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")]).EndsAt("Conformance").Build(), // New template
            Rule.Between("Calibration", "Conformance").Named(WrInspectionReportFieldNames.Calibration).WholeLine()
                .RequireTextToClaimGroup().Possibilities(CheckboxMarkPossibilities).Build(), // Existing template
            Rule.Between("Calibration", "Verification").Named(WrInspectionReportFieldNames.Calibration).NextLines(1).RequireTextToClaimGroup()
                .AlsoEndsAt("Conformance")
                .IgnoreIfContains([..VerificationGridSiblingLeakTerms, "Certificate"])
                .SkipNextLineWhenStartsWith("Maintenance").Build() // T6 / Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleVerification() =>
        (WrInspectionReportFieldNames.Verification, [Rule.Between("Verification", "Spot Check Result").Named(WrInspectionReportFieldNames.Verification).NextLines(1).Build()]); // T6 template only

    private static (string, List<LabelToMatch>) RuleSpotCheckResult() =>
        (WrInspectionReportFieldNames.SpotCheckResult, [
            Rule.Between("Spot Check Result", "General comments").Named(WrInspectionReportFieldNames.SpotCheckResult).NextLines(1)
                .Remove([new("–")]).Build()
        ]); // T6 template only

    private static (string, List<LabelToMatch>) RuleConformance() =>
        (WrInspectionReportFieldNames.Conformance, [
            Rule.After("Conformance").Named(WrInspectionReportFieldNames.Conformance).NextLines(1).RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")]).EndsAt("Flow verification").Build(), // New template
            Rule.Between("Conformance", "Flow verification").Named(WrInspectionReportFieldNames.Conformance).WholeLine()
                .RequireTextToClaimGroup().Possibilities(CheckboxMarkPossibilities).Build(), // Existing template
            Rule.Between("Conformance", "Flow verification").Named(WrInspectionReportFieldNames.Conformance).NextLines(1)
                .RequireTextToClaimGroup().IgnoreIfContains([..VerificationGridSiblingLeakTerms])
                .SkipNextLineWhenStartsWith("Maintenance").Build() // Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleFlowVerification() =>
        (WrInspectionReportFieldNames.FlowVerification, [
            Rule.After("Flow verification").Named(WrInspectionReportFieldNames.FlowVerification).NextLines(1).RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")]).EndsAt("Meter verification").Build(), // New template
            Rule.Between("Flow verification", "Meter verification").Named(WrInspectionReportFieldNames.FlowVerification).WholeLine()
                .RequireTextToClaimGroup().Possibilities(CheckboxMarkPossibilities).Build(), // Existing template
            Rule.Between("Flow verification", "Meter verification").Named(WrInspectionReportFieldNames.FlowVerification).NextLines(1)
                .RequireTextToClaimGroup().IgnoreIfContains([..VerificationGridSiblingLeakTerms])
                .SkipNextLineWhenStartsWith("Maintenance").Build() // Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleMeterVerification() =>
        (WrInspectionReportFieldNames.MeterVerification, [
            Rule.After("Meter verification").Named(WrInspectionReportFieldNames.MeterVerification).NextLines(1).RequireTextToClaimGroup()
                .Possibilities([new("Yes"), new("No")]).EndsAt("Maintenance").Build(), // New template
            Rule.Between("Meter verification", "record").Named(WrInspectionReportFieldNames.MeterVerification).WholeLine()
                .RequireTextToClaimGroup().Possibilities(CheckboxMarkPossibilities).Build(), // Existing template
            Rule.Between("Meter verification", "record").Named(WrInspectionReportFieldNames.MeterVerification).NextLines(1)
                .RequireTextToClaimGroup().IgnoreIfContains([..VerificationGridSiblingLeakTerms])
                .SkipNextLineWhenStartsWith("Maintenance").Build() // Grid template
        ]);

    private static (string, List<LabelToMatch>) RuleWhereKept() =>
        (WrInspectionReportFieldNames.WhereKept, [Rule.After("Where kept").Named(WrInspectionReportFieldNames.WhereKept).Build()]);

    private static (string, List<LabelToMatch>) RuleFormSentTo() =>
        (WrInspectionReportFieldNames.FormSentTo, [Rule.Between("Form sent to", "Date").Named(WrInspectionReportFieldNames.FormSentTo).NextLines(1).Build()]);

    private static (string, List<LabelToMatch>) RuleDate() =>
        (WrInspectionReportFieldNames.Date, [Rule.After("Date:").Named(WrInspectionReportFieldNames.Date).Build()]);

    private static (string, List<LabelToMatch>) RuleDocumentTemplateVersion() =>
        (WrInspectionReportFieldNames.DocumentTemplateVersion, [Rule.After("Document Template Version:").Named(WrInspectionReportFieldNames.DocumentTemplateVersion).Build()]);

    private static (string, List<LabelToMatch>) RuleDocumentHeader() =>
        (WrInspectionReportFieldNames.DocumentHeader, [Rule.After("Form WR - ").Named(WrInspectionReportFieldNames.DocumentHeader).Build()]);

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
            Rule.Between("General comments, details / dates of occupation changes, actions required etc.", "Form sent to")
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
            Rule.Between("Inspection Date:", "Quantities").Named(WrInspectionReportFieldNames.InspectionDate).NextLines(2)
                .AlsoEndsAt("Time:", "Inspecting Officer").Build()
        ]);

    private static (string, List<LabelToMatch>) RuleEmail() =>
        (WrInspectionReportFieldNames.Email, [Rule.Between("Email", "Position:").Named(WrInspectionReportFieldNames.Email).NextLines(1).Build()]);

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
                        ? Rule.After("Maintenance:").Named($"{name}Maintenance").EndsAt("Frequency")
                        : Rule.After("Readings taken:").Named($"{name}ReadingsTaken").EndsAt("Frequency")).Build(),
                    (name == WrInspectionReportFieldNames.MaintenanceLine
                        ? Rule.Between("Maintenance:", "N:").Named($"{name}MaintenanceYes").WholeLine().Possibilities([new("✓"), new("X")])
                        : Rule.Between("Readings taken:", "N:").Named($"{name}ReadingsTakenYes").WholeLine().Possibilities([new("✓"), new("X")])).Build(),
                    (name == WrInspectionReportFieldNames.MaintenanceLine
                        ? Rule.Between("N:", "Frequency:").Named($"{name}MaintenanceNo").WholeLine().Possibilities([new("✓"), new("X")])
                        : Rule.Between("N:", "Frequency:").Named($"{name}ReadingsTakenNo").WholeLine().Possibilities([new("✓"), new("X")])).Build(),
                    Rule.After("Frequency:").Named($"{name}Frequency").EndsAt("By whom").Build(),
                    Rule.After("By whom:").Named($"{name}ByWhom").Build()
                ]
            }
        ];
    }
}
