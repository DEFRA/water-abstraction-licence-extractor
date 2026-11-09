using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Models.Configuration;

namespace WRADI.DocumentType.WrInspectionReport.Configuration;

public static class WrInspectionT1LabelConfiguration
{
    // The "T6 template" alternates are kept: that name says where a phrasing was first found, not
    // which template uses it, and T1 documents do - dropping Calibration alone loses 51 of 480 T1
    // documents. Verify any change here against the full per-field coverage report over every T1
    // document, not the golden set; a narrower check missed this once.
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> FilterFrom(
        List<(string LabelGroupName, List<LabelToMatch> Labels)> inputLabels)
    {
        var labels = inputLabels
            .Where(label => label.LabelGroupName is not (
                "TemplateMarkerT4"
                or "TemplateMarkerT6"
                or "TemplateMarkerT7"
                or "TemplateMarkerImpounding"
                or "TemplateMarkerBaselineComments"
                or "TemplateMarkerAlternateComments"))
            .Select(l => l with { Labels = l.Labels.ToList() })
            .ToList();

        var nameAndAddress = labels.First(
            label => label.LabelGroupName == WrInspectionReportFieldNames.NameAndAddress);
        
        // TODO - fragile, look up on text being the below instead
        nameAndAddress.Labels.RemoveAt(3); // "Permit holder name and address" - T4 only, confirmed zero T1 usage

        var generalCommentsIndex = labels.FindIndex(
            label => label.LabelGroupName == WrInspectionReportFieldNames.GeneralComments);
        
        // Swap out how to find general comments
        labels[generalCommentsIndex] = (WrInspectionReportFieldNames.GeneralComments, [
            // Tried 2026-09-08 and reverted: "Actions" and "Page N of M" end-anchors to stop the
            // field short of a trailing footer. Net regression (Hit+PartialHit 36->35) - "Actions:"
            // is narrative inside wr51__1142109's own truth and a real heading elsewhere.
            WrFluentRule
                .Between("General comments, details / dates of occupation changes, actions required etc.", "Form sent to")
                .Named(WrInspectionReportFieldNames.GeneralComments)
                .WholeLine()
                .NextLines(100)
                .FromText()
                .Build()
        ]);

        return labels;
    }
}