using Npgsql;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class NpgsqlDataSourceProvider : INpgsqlDataSourceProvider
{
    public NpgsqlDataSourceProvider(string host, int port, string databaseName, string username, string password)
    {
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = databaseName,
            Username = username,
            Password = password
        };

        DataSource = NpgsqlDataSource.Create(connectionString);
    }
    
    public NpgsqlDataSource DataSource { get; }
}