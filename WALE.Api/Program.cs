using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Scalar.AspNetCore;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL;
using WALE.ProcessFile.Services.AwsS3;
using WALE.ProcessFile.Services.AwsSqs;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Output;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

if (true || app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors();
app.UseResponseCompression();
app.MapControllers();
app.MapHealthChecks("/healthz");
app.Run();
return;

static void ConfigureServices(IServiceCollection services, IConfigurationRoot config)
{
    services.AddControllers();
    services.AddResponseCaching();
    
    services.AddOpenApi();
    services.AddHealthChecks();

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
        ?? throw new NullReferenceException("AwsRegionName");
    var s3BucketName = config.GetValue<string>("AwsS3BucketName")
        ?? throw new NullReferenceException("AwsS3BucketName");
    var awsAccessKey = config.GetValue<string>("AwsAccessKey");
    var awsSecretKey = config.GetValue<string>("AwsSecretKey");
    var awsSessionToken = config.GetValue<string>("AwsSessionToken");

    services
        .AddSingleton<IAmazonSQS>(_ => AwsSqsHelper.GetAwsSqsClient(
            awsRegionName,
            awsAccessKey,
            awsSecretKey,
            awsSessionToken))
        .AddOptions<AwsSqsQueueConfig>()
        .Bind(config.GetSection("AwsQueueConfig"));
    
        // Commented out for now till these exist in DEV env
        /*.Validate(configLocal => !string.IsNullOrWhiteSpace(configLocal.OrchestratorQueue),
            "AwsQueueConfig:OrchestratorQueue is required")
        .Validate(configLocal => !string.IsNullOrWhiteSpace(configLocal.FileProcessQueue),
            "AwsQueueConfig:FileProcessQueue is required")
        .ValidateOnStart();*/
    
    services
        .AddPostgreSqlServices(dbHost, dbPort, dbDatabaseName, dbUsername, dbPassword)
        .AddS3Services(
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
        .AddTransient<ICacheService>(sp => new DatabaseCacheService(
            sp.GetRequiredService<IDatabaseReadService>(),
            sp.GetRequiredService<IDatabaseWriteService>()));
}