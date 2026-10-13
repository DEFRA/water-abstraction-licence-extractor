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
/// Two-pass extraction: a cheap first pass over the 7 classification label groups decides
/// Metadata.Template, then a second runs GetT1Labels() or GetLabels() accordingly. Exists so a
/// T1-specific rule change can live in GetT1Labels() alone with no way to affect other templates -
/// needing one shared field to be correct for every template at once is what made two earlier
/// WalkSameLineColumns fixes unsafe.
///
/// The classification pass forces UseLockExclusivity off: it's a throwaway probe and mustn't take
/// a DMS lock or write a stub matches-result row. The real pass keeps the caller's own setting.
/// </summary>
public static class WrInspectionReportExtractionOrchestrator
{
    // The fields a multi-meter document repeats per meter, one cell each - see
    // TableMatcherHelper.MatchMultiValueTextFields.
    private static readonly string[] MeterFieldNames =
    [
        WrInspectionReportFieldNames.MeterName, WrInspectionReportFieldNames.MeterMake,
        WrInspectionReportFieldNames.SerialNumber, WrInspectionReportFieldNames.MeterAssetNumber,
        WrInspectionReportFieldNames.Reading, WrInspectionReportFieldNames.FlowRate,
        WrInspectionReportFieldNames.Units
    ];

    // A value stops where a sibling meter field's label follows it in the same merged cell, so
    // MeterMake in "Meter make: VuAqua Serial number 25 061010" doesn't swallow the serial too.
    // The field labels themselves, not each rule's full TextStart list: these are substring
    // searches within matched cell text, so the short recognisable form is what's needed.
    private static readonly string[] MeterFieldBoundaryLabels =
    [
        "Meter Name", "Meter make", "Meter Make", "Serial number", "Serial Number",
        "Meter Serial Number", "Meter Asset Number", "Asset no", "Asset number",
        "Reading", "Flow Rate", "Units"
    ];

    // Same guard as MeterFieldBoundaryLabels for the 13 LicenceProvisions grid fields - the short
    // form of each row label; the RuleXxx() definitions hold the exact TextStart.
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
            // Opt-in overlay, off when either is null. Both non-null attempts the table-based
            // lookup for the LicenceProvisions grid fields; anything it can't resolve confidently
            // keeps its heuristic result. Wired in by FileProcessSingleService.
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
        
        // Gating on template avoids spending a paid call on templates with no tick/cross grid.
        // T1 is what this was tuned against; T4/T6 followed once PdfClownGridExtractionPocTests
        // confirmed they draw a real border grid, and NonStandardNarrative (25% of the corpus)
        // 2026-09-25 since it's classified on GeneralComments wording, not grid structure.
        // Safe to broaden: ApplyTableBasedGridMatchesAsync only replaces keys it resolves
        // confidently, so a template with no matching content costs one free local attempt.
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
            // Meter fields first, scoped to their own subset: only MatchMultiValueTextFields
            // returns every meter's value in row order rather than keeping whichever cell matched
            // first. Deliberately not run over all labelLookups - its Contains-based search and
            // boundary-bounded extraction are untested outside this field set.
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