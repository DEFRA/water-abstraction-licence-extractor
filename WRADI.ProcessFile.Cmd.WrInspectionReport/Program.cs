using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;
using WRADI.Services.ProcessFile.WrInspectionReport;

// A one-shot local batch runner for WR51 - the WrInspectionReport equivalent of
// WRADI.ProcessFile.Cmd.AbstractionLicence. Bypasses SQS entirely: builds the candidate list from
// InspectionReportFinderResult (the real SharePoint discovery), creates one real ProcessRun, then
// calls IFileProcessSingleService.RunAsync in-process for each candidate - the same class the
// WrInspectionReport Lambda/Local projects use, just invoked directly instead of via a queue
// message. Replaces the old hand-rolled Tools/WALE.Tools/2ndHalf/RunInspectionReportProcessRun.cs
// tool, which duplicated this logic against a hardcoded local truth-set folder rather than the
// real discovery service.

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

await ProgramAsync(configuration);
return;

async Task ProgramAsync(IConfiguration configurationItem)
{
    ConsoleHelper.WriteLine($"INFO - WRADI.ProcessFile.Cmd.WrInspectionReport - Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    var startDateTimeUtc = DateTime.UtcNow;

    var serviceProvider = new ServiceCollection()
        .AddWrInspectionReportFileProcessServices(configurationItem)
        .BuildServiceProvider();

    var cacheService = serviceProvider.GetRequiredService<ICacheService>();
    var inspectionReportFinderCacheService = serviceProvider.GetRequiredService<IInspectionReportFinderCacheService>();
    var outputService = serviceProvider.GetRequiredService<IOutputService>();
    var fileProcessSingleService = serviceProvider.GetRequiredService<IFileProcessSingleService>();

    await cacheService.SetupAsync();
    await outputService.SetupAsync();

    var candidates = (await inspectionReportFinderCacheService.GetInspectionReportFinderResultsAsync(0, int.MaxValue))
        .Where(candidate =>
            !string.IsNullOrEmpty(candidate.PermitNumber)
            && !string.IsNullOrEmpty(candidate.FileId)
            && Guid.TryParse(candidate.FileId, out _))
        .ToList();

    if (candidates.Count == 0)
    {
        ConsoleHelper.WriteLine("INFO - WRADI.ProcessFile.Cmd.WrInspectionReport - No inspection report files to process");
        return;
    }

    var processRun = await outputService.StartProcessRunAsync(
        new ProcessRun
        {
            Description = "Local batch run from inspection_report_finder_result",
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
        var destinationFileName = $"{candidate.PermitNumber!.ToLower()}__{candidate.FileId!.ToLower()}.pdf";

        ConsoleHelper.WriteLine(
            $"INFO - WRADI.ProcessFile.Cmd.WrInspectionReport - [{fileNumber}/{candidates.Count}] {destinationFileName}");

        try
        {
            await fileProcessSingleService.RunAsync(
                new FileProcessSingleRequest
                {
                    FilePath = destinationFileName,
                    DestinationFileName = destinationFileName,
                    DmsPath = candidate.FileUrl,
                    FileId = Guid.Parse(candidate.FileId!),
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
                $"ERROR - WRADI.ProcessFile.Cmd.WrInspectionReport - {destinationFileName} threw: {ex}");
        }
    }

    ConsoleHelper.WriteLine(
        $"INFO - WRADI.ProcessFile.Cmd.WrInspectionReport - Finished at {DateTime.Now:yyyy-MM-dd HH:mm:ss}. ProcessRunId: {processRun.ProcessRunId}");
}
