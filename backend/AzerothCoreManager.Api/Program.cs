using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json.Serialization;
using AzerothCoreManager.Api.Hubs;
using AzerothCoreManager.Infrastructure;
using AzerothCoreManager.Infrastructure.Configuration;
using AzerothCoreManager.Infrastructure.Data;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting AzerothCore Manager API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    
    // Add SignalR
    builder.Services.AddSignalR();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                ?? new[] { "http://localhost:5173" };
            
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials(); // Required for SignalR
        });
    });

    builder.Services.AddInfrastructure(builder.Configuration);
    
    // Register SignalR event publisher
    builder.Services.AddSingleton<AzerothCoreManager.Core.Services.Interfaces.IBuildEventPublisher, AzerothCoreManager.Api.Services.SignalRBuildEventPublisher>();

    // Configure Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AzerothCoreDbContext>();
        await dbContext.Database.MigrateAsync();
        
        // Ensure builds directory exists
        var dockerOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DockerOptions>>().Value;
        Directory.CreateDirectory(dockerOptions.BuildsPath);
        Log.Information("Builds directory ready at: {BuildsPath}", dockerOptions.BuildsPath);
    }

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "AzerothCore Manager API v1");
            options.RoutePrefix = "swagger";
        });
    }

    // Serve static files (frontend) from wwwroot
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // Use CORS
    app.UseCors("AllowFrontend");

    // Use routing
    app.UseRouting();

    // Map controllers
    app.MapControllers();

    app.MapHub<BuildProgressHub>("/hubs/buildprogress");
    app.MapHub<BuildProgressHub>("/hubs/build-progress");

    // Fallback to index.html for client-side routing (SPA)
    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
