using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Zillo.Application.Services;
using Zillo.Application.DTOs;

namespace Zillo.API.Controllers;

/// <summary>
/// Controller for terminal pairing and validation endpoints
/// These endpoints are called from the terminal device and don't require terminal authentication
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/terminal")]
public class TerminalPairingController : ControllerBase
{
    private readonly ITerminalService _terminalService;
    private readonly ILogger<TerminalPairingController> _logger;

    public TerminalPairingController(
        ITerminalService terminalService,
        ILogger<TerminalPairingController> logger)
    {
        _terminalService = terminalService;
        _logger = logger;
    }

    /// <summary>
    /// Pair a terminal using a pairing code
    /// Called from the terminal device during initial setup - no authentication required
    /// </summary>
    [HttpPost("pair")]
    public async Task<IActionResult> PairTerminal([FromBody] PairTerminalRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.PairingCode))
            {
                return BadRequest(new { error = "Pairing code is required" });
            }

            if (string.IsNullOrEmpty(request.TerminalLabel))
            {
                return BadRequest(new { error = "Terminal label is required" });
            }

            var response = await _terminalService.PairTerminalAsync(request);

            _logger.LogInformation("Terminal {TerminalId} paired successfully with code {PairingCode}",
                response.TerminalId, request.PairingCode);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pairing terminal");

            // Return appropriate error codes
            if (ex.Message.Contains("Invalid pairing code"))
            {
                return NotFound(new { error = ex.Message });
            }
            else if (ex.Message.Contains("expired") || ex.Message.Contains("already been used"))
            {
                return BadRequest(new { error = ex.Message });
            }

            return StatusCode(500, new { error = "An error occurred during pairing" });
        }
    }

    /// <summary>
    /// Validate terminal API key
    /// Called from the terminal to verify its API key is still valid
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateApiKey([FromBody] ValidateApiKeyRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ApiKey))
            {
                return BadRequest(new { error = "API key is required" });
            }

            var terminal = await _terminalService.ValidateApiKeyAsync(request.ApiKey);

            if (terminal == null)
            {
                return Unauthorized(new { error = "Invalid API key" });
            }

            return Ok(new
            {
                valid = true,
                terminal_id = terminal.Id,
                account_id = terminal.AccountId,
                terminal_label = terminal.TerminalLabel,
                status = terminal.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request model for validating API key
/// </summary>
public record ValidateApiKeyRequest(string ApiKey);
