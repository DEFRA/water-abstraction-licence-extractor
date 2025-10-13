using Microsoft.Data.SqlClient;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Database.Services;

public class SqlSeverReadServiceService(string connectionString) : IDatabaseReadService
{
    public List<ProcessRun> GetProcessRuns()
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 1 FROM NoOcrPagesMetadataCache";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return reader.GetString(0);
        }

        return null;
    }
}