using Npgsql;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class NpgsqlDataSourceProvider : INpgsqlDataSourceProvider
{
    public static readonly bool AddDebugLogging = false;
    
    public static readonly List<(int, string)> Queries = [];
    public static int QueryNumber = 0;
    
    public NpgsqlDataSourceProvider(
        string host,
        int port,
        string databaseName,
        string username,
        string password,
        int maxPoolSize = 30)
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
            Pooling = true,
            MaxPoolSize = maxPoolSize
        };

        DataSource = NpgsqlDataSource.Create(connectionString);
    }
    
    public NpgsqlDataSource DataSource { get; }
}