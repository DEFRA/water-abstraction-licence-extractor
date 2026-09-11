using Dapper;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WALE.Tools._2ndHalf;

// A WR51 processor mirroring FileProcessOrchestrationService + FileProcessSingleService
// (WRADI.Services.ProcessFile.AbstractionLicence) as closely as possible for a local run:
// - Creates a REAL process_run row (outputService.StartProcessRunAsync), same as the licence
//   orchestrator's "Batch process request" pattern.
// - For each file, adds a REAL process_run_file row, runs extraction, saves via the SAME
//   generic pdfDataExtractor.SaveMatchResultAsync path AbstractionLicence uses (writes to the
//   generic matches_result/match tables - no new schema needed, confirmed in
//   wr51_portal_bff_analysis), marks the file complete, then marks the run complete once all
//   files are done - the exact sequence FileProcessSingleService.RunAsync follows.
// - Deliberately skips everything AbstractionLicence-specific (LicenceSets, NALD linking,
//   licence number service) - none of it applies to inspection reports.
//
// Source of files: inspection_report_finder_result (the real SharePoint filename/folder
// discovery - see wr51_sharepoint_filter_rules memory), matched by PERMIT NUMBER against the
// local golden-set corpus, since no working AWS credentials exist for the real
// wradi-s3-ingress-dev bucket (confirmed via IAM AccessDenied) - these are real, S3-sourced
// documents (the golden set was originally downloaded from S3), just not necessarily the exact
// same file inspection_report_finder_result's row points to for a given permit.
//
// Not a real SQS/Lambda deployment - runs single-file processing inline in a loop rather than
// enqueuing, since there's no WR51 Lambda entry point yet. Everything it writes to Postgres
// (process_run, process_run_file, matches_result, match) is otherwise identical to what the
// real pipeline would write.
public static class RunInspectionReportProcessRun
{
    private const string GoldenSetFolder = "/Users/edwardbutler/Documents/TestLicences/";
    private const string ApiBaseUrl = "http://localhost:8080";

    // When true, skips inspection_report_finder_result/dms_extract matching entirely and
    // processes every wr51__*.pdf in the local golden-set corpus - so templates that happen not
    // to be represented among the current dms_extract snapshot's real file_ids (the "Exact"
    // matches only cover 36/789 - see wr51 process-run memory) still show up in a run, e.g. T1.
    // Nothing here proves these are the SAME documents the real SharePoint feed points at for
    // their permit - this mode is for exercising extraction against the full template variety,
    // not for validating against real discovery data. Use the Exact-match mode for that.
    private const bool UseFullCorpus = true;

