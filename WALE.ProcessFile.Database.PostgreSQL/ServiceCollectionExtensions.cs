using Microsoft.Extensions.DependencyInjection;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL.Services;

namespace WALE.ProcessFile.Database.PostgreSQL;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgreSqlServices(this IServiceCollection services, string dbConnectionString)
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        services.AddSingleton<INpgsqlDataSourceProvider>(new NpgsqlDataSourceProvider(dbConnectionString));
        services.AddTransient<IDatabaseReadService, PostgresReadService>();
        services.AddTransient<IDatabaseWriteService, PostgresWriteService>();
        return services;
    }
}