using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WRADI.ProcessFile.Local.BackgroundServices;
using WRADI.Services.LicenceFinder;

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddUserSecrets<Program>(optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Register application services
        services.AddLicenceFinderApplicationLocalHosted(configuration);
        
        // Register background worker
        services.AddHostedService<LicenceFinderHostedService>();
    })
    .RunConsoleAsync();