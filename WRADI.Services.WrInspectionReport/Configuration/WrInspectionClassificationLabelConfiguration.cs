using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Constants;

namespace WRADI.DocumentType.WrInspectionReport.Configuration;

public static class WrInspectionClassificationLabelConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels() =>
        WrInspectionReportLabelConfiguration.GetLabels()
            .Where(l => ClassificationLabelGroupNames.Contains(l.LabelGroupName))
            .ToList();
    
    // Filtered out of GetLabels() by name rather than redefined for maintainability
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
}