using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Constants;
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
    public static async Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item, WrTemplateType Template)>
        ExtractAsync(
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
            byte[]? pdfBytesForTableExtraction = null,
            // Cost-optimised two-tier design: pass a free/local extractor (e.g. TabulaTableExtractorService)
            // as tableExtractorService and a paid/cloud one (e.g. AzureAiServicesDocumentIntelligenceTableExtractorService)
            // here - the fallback is only ever tried, and only ever billed, when the primary resolved
            // fewer than minimumFieldsToSkipFallback of the 13 grid fields confidently (see
            // TryGetTableMatchesAsync). Passing null here (the default) keeps today's single-extractor
            // behaviour unchanged.
            ITableExtractorService? fallbackTableExtractorService = null,
            // The cost/accuracy dial. Swept 1/4/7/10/13 against the golden set (2026-09-08, see
            // wr51_textract_tables_design memory for the full curve) - it's a step function, not
            // smooth: 1/4/7 are flat at the same recall as Tabula alone (fallback usage climbs from
            // 6%->22% of T1 docs for no accuracy gain), then 10 jumps to matching-or-beating Azure
            // DI's own accuracy (154 Hit vs Azure-DI-alone's 151, on the same 187-field T1 grid
            // sample) at only 28% fallback usage; 13 gives slightly less (153) at 39% usage. 10 is the
            // measured sweet spot and the default here - raise towards GridFieldNames.Length for more
            // accuracy at more cost, lower towards 1 to spend as little as possible, but neither
            // direction is evidenced to help past this curve without a fresh corpus-scale measurement.
            int minimumFieldsToSkipFallback = 10)
    {
        configuration = configuration.Clone();
        configuration.LineHeight = 6;
        configuration.MinimumRowsForDigital = 30;
        configuration.UseAnchoredLineGrouping = true;
        
        var classificationConfiguration = configuration.Clone();
        classificationConfiguration.Labels = WrInspectionClassificationLabelConfiguration.GetLabels();
        //classificationConfiguration.UseLockExclusivity = false;

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

        var documentHeader = WrInspectionReportSchemaConverter.GetMultilineText(
            classificationResult,
            WrInspectionReportFieldNames.DocumentHeader);
        
        var template = WrInspectionReportSchemaConverter.ClassifyTemplate(
            classificationResult,
            documentHeader);

        configuration = configuration.Clone();
        configuration.Labels = template == WrTemplateType.T1
            ? WrInspectionT1LabelConfiguration.GetLabels()
            : WrInspectionReportLabelConfiguration.GetLabels();;

        var (stopExecution, alreadySaved, scrapeResult) = await pdfDataExtractor.GetMatchesAsync(
            pdfFileName,
            dmsDataForFile,
            configuration,
            previouslyParsedFiles,
            processRunId);

        if (scrapeResult == null || stopExecution)
        {
            return (stopExecution, alreadySaved, scrapeResult, template);
        }
        
        // Gating on T1 here (rather than relying solely on WrInspectionReportTableMatcher's own
        // content-based guards) avoids spending a real Document Intelligence call/cost on
        // templates with no tick/cross grid for this mechanism to find
        if (template == WrTemplateType.T1
            && tableExtractorService != null
            && pdfBytesForTableExtraction != null)
        {
            try
            {
                await ApplyTableBasedGridMatchesAsync(
                    scrapeResult,
                    configuration.Labels,
                    tableExtractorService,
                    pdfBytesForTableExtraction,
                    dmsDataForFile.FileId,
                    processRunId,
                    fallbackTableExtractorService,
                    minimumFieldsToSkipFallback);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR - {nameof(ApplyTableBasedGridMatchesAsync)} - {ex.Message}");
                
                // Never discard an already-good result, so carry on
            }
        }

        scrapeResult.AdditionalInformation ??= [];
        scrapeResult.AdditionalInformation[WrInspectionReportSchemaConverter.AdditionalInformationTemplateKey]
            = template.ToString();

        return (stopExecution, alreadySaved, scrapeResult, template);
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
        int processRunId,
        ITableExtractorService? fallbackTableExtractorService = null,
        int minimumFieldsToSkipFallback = 10)
    {
        var (allMatches, tables, usedServiceName) =
            await TryGetTableMatchesAsync(
                tableExtractorService,
                labelLookups,
                pdfBytes,
                fileId,
                processRunId);

        // Only reached - and only billed, for a paid fallback - when the primary extractor
        // (expected to be the free/local one) resolved fewer than minimumFieldsToSkipFallback of
        // the grid confidently. At the default (1), a primary that resolved even one field never
        // triggers this, however much of the rest of the grid it missed - that's the cheapest,
        // most conservative setting. A caller wanting more of a paid fallback's accuracy back, at
        // the cost of more paid calls, raises this towards GridFieldNames.Length.
        if (allMatches.Count < minimumFieldsToSkipFallback
            && fallbackTableExtractorService != null)
        {
            (allMatches, tables, usedServiceName) = await TryGetTableMatchesAsync(
                fallbackTableExtractorService,
                labelLookups,
                pdfBytes,
                fileId,
                processRunId);
        }

        // Free-text fields (Time/SerialNumber/TelephoneNumber) are resolved from whichever
        // table/service the grid-field logic above ended up using - deliberately AFTER the
        // fallback-escalation decision, and never folded into tableMatches.Count before that
        // decision is made. minimumFieldsToSkipFallback was tuned against the golden set purely
        // against the 13 tick/cross grid fields; letting free-text hits count towards it would
        // silently change what "confident enough, skip the paid fallback" means without
        // re-measuring it.
        if (tables != null && !string.IsNullOrEmpty(usedServiceName))
        {
            var freeTextMatches = 
                WrInspectionReportTableMatcher.MatchFreeTextFields(
                    tables,
                    labelLookups,
                    GridFieldNames,
                    FreeTextFieldNames,
                    usedServiceName);

            foreach (var (key, value) in freeTextMatches)
            {
                allMatches.TryAdd(key, value);
            }
        }

        if (allMatches.Count == 0)
        {
            return;
        }

        item.Matches = item.Matches!
            .Where(m => m.LabelGroupName == null || !allMatches.ContainsKey(m.LabelGroupName))
            .Concat(allMatches.Values)
            .ToList();
    }

    // Failure here is treated identically to "found nothing confident" (empty dictionary, null
    // tables), not propagated - so a primary extractor that throws (a local parser tripping on a
    // malformed PDF, say) still gives a fallback extractor its own chance, rather than the whole
    // overlay being abandoned on the primary's failure alone. Tables/serviceName are returned
    // alongside the grid matches so the caller can resolve free-text fields from the SAME fetched
    // tables afterward, without a second (and for a paid fallback, separately billed)
    // GetTablesAsync call.
    private static async Task<(
        Dictionary<string, LabelGroupResult> Matches,
        IReadOnlyList<DocumentTable>? Tables,
        string? ServiceName)>
            TryGetTableMatchesAsync(
                ITableExtractorService tableExtractorService,
                List<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
                byte[] pdfBytes,
                Guid fileId,
                int processRunId)
    {
        try
        {
            var tables = await tableExtractorService.GetTablesAsync(
                pdfBytes,
                fileId,
                processRunId);

            var possibilityBasedMatches =
                WrInspectionReportTableMatcher.MatchPossibility(
                    tables,
                    labelLookups,
                    GridFieldNames,
                    tableExtractorService.Name);

            return (possibilityBasedMatches, tables, tableExtractorService.Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR - {nameof(TryGetTableMatchesAsync)} - {ex.Message}");
            return ([], null, null);
        }
    }
    
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

    // Free-text (not tick/cross) fields also confirmed sitting in the same table as the grid on
    // real T1 documents (2026-09-08 spot check) - the same 3 fields identified as affected by the
    // column-walk engine's own known leak bugs (see wr51_column_walk_bug memory). Resolved via
    // WrInspectionReportTableMatcher.MatchFreeTextFields, which never counts towards
    // minimumFieldsToSkipFallback below - see ApplyTableBasedGridMatchesAsync for why.
    private static readonly string[] FreeTextFieldNames =
    [
        WrInspectionReportFieldNames.Time, WrInspectionReportFieldNames.SerialNumber,
        WrInspectionReportFieldNames.TelephoneNumber
    ];
}