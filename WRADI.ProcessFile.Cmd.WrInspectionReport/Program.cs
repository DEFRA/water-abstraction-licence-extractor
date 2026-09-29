using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;
using WRADI.Services.ProcessFile.WrInspectionReport;

// A one-shot local batch runner for WR51 - the WrInspectionReport equivalent of
// WRADI.ProcessFile.Cmd.AbstractionLicence. Bypasses SQS entirely: builds a candidate list, creates
// one real ProcessRun, then calls IFileProcessSingleService.RunAsync in-process for each candidate -
// the same class the WrInspectionReport Lambda/Local projects use, just invoked directly instead of
// via a queue message. Replaces the old hand-rolled Tools/WALE.Tools/2ndHalf/RunInspectionReportProcessRun.cs
// tool, which duplicated this logic against a hardcoded local truth-set folder rather than a real
// discovery source.
//
// Two candidate sources are supported:
//   (no args)  - InspectionReportFinderResult, the real SharePoint discovery (today's default).
//                Finder discovery always outnumbers what's actually been uploaded to S3, since a
//                person manually uploads from SharePoint with no automation linking the two.
//   "s3"       - every wr51__-prefixed key already sitting in the shared ingress bucket, so this
//                only ever processes files confirmed to actually exist, at the cost of skipping
//                anything the finder knows about but nobody's uploaded yet.

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

await ProgramAsync(configuration, args);
return;

async Task ProgramAsync(IConfiguration configurationItem, string[] programArgs)
{
    ConsoleHelper.WriteLine($"INFO - WRADI.ProcessFile.Cmd.WrInspectionReport - Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    var startDateTimeUtc = DateTime.UtcNow;

    var useS3Source = programArgs.Any(a => string.Equals(a, "s3", StringComparison.OrdinalIgnoreCase));

    var serviceProvider = new ServiceCollection()
        .AddWrInspectionReportFileProcessServices(configurationItem)
        .BuildServiceProvider();

    var cacheService = serviceProvider.GetRequiredService<ICacheService>();
    var outputService = serviceProvider.GetRequiredService<IOutputService>();
    var fileProcessSingleService = serviceProvider.GetRequiredService<IFileProcessSingleService>();

    await cacheService.SetupAsync();
    await outputService.SetupAsync();

    var candidates = useS3Source
        ? await GetCandidatesFromS3Async(serviceProvider)
        : await GetCandidatesFromFinderAsync(serviceProvider);

    var description = useS3Source
        ? "Local batch run from S3 wr51_ prefix"
        : "Local batch run from inspection_report_finder_result";

    if (candidates.Count == 0)
    {
        ConsoleHelper.WriteLine("INFO - WRADI.ProcessFile.Cmd.WrInspectionReport - No inspection report files to process");
        return;
    }

    var processRun = await outputService.StartProcessRunAsync(
        new ProcessRun
        {
            Description = description,
            StartDateTimeUtc = startDateTimeUtc,
            NumberOfFiles = candidates.Count,
            Status = "Batch",
            DocumentType = "WrInspectionReport"
        });

    ConsoleHelper.WriteLine(
        $"INFO - WRADI.ProcessFile.Cmd.WrInspectionReport - Created ProcessRun {processRun.ProcessRunId} for {candidates.Count} files");

    var fileNumber = 0;

    foreach (var candidate in candidates)
    {
        fileNumber++;

        ConsoleHelper.WriteLine(
            $"INFO - WRADI.ProcessFile.Cmd.WrInspectionReport - [{fileNumber}/{candidates.Count}] {candidate.FilePath}");

        try
        {
            await fileProcessSingleService.RunAsync(
                new FileProcessSingleRequest
                {
                    FilePath = candidate.FilePath,
                    DestinationFileName = candidate.FilePath,
                    DmsPath = candidate.DmsPath,
                    FileId = candidate.FileId,
                    PermitNumber = candidate.PermitNumber,
                    RegionId = GeneralConstants.UnsetRegionCode,
                    ProcessRunId = processRun.ProcessRunId,
                    RequestedAt = DateTime.UtcNow
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine(
                $"ERROR - WRADI.ProcessFile.Cmd.WrInspectionReport - {candidate.FilePath} threw: {ex}");
        }
    }

    ConsoleHelper.WriteLine(
        $"INFO - WRADI.ProcessFile.Cmd.WrInspectionReport - Finished at {DateTime.Now:yyyy-MM-dd HH:mm:ss}. ProcessRunId: {processRun.ProcessRunId}");
}

async Task<List<(string FilePath, string PermitNumber, Guid FileId, string? DmsPath)>> GetCandidatesFromFinderAsync(
    IServiceProvider serviceProvider)
{
    var inspectionReportFinderCacheService = serviceProvider.GetRequiredService<IInspectionReportFinderCacheService>();

    return (await inspectionReportFinderCacheService.GetInspectionReportFinderResultsAsync(0, int.MaxValue))
        .Where(candidate =>
            !string.IsNullOrEmpty(candidate.PermitNumber)
            && !string.IsNullOrEmpty(candidate.FileId)
            && Guid.TryParse(candidate.FileId, out _))
        .Select(candidate =>
        {
            var destinationFileName = $"{candidate.PermitNumber!.ToLower()}__{candidate.FileId!.ToLower()}.pdf";

            return (
                FilePath: destinationFileName,
                PermitNumber: candidate.PermitNumber!,
                FileId: Guid.Parse(candidate.FileId!),
                DmsPath: (string?)candidate.FileUrl);
        })
        .ToList();
}

async Task<List<(string FilePath, string PermitNumber, Guid FileId, string? DmsPath)>> GetCandidatesFromS3Async(
    IServiceProvider serviceProvider)
{
    const string wr51Prefix = "wr51__";

    var fileService = serviceProvider.GetRequiredService<IFileService>();
    var allFiles = await fileService.GetAllFilesAsync();

    return allFiles
        .Where(f => f.StartsWith(wr51Prefix, StringComparison.OrdinalIgnoreCase))
        .Select(f =>
        {
            var remainder = f[wr51Prefix.Length..];
            return (
                FilePath: f,
                PermitNumber: FileHelper.ExtractPermitNumber(remainder),
                FileId: FileHelper.ExtractFileId(remainder));
        })
        .Where(c => !string.IsNullOrEmpty(c.PermitNumber) && c.FileId.HasValue)
        .Select(c => (
            FilePath: c.FilePath,
            PermitNumber: c.PermitNumber!,
            FileId: c.FileId!.Value,
            DmsPath: (string?)null))
        .ToList();
}
