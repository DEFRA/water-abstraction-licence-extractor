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
using WALE.ProcessFile.Services.Services;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Database.PostgreSQL.AbstractionLicence;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Cache.WrInspectionReport;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;
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
        // TEMPORARY (2026-09-09): swapped from AddAwsS3Services to a local-filesystem
        // IFileService - we have no working AWS credentials for wradi-s3-ingress-dev (the real
        // bucket), only for shswals3bkt001 (NALD-transfer only, IAM-scoped, confirmed via
        // Access Denied). Points at the existing golden-set corpus, which has real overlap with
        // inspection_report_finder_result by permit number (212 of 2,772 real discovered
        // permits also exist in the 789-doc local WR51 corpus). Revert to AddAwsS3Services once
        // real S3 access exists.
        .AddTransient<IFileService>(_ => new LocalFileService("/Users/edwardbutler/Documents/TestLicences/"))
        .AddAwsSqsServices(
            awsRegionName,
            awsAccessKey,
            awsSecretKey,
            awsSessionToken)
        .AddTransient<IOutputService, DatabaseOutputService>()
        .AddTransient<IAbstractionLicenceOutputService, DatabaseAbstractionLicenceOutputService>()
        .AddTransient<ICacheService, DatabaseCacheService>()
        .AddTransient<IInspectionReportFinderCacheService, DatabaseInspectionReportFinderCacheService>()
        .AddTransient<IAbstractionLicenceCacheService, DatabaseAbstractionLicenceCacheService>()
        .AddTransient<ILicenceListItemModelService, LicenceListItemModelService>()
        .AddTransient<IUiProcessRunService, UiProcessRunService>()
        .AddTransient<ILicenceListRepository, DatabaseAbstractionLicenceOutputService>();
}