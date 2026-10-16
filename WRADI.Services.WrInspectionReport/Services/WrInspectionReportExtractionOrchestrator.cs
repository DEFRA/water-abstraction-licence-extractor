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
/// Two-pass extraction: a cheap first pass with only the classification label groups
/// (WrInspectionReportTextBasedLabelConfiguration.GetClassificationLabels - 7 groups) decides
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
            LookupConfiguration configuration1,
            LookupConfiguration configuration2,
            List<string> previouslyParsedFiles,
            int processRunId,
            IPdfDataExtractorService pdfDataExtractor,
            // Pass both of these together or neither. What they mean depends on the template:
            //
            //   T1  - optional. An overlay for the LicenceProvisions grid fields; any field it
            //         cannot confidently resolve keeps its existing heuristic result. Omitting
            //         them leaves a result that is merely less good. See
            //         WrInspectionReportTableMatcher.
            //   V3  - required. Its fields are two-column key/value grids, so this IS how the
            //         document is read; the V3 block below throws without it rather than
            //         returning a quietly truncated result.
            //
            // Any other template ignores them.
            ITableExtractorService? tableExtractorService = null,
            byte[]? pdfBytesForTableExtraction = null,
            // Two-tier: a free/local primary (Tabula) and a paid/cloud fallback (Azure DI) here.
            // The fallback is only tried, and only billed, when the primary resolved fewer than
            // minimumFieldsToSkipFallback of the 13 grid fields. Null keeps single-extractor
            // behaviour.
            ITableExtractorService? fallbackTableExtractorService = null,
            // The cost/accuracy dial, a step function not a smooth one. Swept 1/4/7/10/13 against
            // the golden set (2026-09-08): 1/4/7 all match Tabula-alone recall while fallback usage
            // climbs 6%->22% of T1 docs for nothing; 10 matches or beats Azure DI alone (154 vs 151
            // hits on the same 187-field T1 sample) at 28% usage; 13 gives 153 at 39%. 10 is the
            // measured sweet spot. Moving either way needs a fresh corpus-scale measurement.
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

        // V3's fields live in two-column key/value grids, and reading them as cells is the only
        // way its multi-line values (the licence holder name and address) come out whole - a line
        // rule truncates them at the first line, and a document with five meters collapses to one.
        //
        // REQUIRED for V3, unlike the opt-in T1 overlay above. Without it a v3 document still
        // produces a result, and that result looks healthy while being quietly wrong - which is
        // precisely the failure WRADI-392 existed to stamp out. Better to refuse the file than to
        // save a plausible-looking truncation. A free local extractor resolves the whole form, so
        // this costs nothing to satisfy and the paid fallback is deliberately never consulted.
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

            // Fetched once and shared, so a document costs a single GetTablesAsync call whether
            // one field or every field is resolved from it.
            var tables = await tableExtractorService.GetTablesAsync(
                BuildTableLookupDocument(pdfBytesForTableExtraction, dmsDataForFile.FileId),
                dmsDataForFile.FileId,
                processRunId);

            // Deliberately not wrapped in a try/catch, unlike the T1 overlay. There the table pass
            // is an enhancement, so losing it leaves a result that is merely less good; here it is
            // how the document is read, so swallowing a failure would ship the truncation this
            // whole mechanism exists to avoid.
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

        return (stopExecution, alreadySaved, scrapeResult, template);
    }

    // Internal, not private: lets the tests exercise the merge logic with a faked
    // ITableExtractorService, no real PDF or full two-pass pipeline needed.
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

        // Only reached, and for a paid fallback only billed, when the primary resolved fewer than
        // minimumFieldsToSkipFallback of the grid confidently. See that parameter's own comment for
        // the measured sweep behind the default of 10.
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

        // Free-text fields resolve from whichever table/service the grid logic above settled on,
        // deliberately after the escalation decision and never counted towards it:
        // minimumFieldsToSkipFallback was tuned on the 13 grid fields alone, so letting free-text
        // hits count would redefine "confident enough to skip the paid fallback" unmeasured.
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

        // Recorded here rather than where the overlay is requested, so it only ever claims a
        // service that actually contributed a match: the overlay is skipped for a template
        // without a grid, for an unreadable file, and whenever it finds nothing. Without this the
        // table extractor was absent from ServicesUsed on documents where it had resolved the
        // whole licence provisions grid, so the field could not answer whether it had run.
        if (!string.IsNullOrEmpty(usedServiceName) && !item.ServicesUsed.Contains(usedServiceName))
        {
            item.ServicesUsed.Add(usedServiceName);
        }
    }

    /// <summary>
    /// The minimal PdfDocument a table extractor needs: it only reads the bytes, so everything
    /// else is left unset rather than loading the document twice.
    /// </summary>
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

    // Failure is treated as "found nothing confident" rather than propagated, so a primary that
    // throws on a malformed PDF still leaves the fallback its chance. Tables/serviceName come back
    // with the matches so free-text fields resolve from the same fetch, with no second (for a paid
    // fallback, separately billed) GetTablesAsync call.
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