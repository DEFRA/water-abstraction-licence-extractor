using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Constants;

namespace WRADI.Services.WrInspectionReport.Tests;

/// Pins GetT1Labels() against GetLabels(), and the ExceptWhenInsideWord guard on the checkbox
/// possibilities - both reintroduce silently, and neither needs the harness to catch.
public class WrInspectionReportLabelConfigurationTests
{
    [Fact]
    public void WhenBuildingT1Labels_ThenOnlyTheSixTemplateMarkerGroupsAreExcluded()
    {
        var allLabels = WrInspectionReportTextBasedLabelConfiguration.GetLabels();
        
        var generalGroupNames = allLabels
            .Select(l => l.LabelGroupName)
            .ToHashSet();
        
        var t1GroupNames = WrInspectionT1LabelConfiguration.FilterFrom(allLabels)
            .Select(l => l.LabelGroupName)
            .ToHashSet();

        var removed = generalGroupNames.Except(t1GroupNames).ToList();

        var expectedRemoved = new[]
        {
            WrInspectionReportFieldNames.TemplateMarkerT4, WrInspectionReportFieldNames.TemplateMarkerT6,
            WrInspectionReportFieldNames.TemplateMarkerT7, WrInspectionReportFieldNames.TemplateMarkerImpounding,
            WrInspectionReportFieldNames.TemplateMarkerBaselineComments, WrInspectionReportFieldNames.TemplateMarkerAlternateComments
        };

        Assert.Equal(expectedRemoved.OrderBy(n => n), removed.OrderBy(n => n));
    }

    [Fact]
    public void WhenBuildingT1Labels_ThenNameAndAddressDropsOnlyThePermitHolderAlternate()
    {
        var allLabels = WrInspectionReportTextBasedLabelConfiguration.GetLabels();
        
        var generalAlternateCount = allLabels
            .First(l => l.LabelGroupName == WrInspectionReportFieldNames.NameAndAddress).Labels.Count;
        var t1AlternateCount = WrInspectionT1LabelConfiguration.FilterFrom(allLabels)
            .First(l => l.LabelGroupName == WrInspectionReportFieldNames.NameAndAddress).Labels.Count;

        Assert.Equal(generalAlternateCount - 1, t1AlternateCount);
    }

    [Fact]
    public void WhenBuildingT1Labels_ThenGeneralCommentsHasOnlyTheBaselineHeading()
    {
        var allLabels = WrInspectionReportTextBasedLabelConfiguration.GetLabels();
        
        // One TextStart only: the NonStandardNarrative alternates cannot apply to a confirmed T1.
        var t1GeneralComments = WrInspectionT1LabelConfiguration.FilterFrom(allLabels)
            .First(l => l.LabelGroupName == WrInspectionReportFieldNames.GeneralComments).Labels.Single();
        var generalGeneralComments = allLabels
            .First(l => l.LabelGroupName == WrInspectionReportFieldNames.GeneralComments).Labels.Single();

        Assert.Single(t1GeneralComments.TextStart!);
        Assert.True(generalGeneralComments.TextStart!.Count > 1);
    }

    [Fact]
    public void WhenBuildingT1Labels_ThenEveryMeasurementDetailsAlternateSurvivesUnchanged()
    {
        // Every "T6 template" alternate turned out to have real T1 usage too - Calibration alone
        // lost 11% of its matches when pruned. Pinned so a future edit has to be deliberate.
        string[] fieldsThatMustStayIdentical =
        [
            WrInspectionReportFieldNames.MeterName, WrInspectionReportFieldNames.MeterMake,
            WrInspectionReportFieldNames.SerialNumber, WrInspectionReportFieldNames.MeterAssetNumber,
            WrInspectionReportFieldNames.Reading, WrInspectionReportFieldNames.Units,
            WrInspectionReportFieldNames.FlowRate, WrInspectionReportFieldNames.Calibration,
            WrInspectionReportFieldNames.Conformance, WrInspectionReportFieldNames.FlowVerification,
            WrInspectionReportFieldNames.MeterVerification, WrInspectionReportFieldNames.Verification,
            WrInspectionReportFieldNames.SpotCheckResult
        ];

        var allLabels = WrInspectionReportTextBasedLabelConfiguration.GetLabels();
        
        var general = allLabels.ToDictionary(l => l.LabelGroupName);
        var t1 = WrInspectionT1LabelConfiguration.FilterFrom(allLabels).ToDictionary(l => l.LabelGroupName);

        foreach (var fieldName in fieldsThatMustStayIdentical)
        {
            Assert.True(t1.ContainsKey(fieldName), $"{fieldName} should still exist in GetT1Labels()");
            Assert.Equal(general[fieldName].Labels.Count, t1[fieldName].Labels.Count);
        }
    }

    [Fact]
    public void WhenMatchingReadingsBaselineTwoColumnTableAlternate_ThenOtherIsExcludedAsANextLineCandidate()
    {
        // Where narrative merges into Reading's row, the next line the algorithm sees is "Other:"'s
        // own row - traced on wr51__83617s0016.
        var readingBaselineTwoColumnAlternate = WrInspectionReportTextBasedLabelConfiguration.GetLabels()
            .First(l => l.LabelGroupName == WrInspectionReportFieldNames.Reading).Labels
            .Single(l => l.TextEnd?.Any(t => t.Text == "Units") == true);

        Assert.Contains("Other", readingBaselineTwoColumnAlternate.LimitToExcludeNextLineIfFirstColumnStartsWith ?? []);
    }

    [Fact]
    public void WhenMatchingCalibrationsCheckboxPossibilities_ThenEveryEntryGuardsAgainstMatchingInsideAWord()
    {
        // Without the guard, a bare "N" possibility matches the "n" inside "verification" before
        // reaching a real tick. Calibration's "Existing template" alternate is the shape that
        // exposed it, via LimitTo.WholeLine.
        var calibration = WrInspectionReportTextBasedLabelConfiguration.GetLabels()
            .First(l => l.LabelGroupName == WrInspectionReportFieldNames.Calibration).Labels
            .Single(l => l.Possibilities?.Any() == true && l.LimitTo == WALE.ProcessFile.Core.Enums.LimitTo.WholeLine);

        Assert.NotEmpty(calibration.Possibilities!);
        Assert.All(calibration.Possibilities!, p => Assert.True(p.ExceptWhenInsideWord, $"'{p.Text}' is missing ExceptWhenInsideWord"));
    }
}