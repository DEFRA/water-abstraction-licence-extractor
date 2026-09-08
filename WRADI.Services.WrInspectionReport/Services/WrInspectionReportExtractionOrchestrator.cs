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
    // The 13 LicenceProvisions grid fields WrInspectionReportTableMatcher can resolve via real
    // table cells (Azure AI Document Intelligence "prebuilt-layout") instead of the heuristic
    // column-walk. Every field this whole grid's known bugs (SpecialConditions,
    // OtherProvisions, etc. - see wr51_column_walk_bug memory) live in.
    private static readonly string[] GridFieldNames =
    [
        "SourceOfSupply", "PointOfAbstraction", "MeansOfAbstraction", "Purposes", "Period",
        "Quantities", "MeansOfMeasurement", "Records", "ProvisionOfInformation",
        "SpecialConditions", "Land", "ChargingFactors", "OtherProvisions"
    ];

    public static async Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item, WrTemplateType Template)> ExtractAsync(
        string pdfFileName,
        DmsFileData dmsDataForFile,
        LookupConfiguration configuration,
        List<string> previouslyParsedFiles,
        int processRunId,
        IPdfDataExtractorService pdfDataExtractor,
        // Opt-in overlay, off by default (both null) - see WrInspectionReportTableMatcher and
        // the "wr51_textract_tables_design" investigation this implements. Passing a non-null
        // tableExtractorService AND pdfBytesForTableExtraction attempts the table-based lookup
        // for the LicenceProvisions grid fields; any field it can't confidently resolve keeps
        // its existing heuristic result unchanged. Not yet wired into production - see this
        // feature's own build plan for why.
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

        var documentHeader = WrInspectionReportSchemaConverter.GetMultilineText(classificationResult, "DocumentHeader");
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

        // template is already known at this point (classified above) - gating on T1 here, not
        // just relying on WrInspectionReportTableMatcher's own content-based guards, avoids
        // spending a real Document Intelligence call/cost on every other template's documents,
        // which the design was never expected to help anyway (see the "Scope reality check" in
        // the wr51_textract_tables_design memory - non-grid templates have no tick/cross grid
        // for this mechanism to find at all). This is a pure efficiency gate, not a correctness
        // one: WrInspectionReportTableMatcher's own guards (majority-of-grid table selection,
        // LooksLikeATickAnswer's narrative-length check) already make running this safe on any
        // template - confirmed via a real golden-set harness run before this gate was added.
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
