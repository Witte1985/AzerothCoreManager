using AzerothCoreManager.Core.Services.Interfaces;
using AzerothCoreManager.Infrastructure.Configuration;
using AzerothCoreManager.Infrastructure.Data;
using AzerothCoreManager.Infrastructure.Services;
using AzerothCoreManager.Infrastructure.Services.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzerothCoreManager.Infrastructure;

/// <summary>
/// Infrastructure service registration helpers.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services
            .AddOptions<DockerOptions>()
            .Bind(configuration.GetSection(DockerOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.SocketPath), "Docker:SocketPath is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BuildsPath), "Docker:BuildsPath is required.")
            .ValidateOnStart();

        services
            .AddOptions<StackUpdateCheckerOptions>()
            .Bind(configuration.GetSection("StackUpdateChecker"))
            .ValidateOnStart();

        services.AddDbContext<AzerothCoreDbContext>(options => options.UseSqlite(connectionString));
        services.AddHttpClient();
        services.AddScoped<IDockerService, DockerService>();
        services.AddScoped<IGitService, GitService>();
        services.AddScoped<IBuildService, BuildService>();
        services.AddScoped<IModuleCatalogService, ModuleCatalogService>();
        services.AddScoped<IModuleConfigService, ModuleConfigService>();
        services.AddScoped<IStackConfigurationValidator, StackConfigurationValidator>();
        services.AddScoped<IStackService, StackService>();
        services.AddScoped<IStackVersionService, StackVersionService>();

        // Register module configuration parsers
        services.AddScoped<IModuleConfigParser, PlayerbotConfigParser>();
        services.AddScoped<IModuleConfigParser, TransmogConfigParser>();
        services.AddScoped<IModuleConfigParser, AutoBalanceConfigParser>();
        services.AddScoped<IModuleConfigParser, AhBotConfigParser>();
        
        // Background services
        services.AddHostedService<StackUpdateCheckerService>();

        return services;
    }
}
