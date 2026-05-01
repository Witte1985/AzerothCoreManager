using AzerothCoreManager.Core.Contracts;
using AzerothCoreManager.Core.Services.Interfaces;

namespace AzerothCoreManager.Infrastructure.Services;

/// <summary>
/// In-memory module catalog until a persistent catalog is introduced.
/// </summary>
public sealed class ModuleCatalogService : IModuleCatalogService
{
    private static readonly IReadOnlyList<ModuleDto> Modules =
    [
        new()
        {
            Id = "mod-ah-bot",
            Name = "Auction House Bot",
            Description = "Adds AI-driven auction house activity to improve economy simulation.",
            Repository = "https://github.com/NathanHandley/mod-ah-bot-plus",
            Branch = "master",
            RequiresPlayerbots = false
        },
        new()
        {
            Id = "mod-autobalance",
            Name = "Auto Balance",
            Description = "Automatically scales dungeon and raid difficulty to the active group size.",
            Repository = "https://github.com/azerothcore/mod-autobalance",
            Branch = "master",
            RequiresPlayerbots = false
        },
        new()
        {
            Id = "mod-transmog",
            Name = "Transmogrification",
            Description = "Lets players change item appearance while keeping original stats.",
            Repository = "https://github.com/azerothcore/mod-transmog",
            Branch = "master",
            RequiresPlayerbots = false
        },
        new()
        {
            Id = "mod-playerbots",
            Name = "Playerbots",
            Description = "Enables AI-controlled party members and world bots for Playerbots builds.",
            Repository = "https://github.com/mod-playerbots/mod-playerbots",
            Branch = "master",
            RequiresPlayerbots = true
        }
    ];

    public Task<IReadOnlyList<ModuleDto>> ListAsync(
        ServerType? serverType = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ModuleDto> modules = serverType switch
        {
            ServerType.Standard => Modules.Where(module => !module.RequiresPlayerbots).ToList(),
            _ => Modules
        };

        return Task.FromResult(modules);
    }
}
