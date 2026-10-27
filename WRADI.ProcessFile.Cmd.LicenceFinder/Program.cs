using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.LicenceFinder;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
            config.AddUserSecrets(
                System.Reflection.Assembly.GetExecutingAssembly());
    })
    .ConfigureServices((_, services) =>
        services.AddLicenceFinderApplication())
    .Build();

using var scope = host.Services.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<ILicenceFinderService>();
var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
var flow = args.FirstOrDefault() ?? config["LicenceFinder:FlowToRun"] ?? "FindLicenceFilesAsync";

var messageRequest = new LicenceFinderMessageRequest
{
    FlowToRun = flow,
    RegionName = config["LicenceFinder:RegionName"],
    RestrictToRegionName = config["LicenceFinder:RestrictToRegionName"]

};
var output = await service.ExecuteAsync(messageRequest);
Console.WriteLine($"File saved to: {output}");