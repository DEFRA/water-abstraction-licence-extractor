using Npgsql;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public interface INpgsqlDataSourceProvider
{
    NpgsqlDataSource DataSource { get; }
}