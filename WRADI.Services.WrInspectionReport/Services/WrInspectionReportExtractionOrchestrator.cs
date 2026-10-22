using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.Helpers;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Helpers;

namespace WRADI.DocumentType.WrInspectionReport.Services;

/// <summary>
/// Two passes so a T1-only rule change lands in GetT1Labels() alone and cannot reach another
/// template's documents - the constraint that made two earlier WalkSameLineColumns fixes unsafe.
///
/// The classification pass forces UseLockExclusivity off: it is a throwaway probe and must not
/// take a DMS lock or write a stub matches-result row. The second pass keeps the caller's.
/// </summary>
public static class WrInspectionReportExtractionOrchestrator
{
    public static async Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item, WrTemplateType Template)>
        ExtractAsync(
            string pdfFileName,
            DmsFileData dmsDataForFile,
            LookupConfiguration configuration1,
            LookupConfiguration configuration2,
            List<string> previouslyParsedFiles,
            int processRunId,
            IPdfDataExtractorService pdfDataExtractor,
            // Both together or neither. Optional for T1, where they overlay the LicenceProvisions
            // grid and a field they cannot resolve keeps its heuristic result; required for V3,
            // where they are how the document is read. Every other template ignores them.
            ITableExtractorService? tableExtractorService = null,
            byte[]? pdfBytesForTableExtraction = null,
            // A paid fallback (Azure DI) behind the free primary (Tabula), tried and billed only
            // below minimumFieldsToSkipFallback. Null keeps single-extractor behaviour.
            ITableExtractorService? fallbackTableExtractorService = null,
            // Swept 1/4/7/10/13 against the golden set (2026-09-08): 1/4/7 buy no recall over
            // Tabula alone while fallback usage climbs to 22% of T1 documents; 10 matches or beats
            // Azure DI alone (154 vs 151 of a 187-field sample) at 28%; 13 gives 153 at 39%.
            int minimumFieldsToSkipFallback = 10)
    {
        configuration1 = configuration1.Clone();
        WrInspectionReportTextBasedLabelConfiguration.ConfigurationPropertiesToSet(configuration1);

        var originalLabels = configuration1.Labels.ToList();
        
        var classificationConfiguration = configuration1.Clone();
        classificationConfiguration.Labels =
            WrInspectionClassificationLabelConfiguration.FilterFrom(originalLabels);
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

        var documentHeader = WrInspectionReportSchemaConverter.GetMultilineText(
            classificationResult,
            WrInspectionReportFieldNames.DocumentHeader);
        
        var template = WrInspectionReportSchemaConverter.ClassifyTemplate(
            classificationResult,
            documentHeader);

        configuration1 = configuration1.Clone();

        configuration1.Labels = template switch
        {
            WrTemplateType.V3 => WrInspectionV3LabelConfiguration.GetLabels(),
            WrTemplateType.T1 => WrInspectionT1LabelConfiguration.FilterFrom(originalLabels),
            _ => originalLabels
        };

        var (stopExecution, alreadySaved, scrapeResult) = await pdfDataExtractor.GetMatchesAsync(
            pdfFileName,
            dmsDataForFile,
            configuration1,
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
                    configuration2.Labels,
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

        // Refuses the file rather than saving a plausible-looking truncation, unlike the opt-in
        // T1 overlay above. A free local extractor satisfies this, so it costs nothing.
        if (template == WrTemplateType.V3)
        {
            if (tableExtractorService == null || pdfBytesForTableExtraction == null)
            {
                throw new InvalidOperationException(
                    $"A {nameof(ITableExtractorService)} and the document bytes are required to " +
                    $"extract a {nameof(WrTemplateType.V3)} document ({pdfFileName}). Its fields " +
                    "are two-column key/value grids, and without them the multi-line values are " +
                    "truncated and multiple meters collapse into one.");
            }

            // Fetched once and shared: one GetTablesAsync call per document however much is read.
            var tables = await tableExtractorService.GetTablesAsync(
                BuildTableLookupDocument(pdfBytesForTableExtraction, dmsDataForFile.FileId),
                dmsDataForFile.FileId,
                processRunId);

            // Not try/caught, unlike the T1 overlay: swallowing a failure here ships a truncation.
            WrInspectionV3KeyValueMatcher.Apply(
                scrapeResult, tables, configuration1.Labels, tableExtractorService.Name);

            WrInspectionV3KeyValueMatcher.ApplyMeters(
                scrapeResult, tables, tableExtractorService.Name);

            WrInspectionV3KeyValueMatcher.ApplyActions(
                scrapeResult, tables, tableExtractorService.Name);
        }

        scrapeResult.AdditionalInformation ??= [];
        scrapeResult.AdditionalInformation[WrInspectionReportSchemaConverter.AdditionalInformationTemplateKey]
            = template.ToString();

        // Also applied in ToForm, which is too late for anything reading the saved matches back by
        // their shared names. The mapper is additive and idempotent, so both calls are safe.
        if (template == WrTemplateType.V3)
        {
            WrInspectionV3SharedFieldMapper.Apply(scrapeResult);
        }

        return (stopExecution, alreadySaved, scrapeResult, template);
    }

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
            await GetTableMatchesAsync(
                tableExtractorService,
                labelLookups,
                pdfBytes,
                fileId,
                processRunId);

        if (allMatches.Count < minimumFieldsToSkipFallback
            && fallbackTableExtractorService != null)
        {
            (allMatches, tables, usedServiceName) = await GetTableMatchesAsync(
                fallbackTableExtractorService,
                labelLookups,
                pdfBytes,
                fileId,
                processRunId);
        }

        // After the escalation decision and never counted towards it: minimumFieldsToSkipFallback
        // was tuned on the 13 grid fields alone.
        if (tables != null && !string.IsNullOrEmpty(usedServiceName))
        {
            var matches = TableMatcherHelper.MatchTextFields(
                tables,
                labelLookups,
                usedServiceName);
            
            foreach (var (key, value) in matches)
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

        // Recorded here, not where the overlay is requested, so ServicesUsed only ever names a
        // service that contributed a match.
        if (!string.IsNullOrEmpty(usedServiceName) && !item.ServicesUsed.Contains(usedServiceName))
        {
            item.ServicesUsed.Add(usedServiceName);
        }
    }

    /// A table extractor reads only the bytes, so the rest is left unset.
    private static PdfDocument BuildTableLookupDocument(byte[] pdfBytes, Guid fileId) =>
        new(
            null!,
            fileId,
            false,
            pdfBytes,
            pdfBytes.Length,
            null!,
            null!,
            null!,
            new LookupConfiguration(
                [],
                [],
                null!,
                null!, null!,
                null!,
                null!,
                null!,
                null!,
                -1,
                DateTime.UtcNow));

    // Failure reads as "found nothing", so a primary throwing on a malformed PDF still leaves the
    // fallback its chance. Tables come back with the matches to save a second, billable, fetch.
    private static async Task<(
        Dictionary<string, LabelGroupResult> Matches,
        IReadOnlyList<DocumentTable>? Tables,
        string? ServiceName)>
            GetTableMatchesAsync(
                ITableExtractorService tableExtractorService,
                List<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
                byte[] pdfBytes,
                Guid fileId,
                int processRunId)
    {
        try
        {
            var tables = await tableExtractorService.GetTablesAsync(
                BuildTableLookupDocument(pdfBytes, fileId),
                fileId,
                processRunId);

            var labels = labelLookups
                .Where(labelGroup => labelGroup.Labels
                    .Any(l => l.LayoutExtractorTableLookupType is LayoutExtractorTableLookupType.Default
                        or LayoutExtractorTableLookupType.Grid))
                .ToList();
            
            var matches = TableMatcherHelper.MatchToPossibilities(
                tables,
                labels,
                tableExtractorService.Name,
                TickHelper.GetTickedOrAcceptedStatus);

            return (matches, tables, tableExtractorService.Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR - {nameof(GetTableMatchesAsync)} - {ex.Message}");
            return ([], null, null);
        }
    }
}