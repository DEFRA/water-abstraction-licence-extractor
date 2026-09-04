using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;

namespace WRADI.DocumentType.WrInspectionReport.Services;

/// <summary>
/// Two-pass extraction: a cheap first pass with only the classification label groups
/// (WrInspectionReportLabelConfiguration.GetClassificationLabels - 7 groups) decides
/// Metadata.Template, then a second pass runs GetT1Labels() or GetLabels() depending on that
/// result. Exists so a T1-specific rule change (once one has real evidence behind it - see the
/// wr51_column_walk_bug memory) can be made in GetT1Labels() alone, with no way to affect any
/// other template's documents, rather than needing a shared field's behaviour to be correct for
/// every template simultaneously - that's exactly the constraint that made the two earlier
/// WalkSameLineColumns fix attempts unsafe.
///
/// The classification pass always runs with UseLockExclusivity forced off - it's a throwaway
/// probe, not the result callers actually want, and mustn't take a real DMS lock or write a
/// stub matches-result row for it. The second, real pass keeps whatever locking behaviour the
/// caller's own configuration asked for, exactly matching what a single-pass call would have
/// done.
/// </summary>
public static class WrInspectionReportExtractionOrchestrator
{
    public static async Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item, WrTemplateType Template)> ExtractAsync(
        string pdfFileName,
        DmsFileData dmsDataForFile,
        LookupConfiguration configuration,
        List<string> previouslyParsedFiles,
        int processRunId,
        IPdfDataExtractorService pdfDataExtractor)
    {
        var classificationConfiguration = configuration.Clone();
        classificationConfiguration.Labels = WrInspectionReportLabelConfiguration.GetClassificationLabels();
        classificationConfiguration.UseLockExclusivity = false;

        var (classificationStopExecution, _, classificationResult) = await pdfDataExtractor.GetMatchesAsync(
            pdfFileName,
            dmsDataForFile,
            classificationConfiguration,
            previouslyParsedFiles,
            processRunId);

        if (classificationStopExecution || classificationResult == null)
        {
            return (classificationStopExecution, null, null, WrTemplateType.Unknown);
        }

        var documentHeader = WrInspectionReportSchemaConverter.GetMultilineText(classificationResult, "DocumentHeader");
        var template = WrInspectionReportSchemaConverter.ClassifyTemplate(classificationResult, documentHeader);

        var realConfiguration = configuration.Clone();
        realConfiguration.Labels = template == WrTemplateType.T1
            ? WrInspectionReportLabelConfiguration.GetT1Labels()
            : WrInspectionReportLabelConfiguration.GetLabels();

        var (stopExecution, alreadySaved, item) = await pdfDataExtractor.GetMatchesAsync(
            pdfFileName,
            dmsDataForFile,
            realConfiguration,
            previouslyParsedFiles,
            processRunId);

        return (stopExecution, alreadySaved, item, template);
    }
}
