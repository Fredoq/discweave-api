using Cratebase.Application.Imports;
using Cratebase.Importing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;

namespace Cratebase.LocalAgent;

public static class LocalAgentWebHost
{
    public const int Port = 43817;

    public static IHost Create(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        string[] allowedOrigins =
        [
            .. builder.Configuration
                .GetSection("LocalAgent:CorsOrigins")
                .GetChildren()
                .Select(origin => origin.Value)
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin!)
        ];
        if (allowedOrigins.Length == 0)
        {
            allowedOrigins = ["http://127.0.0.1:5173", "http://localhost:5173"];
        }

        _ = builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, Port);
        });
        _ = builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
        _ = builder.Services.AddCors(options =>
        {
            options.AddPolicy("CratebaseWeb", policy =>
            {
                _ = policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
        _ = builder.Services.AddSingleton<IAudioMetadataReader, AtlAudioMetadataReader>();
        _ = builder.Services.AddSingleton<ReleaseFolderScanner>();
        _ = builder.Services.AddSingleton<ILocalFolderPicker, MacOsFolderPicker>();
        _ = builder.Services.AddSingleton<LocalAgentScanHandler>();

        WebApplication app = builder.Build();
        _ = app.UseCors("CratebaseWeb");
        _ = app.MapGet("/health", () => Results.Ok(new LocalAgentHealthResponse(
            "cratebase-local-agent",
            "0.1.0",
            1,
            OperatingSystem.IsMacOS() ? "ready" : "unsupported")));
        _ = app.MapPost("/v1/imports/pick-and-scan", LocalAgentScanHandler.HandleAsync);

        return app;
    }
}
