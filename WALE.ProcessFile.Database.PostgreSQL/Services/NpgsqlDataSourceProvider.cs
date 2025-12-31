using Npgsql;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class NpgsqlDataSourceProvider(string connectionString) : INpgsqlDataSourceProvider
{
    public NpgsqlDataSource DataSource { get; } = NpgsqlDataSource.Create(connectionString);
}