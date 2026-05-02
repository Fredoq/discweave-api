using Cratebase.Api;
using Cratebase.Api.Features;
using Cratebase.Application;
using Cratebase.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCratebaseApplication();
builder.Services.AddCratebaseInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

app.MapCratebaseEndpoints();

app.MapGet("/health", () =>
{
    HealthResponse response = new()
    {
        Service = "cratebase-api",
        Status = "ok"
    };

    return Results.Ok(response);
})
.WithName("GetHealth");

await app.RunAsync();
