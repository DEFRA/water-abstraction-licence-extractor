using Dapper;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf;

/// A read already re-derives a stale row on its own, so this only pre-computes - worth a tool
/// because an estate export needs a row for every licence in the estate.
public static class RebuildDocumentText
{
    public static async Task<int> RebuildAsync()
    {
        ConsoleHelper.WriteLine($"Rebuilding stored document text on {KeyConfig.PostgresHost}...");

        NpgsqlDataSourceProvider dataSourceProvider = new(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);

        await using var dataSource = dataSourceProvider.DataSource;

        // Every stale row, not just the native-text ones: the version bump makes an OCR-sourced
        // row re-derive on each read too, even though its text is unchanged.
        await using var connection = await dataSource.OpenConnectionAsync();

        var fileIds = (await connection.QueryAsync<Guid>(
            """
            SELECT file_id
            FROM document_text
            WHERE format_version < @FormatVersion
            ORDER BY file_id
            """,
            new { FormatVersion = DocumentTextService.CurrentFormatVersion })).ToList();

        ConsoleHelper.WriteLine($"{fileIds.Count} stored texts are behind version "
            + $"{DocumentTextService.CurrentFormatVersion}");

        if (fileIds.Count == 0)
        {
            return 0;
        }

        var documentTextService = new DocumentTextService(
            new PostgresReadService(dataSourceProvider),
            new PostgresWriteService(dataSourceProvider));

        var rebuilt = 0;
        var failed = 0;

        foreach (var fileId in fileIds)
        {
            try
            {
                await documentTextService.StoreDocumentTextAsync(fileId);
                rebuilt++;
            }
            catch (Exception exception)
            {
                failed++;
                ConsoleHelper.WriteLine($"ERROR - {fileId} - {exception.Message}");
            }

            if ((rebuilt + failed) % 250 == 0)
            {
                ConsoleHelper.WriteLine($"  {rebuilt + failed} of {fileIds.Count}...");
            }
        }

        var remaining = await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*)
            FROM document_text
            WHERE format_version < @FormatVersion
            """,
            new { FormatVersion = DocumentTextService.CurrentFormatVersion });

        ConsoleHelper.WriteLine($"{rebuilt} rebuilt, {failed} failed, {remaining} still behind");

        return 0;
    }
}
