using AzerothCoreManager.Core.Contracts;
using AzerothCoreManager.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzerothCoreManager.Api.Controllers;

/// <summary>
/// Character management endpoints for AzerothCore stacks
/// </summary>
[ApiController]
[Route("api/stacks/{stackId}/characters")]
public class CharactersController : ControllerBase
{
    private readonly IAccountManagementService _accountService;

    public CharactersController(IAccountManagementService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    /// Get all characters across all accounts for a stack
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CharacterDto>>> GetAll(
        string stackId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var characters = await _accountService.GetAllCharactersAsync(stackId, cancellationToken);
            return Ok(characters);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to retrieve characters: {ex.Message}" });
        }
    }

    /// <summary>
    /// Create the dedicated AH Bot account and inject Alliance + Horde bot characters directly into the database.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    [HttpPost("ahbot-account")]
    public async Task<ActionResult<AhBotSetupResultDto>> CreateAhBotAccount(
        string stackId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _accountService.CreateAhBotCharactersAsync(stackId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to create AH Bot account: {ex.Message}" });
        }
    }

    /// <summary>
    /// Send a message to a character
    /// </summary>
    [HttpPost("{characterName}/send-message")]
    public async Task<IActionResult> SendMessage(
        string stackId,
        string characterName,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        try
        {
            var success = await _accountService.SendMessageAsync(
                stackId, characterName, request.Message, cancellationToken);

            if (success)
            {
                return Ok(new { success = true, message = $"Message sent to '{characterName}'" });
            }
            else
            {
                return BadRequest(new { success = false, error = "Failed to send message. Character may not be online or doesn't exist." });
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to send message: {ex.Message}" });
        }
    }

    /// <summary>
    /// Send items to a character via in-game mail
    /// </summary>
    [HttpPost("{characterName}/send-items")]
    public async Task<IActionResult> SendItems(
        string stackId,
        string characterName,
        [FromBody] SendItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ItemId <= 0)
        {
            return BadRequest(new { error = "Valid item ID is required" });
        }

        if (request.Count <= 0)
        {
            return BadRequest(new { error = "Item count must be greater than 0" });
        }

        try
        {
            var success = await _accountService.SendItemsAsync(
                stackId, characterName, request.ItemId, request.Count, cancellationToken);

            if (success)
            {
                return Ok(new { success = true, message = $"Items sent to '{characterName}' via mail" });
            }
            else
            {
                return BadRequest(new { success = false, error = "Failed to send items. Character may not exist or item ID is invalid." });
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to send items: {ex.Message}" });
        }
    }

    /// <summary>
    /// Send money to a character via in-game mail
    /// </summary>
    [HttpPost("{characterName}/send-money")]
    public async Task<IActionResult> SendMoney(
        string stackId,
        string characterName,
        [FromBody] SendMoneyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CopperAmount <= 0)
        {
            return BadRequest(new { error = "Amount must be greater than 0" });
        }

        try
        {
            var success = await _accountService.SendMoneyAsync(
                stackId, characterName, request.CopperAmount, cancellationToken);

            if (success)
            {
                var gold = request.CopperAmount / 10000;
                var silver = (request.CopperAmount % 10000) / 100;
                var copper = request.CopperAmount % 100;
                
                return Ok(new 
                { 
                    success = true, 
                    message = $"Money sent to '{characterName}' via mail: {gold}g {silver}s {copper}c" 
                });
            }
            else
            {
                return BadRequest(new { success = false, error = "Failed to send money. Character may not exist." });
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to send money: {ex.Message}" });
        }
    }

    /// <summary>
    /// Kick a player from the server
    /// </summary>
    [HttpPost("{characterName}/kick")]
    public async Task<IActionResult> KickPlayer(
        string stackId,
        string characterName,
        [FromBody] KickPlayerRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reason = request?.Reason ?? string.Empty;
            var success = await _accountService.KickPlayerAsync(
                stackId, characterName, reason, cancellationToken);

            if (success)
            {
                return Ok(new { success = true, message = $"Player '{characterName}' has been kicked" });
            }
            else
            {
                return BadRequest(new { success = false, error = "Failed to kick player. Character may not be online." });
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to kick player: {ex.Message}" });
        }
    }

    /// <summary>
    /// Force a character to be renamed on next login
    /// </summary>
    [HttpPost("{characterName}/rename")]
    public async Task<IActionResult> RenameCharacter(
        string stackId,
        string characterName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _accountService.RenameCharacterAsync(
                stackId, characterName, cancellationToken);

            if (success)
            {
                return Ok(new { success = true, message = $"Character '{characterName}' will be renamed on next login" });
            }
            else
            {
                return BadRequest(new { success = false, error = "Failed to mark character for rename. Character may not exist." });
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to rename character: {ex.Message}" });
        }
    }

    /// <summary>
    /// Force a character to be customized on next login
    /// </summary>
    [HttpPost("{characterName}/customize")]
    public async Task<IActionResult> CustomizeCharacter(
        string stackId,
        string characterName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _accountService.CustomizeCharacterAsync(
                stackId, characterName, cancellationToken);

            if (success)
            {
                return Ok(new { success = true, message = $"Character '{characterName}' will be customized on next login" });
            }
            else
            {
                return BadRequest(new { success = false, error = "Failed to mark character for customization. Character may not exist." });
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to customize character: {ex.Message}" });
        }
    }

    /// <summary>
    /// Set character level
    /// </summary>
    [HttpPost("{characterName}/set-level")]
    public async Task<IActionResult> SetCharacterLevel(
        string stackId,
        string characterName,
        [FromBody] SetCharacterLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Level < 1 || request.Level > 80)
        {
            return BadRequest(new { error = "Level must be between 1 and 80" });
        }

        try
        {
            var success = await _accountService.SetCharacterLevelAsync(
                stackId, characterName, request.Level, cancellationToken);

            if (success)
            {
                return Ok(new { success = true, message = $"Character '{characterName}' level set to {request.Level}" });
            }
            else
            {
                return BadRequest(new { success = false, error = "Failed to set character level. Character may not exist." });
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to set character level: {ex.Message}" });
        }
    }
}
