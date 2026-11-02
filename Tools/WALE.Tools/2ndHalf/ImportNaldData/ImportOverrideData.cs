using Dapper;
using Npgsql;
using NpgsqlTypes;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;
using WALE.Tools.Helpers;

namespace WALE.Tools._2ndHalf.ImportNaldData;

public static class ImportOverrideData
{
    public static async Task<int> ImportAsync()
    {
        ConsoleHelper.WriteLine($"Starting override data import into {KeyConfig.PostgresHost}...");

        var records = GetOverridesFromResourcesFolder();

        if (records.Count == 0)
        {
            ConsoleHelper.WriteLine("No overrides found in the override file...");
            return 1;
        }

        ConsoleHelper.WriteLine($"{records.Count} records to insert");

        NpgsqlDataSourceProvider npgsqlDataSourceProvider = new(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);

        await using var dataSource = npgsqlDataSourceProvider.DataSource;

        // The clear shares the load's transaction, so a failed import leaves the previous
        // overrides in place rather than an empty table.
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await ClearExistingOverrideDataAsync(connection);
        var written = await CopyOverrideRowsAsync(connection, records);

        if (written != (ulong)records.Count)
        {
            throw new InvalidOperationException(
                $"COPY wrote {written} rows but {records.Count} were read from the override file. " +
                "Rolling back - override_master is unchanged.");
        }

        await transaction.CommitAsync();

        await SetOverrideImportDateAsync(dataSource);

        ConsoleHelper.WriteLine($"{written} records inserted");
        ConsoleHelper.WriteLine("Override import done");

        return 1;
    }

    private static async Task<ulong> CopyOverrideRowsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<Override> records)
    {
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY public.override_master (
                permit_number, licence_reference, file_url, nald_issue_no, nald_increment_no,
                override_type, file_id
            ) FROM STDIN (FORMAT BINARY)
            """);

        foreach (var record in records)
        {
            await writer.StartRowAsync();

            await writer.WriteAsync(record.PermitNumber, NpgsqlDbType.Text);
            await writer.WriteAsync(record.LicenceReference, NpgsqlDbType.Text);
            await writer.WriteAsync(record.FileUrl, NpgsqlDbType.Text);
            await writer.WriteAsync(record.IssueNo, NpgsqlDbType.Text);
            await writer.WriteAsync(record.IncrementNo?.ToString(), NpgsqlDbType.Text);
            await writer.WriteAsync(record.OverrideType, NpgsqlDbType.Text);
            await writer.WriteAsync(record.FileId, NpgsqlDbType.Text);
        }

        return await writer.CompleteAsync();
    }

    /// <summary>Called only after the load commits, so the date never outruns the data.</summary>
    private static async Task SetOverrideImportDateAsync(NpgsqlDataSource dataSource)
    {
        var sql = @"
            INSERT INTO public.import_dates (data_source, date_time)
            VALUES (@DataSource, @DateTime)";

        await using var connection = await dataSource.OpenConnectionAsync();

        await connection.ExecuteAsync(sql, new
        {
            // The licence finder reads the date back by this same string.
            DataSource = "OverrideMaster",
            DateTime = DateTime.Now
        });
    }

    private static async Task ClearExistingOverrideDataAsync(NpgsqlConnection connection) =>
        await connection.ExecuteAsync("DELETE FROM public.override_master;");

    private static List<Override> GetOverridesFromResourcesFolder()
    {
        var filepaths = FindFilesByPattern("Override_");

        if (filepaths.Count == 0)
        {
            throw new FileNotFoundException(
                "No override files were found in the resources folder matching 'Override_'.");
        }

        // Refusing rather than picking one: the resources folder accumulates old override files,
        // and importing last month's silently drops every override added since.
        if (filepaths.Count != 1)
        {
            throw new InvalidOperationException(
                "Only 1 override file should be used, but the resources folder holds "
                + $"{filepaths.Count}: {string.Join(", ", filepaths.Select(Path.GetFileName))}");
        }

        ConsoleHelper.WriteLine($"Override file is {filepaths[0]}");

        return new LicenceFileProcessor().ExtractExcel<List<Override>>(
            filepaths[0],
            new Dictionary<string, List<string>>
            {
                { "Permit Number", ["PermitNumber"]},
                { "LicenceReference", ["LicenceReference"]},
                { "File URL", ["FileUrl"]},
                { "FullPath", ["FileUrl"]},
                { "DefinitiveURL", ["FileUrl"]},
                { "NALD Issue_No", ["IssueNo"]},
                { "NALD Issue No.", ["IssueNo"]},
                { "NALD Increment_No", ["IncrementNo"]},
                { "NALD Increment No.", ["IncrementNo"]},
                { "OverrideType", ["OverrideType"]},
                { "File ID", ["FileId"]}
            },
            ["Duplicatecheck"]);
    }

    private static List<string> FindFilesByPattern(string pattern)
    {
        var matchingFiles = Directory.GetFiles("../../../Resources").ToList();

        return matchingFiles
            .Distinct()
            .Where(filePath => filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
