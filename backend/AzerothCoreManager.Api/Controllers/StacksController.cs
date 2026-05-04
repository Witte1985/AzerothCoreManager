using AzerothCoreManager.Core.Contracts;
using AzerothCoreManager.Core.Exceptions;
using AzerothCoreManager.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzerothCoreManager.Api.Controllers;

/// <summary>
/// Stack management endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StacksController : ControllerBase
{
    private readonly IBuildService _buildService;
    private readonly IStackService _stackService;
    private readonly IStackConfigurationValidator _stackConfigurationValidator;
    private readonly IStackDiscoveryService _stackDiscoveryService;

    public StacksController(
        IBuildService buildService,
        IStackService stackService,
        IStackConfigurationValidator stackConfigurationValidator,
        IStackDiscoveryService stackDiscoveryService)
    {
        _buildService = buildService;
        _stackService = stackService;
        _stackConfigurationValidator = stackConfigurationValidator;
        _stackDiscoveryService = stackDiscoveryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StackDetailsDto>>> GetAll(CancellationToken cancellationToken = default)
    {
        var stacks = await _stackService.ListAsync(cancellationToken);
        return Ok(stacks);
    }

    [HttpGet("{stackId}")]
    public async Task<ActionResult<StackDetailsDto>> GetById(string stackId, CancellationToken cancellationToken = default)
    {
        var stack = await _stackService.GetAsync(stackId, cancellationToken);
        return stack is null ? NotFound() : Ok(stack);
    }

    [HttpPost]
    public async Task<ActionResult<CreateStackResponse>> Create(
        [FromBody] StackConfigurationDto configuration,
        CancellationToken cancellationToken)
    {
        var validationResult = await _stackConfigurationValidator.ValidateAsync(configuration, cancellationToken: cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult);
        }

        var stack = await _stackService.CreateAsync(configuration, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { stackId = stack.StackId },
            new CreateStackResponse
            {
                StackId = stack.StackId,
                Status = stack.Status.ToString()
            });
    }

    [HttpPut("{stackId}")]
    public async Task<ActionResult<StackDetailsDto>> Update(
        string stackId,
        [FromBody] StackConfigurationDto configuration,
        CancellationToken cancellationToken)
    {
        // Validate configuration (allow same ports if editing same stack)
        var validationResult = await _stackConfigurationValidator.ValidateAsync(
            configuration, 
            existingStackId: stackId, 
            cancellationToken: cancellationToken);
        
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult);
        }

        var updatedStack = await _stackService.UpdateAsync(stackId, configuration, cancellationToken);
        return updatedStack is null ? NotFound() : Ok(updatedStack);
    }

    [HttpDelete("{stackId}")]
    public async Task<IActionResult> Delete(string stackId, CancellationToken cancellationToken)
    {
        var deleted = await _stackService.DeleteAsync(stackId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ValidationResultDto>> Validate(
        [FromBody] StackConfigurationDto configuration,
        [FromQuery] string? existingStackId,
        CancellationToken cancellationToken)
    {
        var validationResult = await _stackConfigurationValidator.ValidateAsync(
            configuration, 
            existingStackId: existingStackId,
            cancellationToken: cancellationToken);
        return Ok(validationResult);
    }

    [HttpPost("{stackId}/build")]
    public async Task<ActionResult<BuildStartedResponse>> StartBuild(
        string stackId,
        [FromBody] StackConfigurationDto? configuration,
        CancellationToken cancellationToken)
    {
        var stack = await _stackService.GetAsync(stackId, cancellationToken);
        if (stack is null)
        {
            return NotFound();
        }

        // If no configuration provided, use the existing stack configuration (for rebuilds)
        var buildConfig = configuration ?? stack.Configuration;
        
        var buildStatus = await _buildService.StartAsync(stackId, buildConfig, cancellationToken);
        return Ok(new BuildStartedResponse
        {
            BuildId = buildStatus.BuildId,
            Status = buildStatus.CurrentPhase.ToString()
        });
    }

    [HttpGet("{stackId}/build/status")]
    public async Task<ActionResult<BuildStatusDto>> GetBuildStatus(string stackId, CancellationToken cancellationToken)
    {
        var buildStatus = await _buildService.GetStatusAsync(stackId, cancellationToken);
        return buildStatus is null ? NotFound() : Ok(buildStatus);
    }

    [HttpPost("{stackId}/build/cancel")]
    public async Task<IActionResult> CancelBuild(string stackId, CancellationToken cancellationToken)
    {
        var cancelled = await _buildService.CancelAsync(stackId, cancellationToken);
        return cancelled ? Ok() : NotFound();
    }

    [HttpDelete("{stackId}/build/files")]
    public async Task<ActionResult<CleanupResultDto>> CleanupBuildFiles(string stackId, CancellationToken cancellationToken)
    {
        var freedSpace = await _buildService.CleanupAsync(stackId, cancellationToken);
        return Ok(new CleanupResultDto { FreedSpace = freedSpace });
    }

    [HttpPost("{stackId}/start")]
    public async Task<IActionResult> Start(string stackId, CancellationToken cancellationToken)
    {
        var started = await _stackService.StartAsync(stackId, cancellationToken);
        return started ? Ok() : NotFound();
    }

    [HttpPost("{stackId}/stop")]
    public async Task<IActionResult> Stop(string stackId, CancellationToken cancellationToken)
    {
        var stopped = await _stackService.StopAsync(stackId, cancellationToken);
        return stopped ? Ok() : NotFound();
    }

    [HttpPost("{stackId}/restart")]
    public async Task<IActionResult> Restart(string stackId, CancellationToken cancellationToken)
    {
        var restarted = await _stackService.RestartAsync(stackId, cancellationToken);
        return restarted ? Ok() : NotFound();
    }

    /// <summary>
    /// Get update status for a specific stack
    /// </summary>
    [HttpGet("{stackId}/update-status")]
    public async Task<ActionResult<StackUpdateStatusDto>> GetUpdateStatus(
        string stackId,
        CancellationToken cancellationToken)
    {
        var versionService = HttpContext.RequestServices.GetRequiredService<IStackVersionService>();
        var status = await versionService.GetCachedStatusAsync(stackId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>
    /// Manually trigger update check for a specific stack (bypasses cache)
    /// </summary>
    [HttpPost("{stackId}/check-updates")]
    public async Task<ActionResult<StackUpdateStatusDto>> CheckUpdatesNow(
        string stackId,
        CancellationToken cancellationToken)
    {
        var versionService = HttpContext.RequestServices.GetRequiredService<IStackVersionService>();
        
        try
        {
            var status = await versionService.CheckAndCacheStatusAsync(stackId, cancellationToken);
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update a stack to the latest version (triggers rebuild with existing configuration)
    /// </summary>
    [HttpPost("{stackId}/update")]
    public async Task<ActionResult<BuildStatusDto>> UpdateStack(
        string stackId,
        CancellationToken cancellationToken)
    {
        // Validate stack exists and is not running
        var stack = await _stackService.GetAsync(stackId, cancellationToken);
        if (stack is null)
        {
            return NotFound(new { error = $"Stack {stackId} not found" });
        }

        if (stack.Status == StackStatus.Running)
        {
            return BadRequest(new { error = "Stack must be stopped before updating. Stop the stack and try again." });
        }

        if (stack.Status == StackStatus.Building)
        {
            return BadRequest(new { error = "Stack is currently building. Wait for the build to complete." });
        }

        // Trigger rebuild with existing configuration (configuration: null)
        var buildStatus = await _buildService.StartAsync(stackId, configuration: null, cancellationToken);
        return Ok(buildStatus);
    }

    /// <summary>
    /// Discover existing stacks from filesystem and Docker that are not tracked in the database
    /// </summary>
    [HttpGet("discover")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<DiscoveredStackDto>))]
    public async Task<ActionResult<IReadOnlyList<DiscoveredStackDto>>> DiscoverStacks(
        CancellationToken cancellationToken)
    {
        var discovered = await _stackDiscoveryService.DiscoverStacksAsync(cancellationToken);
        
        // Filter out stacks already in database
        var existingIds = await _stackService.ListAsync(cancellationToken)
            .ContinueWith(t => t.Result.Select(s => s.StackId).ToHashSet(), cancellationToken);
        
        var newStacks = discovered
            .Where(d => !existingIds.Contains(d.StackId))
            .ToList();
        
        return Ok(newStacks);
    }

    /// <summary>
    /// Import a discovered stack into the manager database
    /// </summary>
    [HttpPost("import/{stackId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StackDetailsDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StackDetailsDto>> ImportStack(
        string stackId,
        [FromBody] ImportStackRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var imported = await _stackService.ImportDiscoveredStackAsync(stackId, request, cancellationToken);
            return Ok(imported);
        }
        catch (StackNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (StackConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Initialize SOAP admin account for a stack
    /// </summary>
    [HttpPost("{stackId}/initialize-admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitializeAdminAccount(
        string stackId,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _stackService.InitializeAdminAccountAsync(stackId, cancellationToken);
            return Ok(new { success = true, created, message = created ? "Admin account created successfully" : "Admin account already initialized" });
        }
        catch (StackNotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}
