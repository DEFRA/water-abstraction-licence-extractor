using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WRADI.DocumentType.AbstractionLicence.Extensions;

public static class AbstractionLicenceServiceRegistration
{
    public static IServiceCollection AddAbstractionLicenceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        return services;
    }
}