    public static async Task<int> RunAsync()
    {
        var npgsqlDataSourceProvider = new NpgsqlDataSourceProvider(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);

        var databaseReadService = new PostgresReadService(npgsqlDataSourceProvider);
        var databaseWriteService = new PostgresWriteService(npgsqlDataSourceProvider);

        IOutputService outputService = new DatabaseOutputService(databaseReadService, databaseWriteService);
        ICacheService cacheService = new DatabaseCacheService(databaseReadService, databaseWriteService);

        Console.WriteLine("Building permit -> golden-set (filename, embedded guid) map...");

        // Each golden-set entry is (FileName, Guid) - the guid parsed out of the filename's own
        // wr51__<permit>__<guid>.pdf convention. This is compared against the real file_id(s)
        // recorded in inspection_report_finder_result for the same permit to tell an exact
        // document match (same guid - genuinely the same real document) apart from a
        // permit-only match (same licence, but inspection_report_finder_result's row points at
        // a different real document than the one available locally).
        var filesByPermit = new Dictionary<string, List<(string FileName, string Guid)>>();

        foreach (var path in Directory.GetFiles(GoldenSetFolder, "wr51__*.pdf"))
        {
            var fileName = Path.GetFileName(path);
            var parts = fileName.Split("__");

            if (parts.Length != 3)
            {
                continue;
            }

            var permit = parts[1].ToLowerInvariant();
            var guid = parts[2].Replace(".pdf", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

            // A handful of golden-set entries have a placeholder like "dummy" instead of a real
            // guid - skip rather than let Guid.Parse throw later.
            if (!Guid.TryParse(guid, out _))
            {
                continue;
            }

            if (!filesByPermit.TryGetValue(permit, out var list))
            {
                list = [];
                filesByPermit[permit] = list;
            }

            list.Add((fileName, guid));
        }

        List<(string Permit, string FileName, string Guid, string MatchType)> matched;

        if (UseFullCorpus)
        {
            Console.WriteLine("UseFullCorpus is set - skipping inspection_report_finder_result/dms_extract " +
                               "matching and processing every golden-set file instead.");

            matched = filesByPermit
                .SelectMany(kvp => kvp.Value.Select(f =>
                    (Permit: kvp.Key, FileName: f.FileName, Guid: f.Guid, MatchType: "GoldenSet")))
                .ToList();

            Console.WriteLine(
                $"{matched.Count} golden-set files across " +
                $"{matched.Select(m => m.Permit).Distinct().Count()} distinct permits.");
        }
        else
        {
            Console.WriteLine("Querying inspection_report_finder_result for permits and real file_ids...");

            await using var connection = await npgsqlDataSourceProvider.DataSource.OpenConnectionAsync();

            var realRows = (await connection.QueryAsync<(string PermitNumber, string FileId)>(
                "SELECT permit_number, file_id FROM inspection_report_finder_result")).ToList();

            var realFileIdsByPermit = realRows
                .GroupBy(r => r.PermitNumber.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.Select(r => r.FileId.ToLowerInvariant()).ToHashSet());

            // Restricted to Exact matches only (2026-09-09) - PermitOnly matches are mostly the
            // imprecise Compliance-folder fallback rule catching non-inspection-report compliance
            // paperwork (site manuals, correspondence, monitoring reports) for the right licence but
            // the wrong document; confirmed the golden-set file for those cases genuinely isn't the
            // same document inspection_report_finder_result found. Exact matches are the only ones
            // where the real file_id itself - not just the permit number - proves it's the same
            // physical document, so this is the only subset worth treating as a real validation run.
            matched = realFileIdsByPermit.Keys
                .Where(filesByPermit.ContainsKey)
                .SelectMany(p => filesByPermit[p].Select(f =>
                {
                    var isExactMatch = realFileIdsByPermit[p].Contains(f.Guid);
                    return (Permit: p, FileName: f.FileName, Guid: f.Guid, MatchType: isExactMatch ? "Exact" : "PermitOnly");
                }))
                .Where(m => m.MatchType == "Exact")
                .ToList();

            Console.WriteLine(
                $"Matched {matched.Count} golden-set files across " +
                $"{matched.Select(m => m.Permit).Distinct().Count()} distinct permits (exact document matches only).");
        }

        if (matched.Count == 0)
        {
            Console.WriteLine("Nothing to process.");
            return 1;
        }

        // ---- Orchestration phase (mirrors FileProcessOrchestrationService.RunAsync) ----
        await outputService.SetupAsync();

        var processRun = await outputService.StartProcessRunAsync(new ProcessRun
        {
            Description = UseFullCorpus
                ? "Local run: every wr51__*.pdf in the local golden-set corpus (real S3-sourced " +
                  "documents) - not matched against inspection_report_finder_result/dms_extract, " +
                  "so template coverage isn't limited to the current extract snapshot's real file_ids."
                : "Local run: inspection report files matched from inspection_report_finder_result " +
                  "against the local golden-set corpus (real S3-sourced documents, exact file_id " +
                  "matches only - no working AWS credentials for the real bucket).",
            StartDateTimeUtc = DateTime.UtcNow,
            NumberOfFiles = matched.Count,
            Status = "Batch",
            DocumentType = "WrInspectionReport"
        });

        Console.WriteLine($"Created ProcessRun {processRun.ProcessRunId}.");

        // ---- Single-file phase (mirrors FileProcessSingleService.RunAsync, run inline) ----
        var fileService = new ApiFileService(new HttpClient { BaseAddress = new Uri(ApiBaseUrl) });
        var documentService = new PdfPigNoOcrPdfDocumentService();
        var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
        var messageQueueService = new ApiMessageQueueService(new HttpClient());

        var outcomes = new List<(string Permit, string FileName, string MatchType, string Outcome, string Template, string Error)>();
        var idx = 0;

        foreach (var (permit, fileName, guid, matchType) in matched)
        {
            idx++;
            Console.WriteLine($"[{idx}/{matched.Count}] {fileName} ({matchType})...");

            // A fresh IPdfDataExtractorService per file - same lifecycle FileProcessSingleService
            // uses (it disposes its own in a finally block); this one is DI-scoped there, here
            // it's just constructed fresh so InUse/Dispose state can't leak between files.
            var pdfDataExtractor = new PdfDataExtractorService(
                new PdfPigNoOcrDataExtractorService(),
                new List<IOcrDataExtractorService>(),
                cacheService,
                outputService,
                documentService,
                docnetAlternativeDocumentService,
                messageQueueService);

            var processRunFile = await outputService.AddProcessRunFileAsync(new ProcessRunFile
            {
                ProcessRunId = processRun.ProcessRunId,
                FileName = fileName
            });

            try
            {
                // Use the golden-set file's OWN embedded guid, not a fresh one - it's the real
                // file_id for "Exact" matches, and for "PermitOnly" matches it's still the
                // stable, correct identifier for the actual document being processed (there is
                // no real file_id to borrow, since inspection_report_finder_result's row for
                // this permit points at a different document).
                var dmsFileData = new DmsFileData
                {
                    FileId = Guid.Parse(guid),
                    PermitNumber = permit,
                    DestinationFileName = fileName
                };

                var configuration = new LookupConfiguration(
                    WrInspectionReportLabelConfiguration.GetLabels(),
                    [],
                    fileService,
                    cacheService,
                    outputService,
                    new NullLicenceNumberService(),
                    new DmsLookupService(),
                    GeneralConstants.UnsetRegionCode,
                    DateTime.UtcNow,
                    lineHeight: 6,
                    minimumRowsForDigital: 30,
                    useAnchoredLineGrouping: true);

                var (stopExecution, alreadySaved, item, template) = await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                    fileName,
                    dmsFileData,
                    configuration,
                    [fileName],
                    processRun.ProcessRunId,
                    pdfDataExtractor);

                if (stopExecution)
                {
                    outcomes.Add((permit, fileName, matchType, "StopExecution", "", ""));
                    continue;
                }

                if (alreadySaved != true && item != null)
                {
                    await pdfDataExtractor.SaveMatchResultAsync(
                        item,
                        dmsFileData.FileId,
                        processRun.ProcessRunId,
                        configuration.UseLockExclusivity);
                }

                await outputService.MarkProcessRunFileCompleteAsync(processRunFile);

                outcomes.Add((permit, fileName, matchType, item != null ? "Success" : "NoResult", template.ToString(), ""));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: {ex.Message}");
                outcomes.Add((permit, fileName, matchType, "Exception", "", ex.Message.Replace(",", ";").Replace("\n", " ")));
            }
            finally
            {
                pdfDataExtractor.Dispose();
            }
        }

        processRun = await outputService.MarkProcessRunCompleteIfCompleteAsync(processRun);
        var completed = processRun is { EndDateTimeUtc: not null } && processRun.EndDateTimeUtc > DateTime.MinValue;
        Console.WriteLine($"\nProcessRun {processRun.ProcessRunId} completed: {completed}");

        Console.WriteLine("\n--- Outcome summary (by match type) ---");
        foreach (var group in outcomes.GroupBy(r => (r.MatchType, r.Outcome)))
        {
            Console.WriteLine($"{group.Key.MatchType} / {group.Key.Outcome}: {group.Count()}");
        }

        Console.WriteLine("\n--- Template summary (Success only) ---");
        foreach (var group in outcomes.Where(r => r.Outcome == "Success").GroupBy(r => r.Template))
        {
            Console.WriteLine($"{group.Key}: {group.Count()}");
        }

        var exceptions = outcomes.Where(r => r.Outcome == "Exception").ToList();
        if (exceptions.Count > 0)
        {
            Console.WriteLine("\n--- Sample exceptions ---");
            foreach (var ex in exceptions.Take(10))
            {
                Console.WriteLine($"{ex.FileName}: {ex.Error}");
            }
        }

        var outputPath = "Output/inspection_report_process_run.csv";
        Directory.CreateDirectory("Output");

        await using (var writer = new StreamWriter(outputPath))
        {
            await writer.WriteLineAsync("Permit,FileName,MatchType,Outcome,Template,Error");

            foreach (var r in outcomes)
            {
                await writer.WriteLineAsync($"{r.Permit},{r.FileName},{r.MatchType},{r.Outcome},{r.Template},{r.Error}");
            }
        }

        Console.WriteLine($"\nFull results written to: {outputPath}");
        Console.WriteLine($"ProcessRunId: {processRun.ProcessRunId}");

        return 0;
    }
}
