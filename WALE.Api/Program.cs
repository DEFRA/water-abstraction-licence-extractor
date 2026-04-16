using Scalar.AspNetCore;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL;
using WALE.ProcessFile.Services.AwsS3;
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

app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("AllowPortal");
app.MapControllers();
app.MapHealthChecks("/healthz");
app.Run();
return;

static void ConfigureServices(IServiceCollection services, IConfigurationRoot config)
{
    services.AddControllers();
    services.AddOpenApi();
    services.AddHealthChecks();

    services.AddCors(options =>
    {
        options.AddPolicy("AllowPortal", policy =>
        {
            policy
                .SetIsOriginAllowed(origin => true)
                /*.WithOrigins(
                    "http://localhost:5173",  // Vite dev server
                    "http://localhost:3000",   // Docker/production portal
                    "http://localhost:8080",
                    "http://localhost"
                )*/
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
    
    var s3AccessKey = config.GetValue<string>("AwsS3AccessKey")
        ?? throw new NullReferenceException("AwsS3AccessKey");
    var s3SecretKey = config.GetValue<string>("AwsS3SecretKey")
        ?? throw new NullReferenceException("AwsS3SecretKey");
    var s3RegionName = config.GetValue<string>("AwsS3RegionName")
        ?? throw new NullReferenceException("AwsS3RegionName");
    var s3BucketName = config.GetValue<string>("AwsS3BucketName")
        ?? throw new NullReferenceException("AwsS3BucketName");
    
    services
        .AddPostgreSqlServices(dbHost, dbPort, dbDatabaseName, dbUsername, dbPassword)
        .AddS3Services(s3AccessKey, s3SecretKey, s3RegionName, s3BucketName)        
        .AddTransient<IOutputService, DatabaseOutputService>()
        .AddTransient<ICacheService>(sp => new DatabaseCacheService(
            sp.GetRequiredService<IDatabaseReadService>(),
            sp.GetRequiredService<IDatabaseWriteService>()));
}