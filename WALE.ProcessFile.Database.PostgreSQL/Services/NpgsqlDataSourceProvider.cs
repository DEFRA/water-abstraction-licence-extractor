using Npgsql;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class NpgsqlDataSourceProvider : INpgsqlDataSourceProvider
{
    public static List<(int, string)> Queries = [];
    public static int QueryNumber = 0;
    
    public NpgsqlDataSourceProvider(string host, int port, string databaseName, string username, string password)
    {
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = databaseName,
            Username = username,
            Password = password,
            Timeout = 0,
            CommandTimeout = 0,
            KeepAlive = 30,
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 30
        };

        DataSource = NpgsqlDataSource.Create(connectionString);
    }
    
    public NpgsqlDataSource DataSource { get; }
}