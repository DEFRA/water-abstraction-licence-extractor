using Microsoft.Extensions.DependencyInjection;
using WALE.ProcessFile.Core.Interfaces;
using WRADI.Services.LicenceFinder.Implementations;

namespace WRADI.Services.LicenceFinder;

public static class LicenceFinderServiceExtensions
{
    public static IServiceCollection AddLicenceFinderApplication(this IServiceCollection services)
    {
        // Existing registration extension from your solution.
        services.AddLicenceFinderServices();
        services.AddScoped<ILicenceFinderService, LicenceFinderService>();
        return services;
    }
}
