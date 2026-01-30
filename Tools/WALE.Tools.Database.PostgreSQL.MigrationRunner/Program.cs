using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WALE.ProcessFile.Database.PostgreSQL.Migrations;

var serviceProvider = CreateServices();

using var scope = serviceProvider.CreateScope();
UpdateDatabase(scope.ServiceProvider);

return;

static ServiceProvider CreateServices()
{
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddUserSecrets<Program>()
        .AddEnvironmentVariables()
        .Build();

    var dbHost = config.GetValue<string>("POSTGRESQL_HOST");
    if (string.IsNullOrEmpty(dbHost)) throw new InvalidOperationException("POSTGRESQL_HOST connection string not configured");
    
    var dbPort = config.GetValue<string>("POSTGRESQL_PORT");
    if (string.IsNullOrEmpty(dbPort)) throw new InvalidOperationException("POSTGRESQL_PORT connection string");
    
    var dbDatabaseName = config.GetValue<string>("POSTGRESQL_DBNAME");
    if (string.IsNullOrEmpty(dbDatabaseName)) throw new InvalidOperationException("POSTGRESQL_DBNAME connection string not configured");
    
    var dbUsername = config.GetValue<string>("POSTGRESQL_USERNAME");
    if (string.IsNullOrEmpty(dbUsername)) throw new InvalidOperationException("POSTGRESQL_USERNAME connection string not configured");
    
    var dbPassword = config.GetValue<string>("POSTGRESQL_PASSWORD");
    if (string.IsNullOrEmpty(dbPassword)) throw new InvalidOperationException("POSTGRESQL_PASSWORD connection string not configured");

    var dbConnectionString =
        $"Host={dbHost};Port={dbPort};Database={dbDatabaseName};Username={dbUsername};Password={dbPassword};Timeout=300;CommandTimeout=300;KeepAlive=300;";
    
    return new ServiceCollection()
        .AddFluentMigratorCore()
        .ConfigureRunner(rb => rb
            .AddPostgres()
            .WithGlobalConnectionString(dbConnectionString)
            .ScanIn(typeof(InitialSchema).Assembly).For.Migrations())
        .AddLogging(lb => lb.AddFluentMigratorConsole())
        .BuildServiceProvider(false);
}

static void UpdateDatabase(IServiceProvider serviceProvider)
{
    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}