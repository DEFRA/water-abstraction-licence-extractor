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
/// result. Exists so a T1-specific rule change (once it has real evidence behind it) can be made
/// in GetT1Labels() alone, with no way to affect any other template's documents, rather than
/// needing a shared field's behaviour to be correct for every template simultaneously - that's
/// exactly the constraint that made two earlier WalkSameLineColumns fix attempts unsafe.
///
/// The classification pass always runs with UseLockExclusivity forced off - it's a throwaway
/// probe, not the result callers actually want, and mustn't take a real DMS lock or write a
/// stub matches-result row for it. The second, real pass keeps whatever locking behaviour the
/// caller's own configuration asked for, exactly matching what a single-pass call would have
/// done.
/// </summary>
public static class WrInspectionReportExtractionOrchestrator
{
    // The 13 LicenceProvisions grid fields WrInspectionReportTableMatcher can resolve via real
    // table cells (Azure AI Document Intelligence "prebuilt-layout") instead of the heuristic
    // column-walk.
    private static readonly string[] GridFieldNames =
    [
        WrInspectionReportFieldNames.SourceOfSupply, WrInspectionReportFieldNames.PointOfAbstraction,
        WrInspectionReportFieldNames.MeansOfAbstraction, WrInspectionReportFieldNames.Purposes,
        WrInspectionReportFieldNames.Period, WrInspectionReportFieldNames.Quantities,
        WrInspectionReportFieldNames.MeansOfMeasurement, WrInspectionReportFieldNames.Records,
        WrInspectionReportFieldNames.ProvisionOfInformation, WrInspectionReportFieldNames.SpecialConditions,
        WrInspectionReportFieldNames.Land, WrInspectionReportFieldNames.ChargingFactors,
        WrInspectionReportFieldNames.OtherProvisions
    ];

    public static async Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item, WrTemplateType Template)> ExtractAsync(
        string pdfFileName,
        DmsFileData dmsDataForFile,
        LookupConfiguration configuration,
        List<string> previouslyParsedFiles,
        int processRunId,
        IPdfDataExtractorService pdfDataExtractor,
        // Opt-in overlay, off by default (both null) - see WrInspectionReportTableMatcher.
        // Passing a non-null tableExtractorService AND pdfBytesForTableExtraction attempts the
        // table-based lookup for the LicenceProvisions grid fields; any field it can't
        // confidently resolve keeps its existing heuristic result unchanged. Not yet wired into
        // any production caller.
        ITableExtractorService? tableExtractorService = null,
        byte[]? pdfBytesForTableExtraction = null)
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

        var documentHeader = WrInspectionReportSchemaConverter.GetMultilineText(classificationResult, WrInspectionReportFieldNames.DocumentHeader);
        var template = WrInspectionReportSchemaConverter.ClassifyTemplate(classificationResult, documentHeader);

        var realLabels = template == WrTemplateType.T1
            ? WrInspectionReportLabelConfiguration.GetT1Labels()
            : WrInspectionReportLabelConfiguration.GetLabels();

        var realConfiguration = configuration.Clone();
        realConfiguration.Labels = realLabels;

        var (stopExecution, alreadySaved, item) = await pdfDataExtractor.GetMatchesAsync(
            pdfFileName,
            dmsDataForFile,
            realConfiguration,
            previouslyParsedFiles,
            processRunId);

        // Gating on T1 here (rather than relying solely on WrInspectionReportTableMatcher's own
        // content-based guards) avoids spending a real Document Intelligence call/cost on
        // templates with no tick/cross grid for this mechanism to find at all. Pure efficiency
        // gate, not a correctness one - the matcher's own guards (majority-of-grid table
        // selection, LooksLikeATickAnswer's narrative-length check) already make running this
        // safe on any template.
        if (!stopExecution
            && item?.Matches != null
            && template == WrTemplateType.T1
            && tableExtractorService != null
            && pdfBytesForTableExtraction != null)
        {
            await ApplyTableBasedGridMatchesAsync(
                item,
                realLabels,
                tableExtractorService,
                pdfBytesForTableExtraction,
                dmsDataForFile.FileId,
                processRunId);
        }

        return (stopExecution, alreadySaved, item, template);
    }

    // Internal (not private): lets WRADI.Services.WrInspectionReport.Tests exercise the merge
    // logic directly with a faked ITableExtractorService, without needing a real PDF and the
    // full two-pass GetMatchesAsync pipeline - same pattern this project already uses for
    // other internal helpers (see the csproj's InternalsVisibleTo).
    internal static async Task ApplyTableBasedGridMatchesAsync(
        MatchesResult item,
        List<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        ITableExtractorService tableExtractorService,
        byte[] pdfBytes,
        Guid fileId,
        int processRunId)
    {
        var tables = await tableExtractorService.GetTablesAsync(pdfBytes, fileId, processRunId);

        var tableMatches = WrInspectionReportTableMatcher.MatchGridFields(
            tables,
            labelLookups,
            GridFieldNames,
            tableExtractorService.Name);

        if (tableMatches.Count == 0)
        {
            return;
        }

        item.Matches = item.Matches!
            .Where(m => m.LabelGroupName == null || !tableMatches.ContainsKey(m.LabelGroupName))
            .Concat(tableMatches.Values)
            .ToList();
    }
}
