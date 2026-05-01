using AzerothCoreManager.Core.Contracts;
using AzerothCoreManager.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzerothCoreManager.Api.Controllers;

/// <summary>
/// Module catalogue endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ModulesController : ControllerBase
{
    private readonly IModuleCatalogService _moduleCatalogService;
    private readonly IModuleConfigService _moduleConfigService;

    public ModulesController(
        IModuleCatalogService moduleCatalogService,
        IModuleConfigService moduleConfigService)
    {
        _moduleCatalogService = moduleCatalogService;
        _moduleConfigService = moduleConfigService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ModuleDto>>> Get(
        [FromQuery] ServerType? serverType,
        CancellationToken cancellationToken)
    {
        var modules = await _moduleCatalogService.ListAsync(serverType, cancellationToken);
        return Ok(modules);
    }

    [HttpGet("{moduleId}/config")]
    public async Task<ActionResult<ModuleConfigSchema>> GetConfig(
        string moduleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var schema = await _moduleConfigService.GetConfigSchemaAsync(moduleId, cancellationToken);
            return Ok(schema);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
