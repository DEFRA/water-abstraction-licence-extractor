using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

ConfigureServices(builder.Services);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/healthz");
app.Run();
return;

static void ConfigureServices(IServiceCollection services)
{
    services.AddControllers();
    services.AddOpenApi();
    services.AddHealthChecks();
}