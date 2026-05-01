using AzerothCoreManager.Core.Contracts;

namespace AzerothCoreManager.Core.Services.Interfaces;

public interface IModuleConfigService
{
    Task<ModuleConfigSchema> GetConfigSchemaAsync(string moduleId, CancellationToken cancellationToken = default);
    Task RefreshCacheAsync(CancellationToken cancellationToken = default);
}
