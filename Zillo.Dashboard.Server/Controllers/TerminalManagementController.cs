using Microsoft.AspNetCore.Mvc;
using Zillo.Application.Services;
using Zillo.Application.DTOs;
using System.Security.Claims;

namespace Zillo.Dashboard.Server.Controllers;

/// <summary>
/// Controller for terminal registration and management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TerminalManagementController : ControllerBase
{
    private readonly ITerminalService _terminalService;
    private readonly ILogger<TerminalManagementController> _logger;

    public TerminalManagementController(
        ITerminalService terminalService,
        ILogger<TerminalManagementController> logger)
    {
        _terminalService = terminalService;
        _logger = logger;
    }

    /// <summary>
    /// Generate a pairing code for terminal registration
    /// Requires authentication - called from dashboard
    /// </summary>
    [HttpPost("generate-pairing-code")]
    public async Task<IActionResult> GeneratePairingCode([FromBody] GeneratePairingCodeRequest request)
    {
        try
        {
            // Get accountId from authenticated user
            // TODO: Replace with proper authentication when implemented
            var accountIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(accountIdClaim, out var accountId))
            {
                // For testing, allow accountId to be passed in request body
                if (!Guid.TryParse(Request.Headers["X-Account-Id"].ToString(), out accountId))
                {
                    return Unauthorized(new { error = "Account authentication required" });
                }
            }

            var response = await _terminalService.GeneratePairingCodeAsync(accountId, request.TerminalLabel);

            _logger.LogInformation("Generated pairing code {PairingCode} for account {AccountId}",
                response.PairingCode, accountId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pairing code");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all terminals for the authenticated account
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTerminals()
    {
        try
        {
            // Get accountId from authenticated user
            var accountIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(accountIdClaim, out var accountId))
            {
                // For testing, allow accountId from header
                if (!Guid.TryParse(Request.Headers["X-Account-Id"].ToString(), out accountId))
                {
                    return Unauthorized(new { error = "Account authentication required" });
                }
            }

            var terminals = await _terminalService.GetTerminalsByAccountAsync(accountId);

            return Ok(terminals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving terminals");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific terminal by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTerminal(Guid id)
    {
        try
        {
            // Get accountId from authenticated user
            var accountIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(accountIdClaim, out var accountId))
            {
                // For testing, allow accountId from header
                if (!Guid.TryParse(Request.Headers["X-Account-Id"].ToString(), out accountId))
                {
                    return Unauthorized(new { error = "Account authentication required" });
                }
            }

            var terminal = await _terminalService.GetTerminalByIdAsync(id, accountId);

            if (terminal == null)
            {
                return NotFound(new { error = "Terminal not found" });
            }

            return Ok(terminal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving terminal {TerminalId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update terminal settings
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTerminal(Guid id, [FromBody] UpdateTerminalRequest request)
    {
        try
        {
            // Get accountId from authenticated user
            var accountIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(accountIdClaim, out var accountId))
            {
                // For testing, allow accountId from header
                if (!Guid.TryParse(Request.Headers["X-Account-Id"].ToString(), out accountId))
                {
                    return Unauthorized(new { error = "Account authentication required" });
                }
            }

            var terminal = await _terminalService.UpdateTerminalAsync(id, request, accountId);

            _logger.LogInformation("Updated terminal {TerminalId}", id);

            return Ok(terminal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating terminal {TerminalId}", id);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new { error = ex.Message });
            }

            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Revoke/deactivate a terminal
    /// </summary>
    [HttpPost("{id}/revoke")]
    public async Task<IActionResult> RevokeTerminal(Guid id)
    {
        try
        {
            // Get accountId from authenticated user
            var accountIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(accountIdClaim, out var accountId))
            {
                // For testing, allow accountId from header
                if (!Guid.TryParse(Request.Headers["X-Account-Id"].ToString(), out accountId))
                {
                    return Unauthorized(new { error = "Account authentication required" });
                }
            }

            await _terminalService.RevokeTerminalAsync(id, accountId);

            _logger.LogInformation("Revoked terminal {TerminalId}", id);

            return Ok(new { success = true, message = "Terminal revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking terminal {TerminalId}", id);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new { error = ex.Message });
            }

            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a terminal permanently
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTerminal(Guid id)
    {
        try
        {
            // Get accountId from authenticated user
            var accountIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(accountIdClaim, out var accountId))
            {
                // For testing, allow accountId from header
                if (!Guid.TryParse(Request.Headers["X-Account-Id"].ToString(), out accountId))
                {
                    return Unauthorized(new { error = "Account authentication required" });
                }
            }

            await _terminalService.DeleteTerminalAsync(id, accountId);

            _logger.LogInformation("Deleted terminal {TerminalId}", id);

            return Ok(new { success = true, message = "Terminal deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting terminal {TerminalId}", id);

            if (ex.Message.Contains("not found"))
            {
                return NotFound(new { error = ex.Message });
            }

            return StatusCode(500, new { error = ex.Message });
        }
    }
}
