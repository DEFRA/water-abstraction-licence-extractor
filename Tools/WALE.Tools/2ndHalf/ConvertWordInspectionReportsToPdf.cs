using System.Diagnostics;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf;

/// <summary>
/// Converts the Word-format WR51 reports already in the ingress bucket to PDF, and puts the PDF
/// back beside them.
///
/// Why this exists rather than a step in the pipeline: the people who hold the Word documents
/// cannot install LibreOffice, and the documents are too numerous to pass around by hand. They can
/// upload, so the bucket carries the Word file and this converts it where LibreOffice is available.
///
/// Why the bucket rather than the DMS: the ingress key is what the orchestrator asks S3 for, so
/// converting in place is what makes a document processable - nothing else has to change.
///
/// Safe to re-run: a Word file whose PDF is already in the bucket is skipped, and the upload
/// endpoint skips keys already present.
/// </summary>
public static class ConvertWordInspectionReportsToPdf
{
    private const string Prefix = "wr51__";

    private static readonly string[] WordExtensions = [".doc", ".docx"];

    /// <summary>
    /// One LibreOffice invocation per file would pay its startup each time, and the whole list at
    /// once can exceed the command line length limit.
    /// </summary>
    private const int BatchSize = 40;

    /// <summary>Downloads and uploads are per file and mostly waiting, so they overlap.</summary>
    private const int MaxConcurrentTransfers = 8;

    public static async Task<int> RunAsync()
    {
        var httpClient = HttpHelper.GetResilientHttpClient(KeyConfig.ApiBaseUrl, 100, 30);
        var fileService = new ApiFileService(httpClient);

        ConsoleHelper.WriteLine($"Reading the ingress bucket via {httpClient.BaseAddress}");

        var allFiles = await fileService.GetAllFilesAsync();
        var present = new HashSet<string>(allFiles, StringComparer.OrdinalIgnoreCase);

        // The PDF keeps the Word key's stem, so this makes no assumption about how the stem is
        // built - whatever names the Word file names its PDF.
        var pending = allFiles
            .Where(IsWordInspectionReport)
            .Where(key => !present.Contains(PdfKeyFor(key)))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var wordTotal = allFiles.Count(IsWordInspectionReport);

        ConsoleHelper.WriteLine(
            $"{wordTotal} Word-format reports in the bucket, {wordTotal - pending.Count} already "
            + $"converted, {pending.Count} to do");

        if (pending.Count == 0)
        {
            return 0;
        }

        var workingDirectory = Directory.CreateTempSubdirectory("wr51-word-convert-");
        ConsoleHelper.WriteLine($"Working in {workingDirectory.FullName}");

        var converted = 0;
        var failed = new List<string>();

        try
        {
            foreach (var batch in pending.Chunk(BatchSize))
            {
                converted += await ConvertBatchAsync(
                    batch, fileService, workingDirectory.FullName, failed);

                ConsoleHelper.WriteLine($"  {converted}/{pending.Count} converted and uploaded");
            }
        }
        finally
        {
            workingDirectory.Delete(recursive: true);
        }

        ConsoleHelper.WriteLine($"{converted} converted and uploaded, {failed.Count} failed");

        // Named rather than counted: a failure here is one document a run will not see, and the key
        // is what someone needs to go and look at.
        foreach (var key in failed)
        {
            ConsoleHelper.WriteLine($"  FAILED {key}");
        }

        return failed.Count == 0 ? 0 : 1;
    }

    private static bool IsWordInspectionReport(string key) =>
        key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
        && WordExtensions.Any(extension => key.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    private static string PdfKeyFor(string wordKey) => Path.ChangeExtension(wordKey, ".pdf");

    private static async Task<int> ConvertBatchAsync(
        IReadOnlyList<string> batch,
        IFileService fileService,
        string workingDirectory,
        List<string> failed)
    {
        var downloaded = new List<string>();

        await Parallel.ForEachAsync(
            batch,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentTransfers },
            async (key, _) =>
            {
                var path = Path.Combine(workingDirectory, key);

                await using var source = await fileService.GetFileAsStreamAsync(key);

                if (source == null)
                {
                    lock (failed) { failed.Add($"{key} (could not be downloaded)"); }
                    return;
                }

                await using (var destination = File.Create(path))
                {
                    await source.CopyToAsync(destination);
                }

                lock (downloaded) { downloaded.Add(path); }
            });

        if (downloaded.Count == 0)
        {
            return 0;
        }

        RunLibreOffice(downloaded, workingDirectory);

        var uploaded = 0;

        await Parallel.ForEachAsync(
            downloaded,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentTransfers },
            async (wordPath, _) =>
            {
                var pdfPath = Path.ChangeExtension(wordPath, ".pdf");
                var key = Path.GetFileName(pdfPath);

                if (!File.Exists(pdfPath))
                {
                    lock (failed) { failed.Add($"{Path.GetFileName(wordPath)} (no PDF produced)"); }
                    return;
                }

                await using (var stream = File.OpenRead(pdfPath))
                {
                    await fileService.UploadFileAsStreamAsync(
                        key, stream, "application/pdf", StorageFolder.Ingress);
                }

                File.Delete(pdfPath);
                File.Delete(wordPath);

                Interlocked.Increment(ref uploaded);
            });

        return uploaded;
    }

    /// <summary>
    /// ExportNotes is stated rather than left to LibreOffice's default, so a document carrying
    /// comments cannot put them in the PDF a verifier reads. The profile directory is per run: a
    /// LibreOffice already open on the machine otherwise refuses a headless start.
    /// </summary>
    private static void RunLibreOffice(IReadOnlyList<string> wordPaths, string workingDirectory)
    {
        const string filter = "pdf:writer_pdf_Export:"
            + "{\"ExportNotes\":{\"type\":\"boolean\",\"value\":\"false\"},"
            + "\"ExportNotesInMargin\":{\"type\":\"boolean\",\"value\":\"false\"}}";

        var startInfo = new ProcessStartInfo
        {
            FileName = KeyConfig.LibreOfficePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--norestore");
        startInfo.ArgumentList.Add($"-env:UserInstallation=file://{Path.Combine(workingDirectory, "profile")}");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(filter);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(workingDirectory);

        foreach (var path in wordPaths)
        {
            startInfo.ArgumentList.Add(path);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {KeyConfig.LibreOfficePath}");

        process.WaitForExit();
    }
}
