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
    // The fields a real multi-meter document repeats per meter (one cell each) - see
    // TableMatcherHelper.MatchMultiValueTextFields and its own doc comment for the WR51
    // example this scoping exists for.
    private static readonly string[] MeterFieldNames =
    [
        WrInspectionReportFieldNames.MeterName, WrInspectionReportFieldNames.MeterMake,
        WrInspectionReportFieldNames.SerialNumber, WrInspectionReportFieldNames.MeterAssetNumber,
        WrInspectionReportFieldNames.Reading, WrInspectionReportFieldNames.FlowRate,
        WrInspectionReportFieldNames.Units
    ];

    // A value stops here when a sibling meter field's own label follows it within the same
    // merged cell (e.g. "Meter make: VuAqua Serial number 25 061010" - the value for
    // MeterMake must not swallow "Serial number 25 061010" too). Deliberately the field labels
    // themselves, not each rule's full TextStart/AlsoStartsWith alternate list - these are
    // substring searches within already-matched cell text, not label-matching TextStart
    // comparisons, so the short, recognisable form of each name is what actually needs to be
    // found here.
    private static readonly string[] MeterFieldBoundaryLabels =
    [
        "Meter Name", "Meter make", "Meter Make", "Serial number", "Serial Number",
        "Meter Serial Number", "Meter Asset Number", "Asset no", "Asset number",
        "Reading", "Flow Rate", "Units"
    ];

    // Same guard as MeterFieldBoundaryLabels, for the 13 LicenceProvisions grid fields - the
    // recognisable short form of each field's own row label (see the RuleXxx() definitions in
    // WrInspectionReportTextBasedLabelConfiguration for the exact TextStart each one matches).
    private static readonly string[] GridFieldBoundaryLabels =
    [
        "Source of supply", "Point of abstraction", "Means of abstraction", "Purpose",
        "Period", "Quantities", "Means of measurement", "Records",
        "Provision of information", "Special conditions", "Land", "Charging factors",
        "Other provisions"
    ];

    public static async Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item, WrTemplateType Template)>
        ExtractAsync(
            string pdfFileName,
            DmsFileData dmsDataForFile,
            LookupConfiguration configuration1,
            LookupConfiguration configuration2,
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
            // The cost/accuracy dial. Swept 1/4/7/10/13 against the golden set (2026-09-08) -
            // it's a step function, not smooth: 1/4/7 are flat at the same recall as Tabula
            // alone (fallback usage climbs from
            // 6%->22% of T1 docs for no accuracy gain), then 10 jumps to matching-or-beating Azure
            // DI's own accuracy (154 Hit vs Azure-DI-alone's 151, on the same 187-field T1 grid
            // sample) at only 28% fallback usage; 13 gives slightly less (153) at 39% usage. 10 is the
            // measured sweet spot and the default here - raise towards GridFieldNames.Length for more
            // accuracy at more cost, lower towards 1 to spend as little as possible, but neither
            // direction is evidenced to help past this curve without a fresh corpus-scale measurement.
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
        configuration1.Labels = template == WrTemplateType.T1
            ? WrInspectionT1LabelConfiguration.FilterFrom(originalLabels)
            : originalLabels;

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
        
        // Gating on template here (rather than relying solely on WrInspectionReportTableMatcher's
        // own content-based guards) avoids spending a real Document Intelligence call/cost on
        // templates with no tick/cross grid for this mechanism to find. T1 is the template this
        // was built and tuned against; T4/T6 added after PdfClownGridExtractionPocTests confirmed
        // both draw a genuine border grid too (see WALE.ProcessFile.Services.PdfClown) - broadened
        // here since ApplyTableBasedGridMatchesAsync only ever adds/replaces the specific
        // LabelGroupName keys it resolves confidently, never touches anything it doesn't, so a
        // template with no matching content just falls through with the heuristic result
        // untouched, at the cost of one extra (free, local) extraction attempt.
        // NonStandardNarrative (25% of the real corpus) added 2026-09-25: WrTemplateType's own
        // doc comment confirms it's classified separately purely on GeneralComments' heading
        // wording, not grid structure - "otherwise T1-shaped (has the standard header and
        // grid)" - so the same safe, additive mechanism applies unchanged.
        if (template is WrTemplateType.T1 or WrTemplateType.T4 or WrTemplateType.T6
                or WrTemplateType.NonStandardNarrative
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
            await GetTableMatchesAsync(
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
            (allMatches, tables, usedServiceName) = await GetTableMatchesAsync(
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
            // Meter-detail fields first, scoped to their own subset of labelLookups: a document
            // with several meters has one cell per meter, and MatchMultiValueTextFields is the
            // only one of the two that returns every meter's value (in row order) rather than
            // silently picking whichever cell happened to match first and discarding the rest -
            // see TableExtractorHelper's own doc comment for the real WR51 example this fixes.
            // Deliberately NOT run over the full labelLookups: its Contains-based label search
            // and boundary-bounded value extraction are untested outside this field set, so
            // scoping it here keeps every other FreeText field on the already-validated
            // MatchTextFields path unchanged.
            var meterFieldLookups = labelLookups
                .Where(l => MeterFieldNames.Contains(l.LabelGroupName))
                .ToList();

            var multiValueMatches = TableMatcherHelper.MatchMultiValueTextFields(
                tables,
                meterFieldLookups,
                usedServiceName,
                MeterFieldBoundaryLabels);

            foreach (var (key, value) in multiValueMatches)
            {
                allMatches.TryAdd(key, value);
            }

            var matches = TableMatcherHelper.MatchTextFields(
                tables,
                labelLookups,
                usedServiceName,
                MeterFieldBoundaryLabels);

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
                new PdfDocument(
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
                        DateTime.UtcNow)),
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
                TickHelper.GetTickedOrAcceptedStatus,
                GridFieldBoundaryLabels);

            return (matches, tables, tableExtractorService.Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR - {nameof(GetTableMatchesAsync)} - {ex.Message}");
            return ([], null, null);
        }
    }
}