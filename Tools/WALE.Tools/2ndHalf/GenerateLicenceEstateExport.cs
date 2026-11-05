using System.Net;
using WALE.ProcessFile.Core.Helpers;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf;

/// Calls the API so the export runs beside the database rather than across the VPN.
public static class GenerateLicenceEstateExport
{
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(90);

    public static async Task<int> GenerateAsync(int processRunId, string? licenceNumber)
    {
        ConsoleHelper.WriteLine($"Exporting from {KeyConfig.ApiBaseUrl}, "
            + $"ProcessRun {processRunId}"
            + (licenceNumber == null ? " (whole run)" : $", licence {licenceNumber}"));

        using var httpClient = new HttpClient { BaseAddress = new Uri(KeyConfig.ApiBaseUrl) };

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "LicenceExport");

        Directory.CreateDirectory(folder);

        return licenceNumber == null
            ? await GenerateRunAsync(httpClient, processRunId, folder)
            : await GenerateLicenceAsync(httpClient, processRunId, licenceNumber, folder);
    }

    private static async Task<int> GenerateRunAsync(
        HttpClient httpClient,
        int processRunId,
        string folder)
    {
        var started = await httpClient.PostAsync(
            $"/Extractor/LicenceEstateExport/StartRunExport?processRunId={processRunId}",
            null);

        if (!started.IsSuccessStatusCode)
        {
            ConsoleHelper.WriteLine($"The API returned {(int)started.StatusCode} starting the "
                + "export");

            return 1;
        }

        var url = await WaitForZipAsync(httpClient, processRunId);

        if (url == null)
        {
            ConsoleHelper.WriteLine($"No zip after {GiveUpAfter.TotalMinutes} minutes. The export "
                + "reports a failure only over SignalR, so check the API log.");

            return 1;
        }

        var zipPath = Path.Combine(folder, $"run-{processRunId}.zip");

        await using (var download = await httpClient.GetStreamAsync(url))
        await using (var file = File.Create(zipPath))
        {
            await download.CopyToAsync(file);
        }

        ConsoleHelper.WriteLine(
            $"{zipPath} ({new FileInfo(zipPath).Length / 1024 / 1024} MB)");

        return 0;
    }

    private static async Task<string?> WaitForZipAsync(HttpClient httpClient, int processRunId)
    {
        var path = $"/Extractor/LicenceEstateExport/GetRunExportUrl?processRunId={processRunId}";
        var giveUpAt = DateTime.UtcNow + GiveUpAfter;

        while (DateTime.UtcNow < giveUpAt)
        {
            await Task.Delay(PollEvery);

            var response = await httpClient.GetAsync(path);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                ConsoleHelper.WriteLine("Still exporting...");
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                // Kept polling: what failed is the question, not the export.
                ConsoleHelper.WriteLine($"WARNING - the API returned {(int)response.StatusCode} "
                    + "asking for the zip");

                continue;
            }

            using var document = System.Text.Json.JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            return document.RootElement.GetProperty("url").GetString();
        }

        return null;
    }

    private static async Task<int> GenerateLicenceAsync(
        HttpClient httpClient,
        int processRunId,
        string licenceNumber,
        string folder)
    {
        var response = await httpClient.GetAsync(
            "/Extractor/LicenceEstateExport/GetLicenceByNumber"
            + $"?licenceNumber={Uri.EscapeDataString(licenceNumber)}"
            + $"&processRunId={processRunId}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            ConsoleHelper.WriteLine($"No completed licence {licenceNumber} in run {processRunId}");
            return 1;
        }

        if (!response.IsSuccessStatusCode)
        {
            ConsoleHelper.WriteLine($"The API returned {(int)response.StatusCode}");
            return 1;
        }

        var name = new string(licenceNumber.Where(char.IsLetterOrDigit).ToArray()) + ".json";
        var path = Path.Combine(folder, name);

        await File.WriteAllTextAsync(path, await response.Content.ReadAsStringAsync());

        ConsoleHelper.WriteLine($"{licenceNumber} -> {path} "
            + $"({new FileInfo(path).Length / 1024} kB)");

        return 0;
    }
}
