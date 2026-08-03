using Microsoft.Extensions.DependencyInjection;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;

namespace WRADI.Database.PostgreSQL.AbstractionLicence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAbstractionLicencePostgreSqlServices(
        this IServiceCollection services)
    {
        services.AddTransient<IAbstractionLicenceDatabaseReadService, PostgresAbstractionLicenceReadService>();
        services.AddTransient<IAbstractionLicenceDatabaseWriteService, PostgresAbstractionLicenceWriteService>();
        
        return services;
    }
}