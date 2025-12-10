using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using WALE.ProcessFile.Database.PostgreSQL.Migrations;

var serviceProvider = CreateServices();

using var scope = serviceProvider.CreateScope();
UpdateDatabase(scope.ServiceProvider);
return;

static ServiceProvider CreateServices()
{
    return new ServiceCollection()
        .AddFluentMigratorCore()
        .ConfigureRunner(rb => rb
            .AddPostgres()
            .WithGlobalConnectionString("Host=localhost;Port=5432;Database=wale;Username=ea;Password=EnvironmentAgency1")
            .ScanIn(typeof(InitialSchema).Assembly).For.Migrations())
        .AddLogging(lb => lb.AddFluentMigratorConsole())
        .BuildServiceProvider(false);
}

static void UpdateDatabase(IServiceProvider serviceProvider)
{
    var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}