using Scalar.AspNetCore;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL;
using WALE.ProcessFile.Services.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

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
            policy.WithOrigins(
                    "http://localhost:5173",  // Vite dev server
                    "http://localhost:3000",   // Docker/production portal
                    "http://localhost:8080",
                    "http://localhost"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    var dbHost = config.GetConnectionString("POSTGRESQL_HOST")
        ?? throw new InvalidOperationException("POSTGRESQL_HOST connection string not configured");
    var dbPort = int.Parse(config.GetConnectionString("POSTGRESQL_PORT")
        ?? throw new InvalidOperationException("POSTGRESQL_PORT connection string not configured"));
    var dbDatabaseName = config.GetConnectionString("POSTGRESQL_DBNAME")
        ?? throw new InvalidOperationException("POSTGRESQL_DBNAME connection string not configured");
    var dbUsername = config.GetConnectionString("POSTGRESQL_USERNAME")
        ?? throw new InvalidOperationException("POSTGRESQL_USERNAME connection string not configured");
    var dbPassword = config.GetConnectionString("POSTGRESQL_PASSWORD")
        ?? throw new InvalidOperationException("POSTGRESQL_PASSWORD connection string not configured");
    
    services
        .AddPostgreSqlServices(dbHost, dbPort, dbDatabaseName, dbUsername, dbPassword)
        .AddTransient<IOutputService, DatabaseOutputService>()
        .AddTransient<ICacheService, DatabaseCacheService>();
}