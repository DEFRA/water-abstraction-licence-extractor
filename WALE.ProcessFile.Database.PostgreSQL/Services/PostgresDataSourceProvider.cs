using Npgsql;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class PostgresDataSourceProvider(string connectionString)
{
    public NpgsqlDataSource DataSource { get; } = NpgsqlDataSource.Create(connectionString);
}