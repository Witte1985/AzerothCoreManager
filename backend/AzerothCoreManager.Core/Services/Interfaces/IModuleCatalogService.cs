using AzerothCoreManager.Core.Contracts;

namespace AzerothCoreManager.Core.Services.Interfaces;

/// <summary>
/// Provides available AzerothCore modules for setup flows.
/// </summary>
public interface IModuleCatalogService
{
    Task<IReadOnlyList<ModuleDto>> ListAsync(ServerType? serverType = null, CancellationToken cancellationToken = default);
}
