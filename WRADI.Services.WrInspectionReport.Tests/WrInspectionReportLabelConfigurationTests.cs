using WRADI.DocumentType.WrInspectionReport.Configuration;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Regression coverage for the shape of GetT1Labels() vs GetLabels(), and for the
/// ExceptWhenInsideWord guard on the checkbox-style possibility fields - both were found the
/// hard way this session (a blanket prune attempt that measured as a real accuracy regression
/// against the golden set and full corpus, and a stray lowercase "n" winning
/// checkbox-possibility matches inside unrelated words). These are cheap to assert directly and
/// catch a silent reintroduction of either without needing to rerun the harness.
/// </summary>
public class WrInspectionReportLabelConfigurationTests
{
    [Fact]
    public void WhenBuildingT1Labels_ThenOnlyTheSixTemplateMarkerGroupsAreExcluded()
    {
        // GetT1Labels() is built additively from GetLabels() with the 6 classification-only
        // marker groups removed - see that method's own comment for the evidence that every
        // other "template-specific" alternate tried was actually needed by real T1 documents
        // too, and is deliberately NOT removed here.
        var generalGroupNames = WrInspectionReportLabelConfiguration.GetLabels()
            .Select(l => l.LabelGroupName)
            .ToHashSet();
        var t1GroupNames = WrInspectionReportLabelConfiguration.GetT1Labels()
            .Select(l => l.LabelGroupName)
            .ToHashSet();

        var removed = generalGroupNames.Except(t1GroupNames).ToList();

        var expectedRemoved = new[]
        {
            "TemplateMarkerT4", "TemplateMarkerT6", "TemplateMarkerT7", "TemplateMarkerImpounding",
            "TemplateMarkerBaselineComments", "TemplateMarkerAlternateComments"
        };

        Assert.Equal(expectedRemoved.OrderBy(n => n), removed.OrderBy(n => n));
    }

    [Fact]
    public void WhenBuildingT1Labels_ThenNameAndAddressDropsOnlyThePermitHolderAlternate()
    {
        var generalAlternateCount = WrInspectionReportLabelConfiguration.GetLabels()
            .First(l => l.LabelGroupName == "NameAndAddress").Labels.Count;
        var t1AlternateCount = WrInspectionReportLabelConfiguration.GetT1Labels()
            .First(l => l.LabelGroupName == "NameAndAddress").Labels.Count;

        Assert.Equal(generalAlternateCount - 1, t1AlternateCount);
    }

    [Fact]
    public void WhenBuildingT1Labels_ThenGeneralCommentsHasOnlyTheBaselineHeading()
    {
        // The T1 GeneralComments variant should have exactly one TextStart entry (the literal
        // baseline heading) - none of the NonStandardNarrative-family alternates
        // (Introduction/Notes and Actions/Actions/Summary/background variants/etc.) apply to a
        // document already confirmed T1.
        var t1GeneralComments = WrInspectionReportLabelConfiguration.GetT1Labels()
            .First(l => l.LabelGroupName == "GeneralComments").Labels.Single();
        var generalGeneralComments = WrInspectionReportLabelConfiguration.GetLabels()
            .First(l => l.LabelGroupName == "GeneralComments").Labels.Single();

        Assert.Single(t1GeneralComments.TextStart!);
        Assert.True(generalGeneralComments.TextStart!.Count > 1);
    }

    [Fact]
    public void WhenBuildingT1Labels_ThenEveryMeasurementDetailsAlternateSurvivesUnchanged()
    {
        // The decisive finding from the corpus-wide diff: every MeasurementDetails alternate
        // attributed to "T6 template" in its own comment turned out to have real usage among
        // T1-classified documents too (Calibration alone lost 11% of its real matches when
        // pruned). None of these fields should ever differ between the two rulesets again
        // without a fresh, evidenced corpus-wide check - this test just pins today's "unchanged"
        // state so a future edit has to deliberately touch it.
        string[] fieldsThatMustStayIdentical =
        [
            "MeterName", "MeterMake", "SerialNumber", "MeterAssetNumber", "Reading", "Units",
            "FlowRate", "Calibration", "Conformance", "FlowVerification", "MeterVerification",
            "Verification", "SpotCheckResult"
        ];

        var general = WrInspectionReportLabelConfiguration.GetLabels().ToDictionary(l => l.LabelGroupName);
        var t1 = WrInspectionReportLabelConfiguration.GetT1Labels().ToDictionary(l => l.LabelGroupName);

        foreach (var fieldName in fieldsThatMustStayIdentical)
        {
            Assert.True(t1.ContainsKey(fieldName), $"{fieldName} should still exist in GetT1Labels()");
            Assert.Equal(general[fieldName].Labels.Count, t1[fieldName].Labels.Count);
        }
    }

    [Fact]
    public void WhenMatchingReadingsBaselineTwoColumnTableAlternate_ThenOtherIsExcludedAsANextLineCandidate()
    {
        // The cross-row leak fix: on documents where narrative text merges into Reading's own
        // row under WR51's line-grouping tolerance (see FindLabelGroupMatchesHelper's own
        // ExcludeNextLineIfFirstColumnStartsWith usage), the literal next line the algorithm
        // sees can be "Other:"'s own row - traced with gated instrumentation on a real document
        // (wr51__83617s0016__...) before this guard was added, not assumed.
        var readingBaselineTwoColumnAlternate = WrInspectionReportLabelConfiguration.GetLabels()
            .First(l => l.LabelGroupName == "Reading").Labels
            .Single(l => l.TextEnd?.Any(t => t.Text == "Units") == true && l.NextLinesToFetch == 1);

        Assert.Contains("Other", readingBaselineTwoColumnAlternate.ExcludeNextLineIfFirstColumnStartsWith ?? []);
    }

    [Fact]
    public void WhenMatchingCalibrationsCheckboxPossibilities_ThenEveryEntryGuardsAgainstMatchingInsideAWord()
    {
        // The stray lowercase "n" bug: CheckboxMarkPossibilities previously had no
        // ExceptWhenInsideWord guard, so a bare "N" possibility won a match against the "n"
        // inside ordinary words like "verification" before ever reaching a real tick/cross
        // elsewhere in the same merged line. Confirmed on two independent real documents this
        // session. Calibration's "Existing template" alternate is the one that actually uses
        // this shared list with LimitTo.WholeLine, the shape that exposed the bug.
        var calibration = WrInspectionReportLabelConfiguration.GetLabels()
            .First(l => l.LabelGroupName == "Calibration").Labels
            .Single(l => l.Possibilities?.Any() == true && l.LimitTo == WALE.ProcessFile.Core.Enums.LimitTo.WholeLine);

        Assert.NotEmpty(calibration.Possibilities!);
        Assert.All(calibration.Possibilities!, p => Assert.True(p.ExceptWhenInsideWord, $"'{p.Text}' is missing ExceptWhenInsideWord"));
    }
}
