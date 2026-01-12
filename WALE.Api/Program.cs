using Scalar.AspNetCore;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL;
using WALE.ProcessFile.Services.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

//if (app.Environment.IsDevelopment())
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

    var dbConnectionString = config.GetConnectionString("PostgreSQL")
                             ?? throw new InvalidOperationException("PostgreSQL connection string not configured");

    services
        .AddPostgreSqlServices(dbConnectionString)
        .AddTransient<IOutputService, DatabaseOutputService>()
        .AddTransient<ICacheService, DatabaseCacheService>();
}