using System.IO.Compression;
using Amazon.SQS;
using Microsoft.AspNetCore.ResponseCompression;
using Scalar.AspNetCore;
using WALE.Api.Areas.BFF.Models;
using WALE.Api.Interfaces;
using WALE.Api.Services;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL;
using WALE.ProcessFile.Services.AwsS3;
using WALE.ProcessFile.Services.AwsSqs;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Output;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Database.PostgreSQL.AbstractionLicence;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Output.AbstractionLicence;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.AddOutputCache();
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

if (true || app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors();
app.UseResponseCaching();
app.UseResponseCompression();
app.UseOutputCache();
app.MapControllers();
app.MapHealthChecks("/healthz");
app.Run();

return;

static void ConfigureServices(IServiceCollection services, IConfigurationRoot config)
{
    services.AddResponseCaching();
    services.AddControllers();
    services.AddOpenApi();
    services.AddHealthChecks();
    services.AddMemoryCache();

    services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy
                .SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    var dbHost = config.GetValue<string>("POSTGRESQL_HOST")
        ?? throw new InvalidOperationException("POSTGRESQL_HOST connection string not configured");
    var dbPort = int.Parse(config.GetValue<string>("POSTGRESQL_PORT")
        ?? throw new InvalidOperationException("POSTGRESQL_PORT connection string not configured"));
    var dbDatabaseName = config.GetValue<string>("POSTGRESQL_DBNAME")
        ?? throw new InvalidOperationException("POSTGRESQL_DBNAME connection string not configured");
    var dbUsername = config.GetValue<string>("POSTGRESQL_USERNAME")
        ?? throw new InvalidOperationException("POSTGRESQL_USERNAME connection string not configured");
    var dbPassword = config.GetValue<string>("POSTGRESQL_PASSWORD")
        ?? throw new InvalidOperationException("POSTGRESQL_PASSWORD connection string not configured");
    
    var awsRegionName = config.GetValue<string>("AwsRegionName")
        ?? config.GetValue<string>("AwsS3RegionName")
        ?? throw new NullReferenceException("AwsRegionName");
    var s3BucketName = config.GetValue<string>("AwsS3BucketName")
        ?? throw new NullReferenceException("AwsS3BucketName");
    var awsAccessKey = config.GetValue<string>("AwsAccessKey");
    var awsSecretKey = config.GetValue<string>("AwsSecretKey");
    var awsSessionToken = config.GetValue<string>("AwsSessionToken");
    
    services
        .AddPostgreSqlServices(dbHost, dbPort, dbDatabaseName, dbUsername, dbPassword)
        .AddAbstractionLicencePostgreSqlServices()
        .AddAwsS3Services(
            awsRegionName,
            s3BucketName,
            awsAccessKey,
            awsSecretKey,
            awsSessionToken)   
        .AddAwsSqsServices(
            awsRegionName,
            awsAccessKey,
            awsSecretKey,
            awsSessionToken)
        .AddTransient<IOutputService, DatabaseOutputService>()
        .AddTransient<IAbstractionLicenceOutputService, DatabaseAbstractionLicenceOutputService>()
        .AddTransient<ICacheService, DatabaseCacheService>()
        .AddTransient<IAbstractionLicenceCacheService, DatabaseAbstractionLicenceCacheService>()
        .AddTransient<ILicenceListItemModelService, LicenceListItemModelService>()
        .AddTransient<IUiProcessRunService, UiProcessRunService>()
        .AddTransient<ILicenceListRepository, DatabaseAbstractionLicenceOutputService>();
}