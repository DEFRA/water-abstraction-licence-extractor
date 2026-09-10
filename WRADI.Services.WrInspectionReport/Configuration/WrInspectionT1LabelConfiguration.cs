using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Models.Configuration;

namespace WRADI.DocumentType.WrInspectionReport.Configuration;

public static class WrInspectionT1LabelConfiguration
{
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
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        var labels = WrInspectionReportLabelConfiguration.GetLabels()
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
            // Tried (2026-09-08) and reverted: an "Actions" end-anchor and a "Page N of M"
            // footer end-anchor, meant to stop the field short of a trailing checklist/footer
            // section seen on some T1 documents. Measured against the golden set: fixed 1 case
            // but broke 2 others - "Actions" isn't reliably a section boundary (wr51__1142109's
            // truth genuinely includes "Actions:\n<content>" as narrative, indistinguishable via
            // StartsWith from the standalone "Actions" heading that IS a real boundary elsewhere)
            // and the footer marker cut a different document short of its true end. Net regression
            // (Hit+PartialHit 36->35), not an improvement - genuine per-document diversity here,
            // not a bounded fix. See wr51_general_comments_gap memory before trying this again.
            WrRule
                .Between("General comments, details / dates of occupation changes, actions required etc.", "Form sent to")
                .Named(WrInspectionReportFieldNames.GeneralComments)
                .WholeLine()
                .NextLines(100)
                .Build()
        ]);

        return labels;
    }
}