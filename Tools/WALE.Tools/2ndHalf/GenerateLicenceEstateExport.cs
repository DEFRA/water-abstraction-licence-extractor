using System.Text.Json;
using Dapper;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.Services.Output.AbstractionLicence;

namespace WALE.Tools._2ndHalf;

public static class GenerateLicenceEstateExport
{
    public static async Task<int> GenerateAsync(int processRunId, string? licenceNumber)
    {
        ConsoleHelper.WriteLine($"Exporting from {KeyConfig.PostgresHost}, "
            + $"ProcessRun {processRunId}"
            + (licenceNumber == null ? " (whole run)" : $", licence {licenceNumber}"));

        NpgsqlDataSourceProvider dataSourceProvider = new(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);

        await using var dataSource = dataSourceProvider.DataSource;
        await using var connection = await dataSource.OpenConnectionAsync();

        var readService = new PostgresReadService(dataSourceProvider);

        var exportService = new LicenceEstateExportService(
            new DatabaseAbstractionLicenceOutputService(
                readService,
                new PostgresAbstractionLicenceReadService(dataSourceProvider),
                new PostgresAbstractionLicenceWriteService(dataSourceProvider),
                new PostgresWriteService(dataSourceProvider)),
            new PostgresAbstractionLicenceReadService(dataSourceProvider),
            new DocumentTextService(readService, new PostgresWriteService(dataSourceProvider)));

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "LicenceExport");

        Directory.CreateDirectory(folder);

        if (licenceNumber == null)
        {
            var zipPath = Path.Combine(folder, $"run-{processRunId}.zip");

            await using (var file = File.Create(zipPath))
            {
                var summary = await exportService.ExportRunAsync(processRunId, file);

                ConsoleHelper.WriteLine($"{summary.Written} licences written"
                    + $", {summary.NotExtracted} the run did not extract"
                    + $", {summary.WithoutDocumentText} without document text"
                    + $", {summary.WithoutTemplateType} without a template type"
                    + (summary.Unreadable > 0 ? $", {summary.Unreadable} unreadable" : ""));
            }

            ConsoleHelper.WriteLine(
                $"{zipPath} ({new FileInfo(zipPath).Length / 1024 / 1024} MB)");

            return 0;
        }

        var licenceId = await connection.ExecuteScalarAsync<int?>(
            """
            SELECT licence_id
            FROM licence
            WHERE process_run_id = @ProcessRunId
                AND status = 'Ok'
                AND upper(licence_number) = upper(@LicenceNumber)
            LIMIT 1
            """,
            new { ProcessRunId = processRunId, LicenceNumber = licenceNumber });

        if (licenceId == null)
        {
            ConsoleHelper.WriteLine($"No completed licence {licenceNumber} in run {processRunId}");
            return 1;
        }

        var licence = await exportService.ExportLicenceAsync(licenceId.Value);

        if (licence == null)
        {
            ConsoleHelper.WriteLine($"Licence {licenceId} could not be read");
            return 1;
        }

        var name = new string(licence.LicenceNumber!.Value!.Where(char.IsLetterOrDigit).ToArray())
            + ".json";
        var path = Path.Combine(folder, name);

        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(licence, LicenceExportSerializer.Options));

        ConsoleHelper.WriteLine($"{licence.LicenceNumber.Value} -> {path} "
            + $"({new FileInfo(path).Length / 1024} kB, "
            + $"{licence.DocumentText?.Length ?? 0} pages, "
            + $"{licence.Verifications.Length} verifications)");

        return 0;
    }
}
