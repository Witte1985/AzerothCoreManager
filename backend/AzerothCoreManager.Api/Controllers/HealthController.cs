using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using AzerothCoreManager.Core.Services.Interfaces;
using AzerothCoreManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AzerothCoreManager.Api.Controllers;

/// <summary>
/// Health check endpoint
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AzerothCoreDbContext _dbContext;
    private readonly IDockerService _dockerService;
    private readonly IGitService _gitService;

    public HealthController(
        AzerothCoreDbContext dbContext,
        IDockerService dockerService,
        IGitService gitService)
    {
        _dbContext = dbContext;
        _dockerService = dockerService;
        _gitService = gitService;
    }

    /// <summary>
    /// Get health status
    /// </summary>
    /// <returns>Health status with timestamp</returns>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseHealthy = await _dbContext.Database.CanConnectAsync(cancellationToken);
        var dockerHealthy = await IsDockerHealthyAsync(cancellationToken);
        var gitHealthy = await IsGitHealthyAsync(cancellationToken);

        var overallStatus = databaseHealthy && dockerHealthy && gitHealthy ? "healthy" : "degraded";

        return Ok(new
        {
            status = overallStatus,
            timestamp = DateTime.UtcNow,
            dependencies = new
            {
                database = databaseHealthy ? "healthy" : "unhealthy",
                docker = dockerHealthy ? "healthy" : "unhealthy",
                git = gitHealthy ? "healthy" : "unhealthy"
            }
        });
    }

    private async Task<bool> IsDockerHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _dockerService.IsDockerAvailableAsync(cancellationToken);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> IsGitHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _gitService.IsGitAvailableAsync(cancellationToken);
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
