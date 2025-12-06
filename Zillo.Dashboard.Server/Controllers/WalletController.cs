using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/wallet")]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly ICustomerPortalService _portalService;
    private readonly ILogger<WalletController> _logger;

    public WalletController(
        IWalletService walletService,
        ICustomerPortalService portalService,
        ILogger<WalletController> logger)
    {
        _walletService = walletService;
        _portalService = portalService;
        _logger = logger;
    }

    #region Public Endpoints (called from frontend)

    /// <summary>
    /// Generate and download Apple Wallet pass for a customer
    /// </summary>
    [HttpGet("apple/pass/{token}")]
    public async Task<IActionResult> GetApplePass(string token)
    {
        try
        {
            // Validate the portal token and get customer data
            var portalData = await _portalService.GetPortalDataAsync(token);

            var passData = await _walletService.GenerateApplePassAsync(portalData.CustomerId);

            return File(passData, "application/vnd.apple.pkpass", "loyalty.pkpass");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized Apple pass request");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Apple pass");
            return StatusCode(500, new { error = "Failed to generate pass" });
        }
    }

    /// <summary>
    /// Get Google Wallet save URL for a customer
    /// </summary>
    [HttpGet("google/pass/{token}")]
    public async Task<IActionResult> GetGoogleWalletUrl(string token)
    {
        try
        {
            // Validate the portal token and get customer data
            var portalData = await _portalService.GetPortalDataAsync(token);

            var saveUrl = await _walletService.GetGoogleWalletSaveUrlAsync(portalData.CustomerId);

            return Ok(new { saveUrl });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized Google Wallet request");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Google Wallet URL");
            return StatusCode(500, new { error = "Failed to generate save URL" });
        }
    }

    #endregion

    #region Apple Wallet Web Service Endpoints (called by Apple)

    /// <summary>
    /// Register a device to receive push notifications for a pass
    /// POST /api/wallet/apple/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}
    /// </summary>
    [HttpPost("apple/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}")]
    public async Task<IActionResult> RegisterDevice(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        string serialNumber,
        [FromBody] AppleDeviceRegistrationRequest request)
    {
        try
        {
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            var authToken = authHeader?.Replace("ApplePass ", "") ?? "";

            await _walletService.RegisterAppleDeviceAsync(
                deviceLibraryIdentifier,
                request.PushToken,
                passTypeIdentifier,
                serialNumber,
                authToken);

            // Return 201 Created for new registration, 200 OK if already registered
            return StatusCode(201);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering Apple device");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Unregister a device from receiving push notifications for a pass
    /// DELETE /api/wallet/apple/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}
    /// </summary>
    [HttpDelete("apple/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}")]
    public async Task<IActionResult> UnregisterDevice(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        string serialNumber)
    {
        try
        {
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            var authToken = authHeader?.Replace("ApplePass ", "") ?? "";

            await _walletService.UnregisterAppleDeviceAsync(
                deviceLibraryIdentifier,
                passTypeIdentifier,
                serialNumber,
                authToken);

            return Ok();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering Apple device");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Get the serial numbers of passes registered for a device
    /// GET /api/wallet/apple/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}
    /// </summary>
    [HttpGet("apple/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}")]
    public async Task<IActionResult> GetRegisteredPasses(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        [FromQuery] string? passesUpdatedSince)
    {
        try
        {
            var serialNumbers = await _walletService.GetSerialNumbersForAppleDeviceAsync(
                deviceLibraryIdentifier,
                passTypeIdentifier);

            var serialNumberList = serialNumbers.ToList();

            if (!serialNumberList.Any())
            {
                return NoContent();
            }

            return Ok(new AppleSerialNumbersResponse(
                serialNumberList.ToArray(),
                DateTime.UtcNow.ToString("o")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting registered passes");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Get the latest version of a pass
    /// GET /api/wallet/apple/v1/passes/{passTypeIdentifier}/{serialNumber}
    /// </summary>
    [HttpGet("apple/v1/passes/{passTypeIdentifier}/{serialNumber}")]
    public async Task<IActionResult> GetLatestPass(
        string passTypeIdentifier,
        string serialNumber)
    {
        try
        {
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            var authToken = authHeader?.Replace("ApplePass ", "") ?? "";

            var passData = await _walletService.GetLatestApplePassAsync(serialNumber, authToken);

            // Set Last-Modified header for caching
            Response.Headers.LastModified = DateTime.UtcNow.ToString("R");

            return File(passData, "application/vnd.apple.pkpass");
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest pass");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Log messages from Wallet (for debugging)
    /// POST /api/wallet/apple/v1/log
    /// </summary>
    [HttpPost("apple/v1/log")]
    public IActionResult Log([FromBody] object logData)
    {
        _logger.LogInformation("Apple Wallet log: {LogData}", logData);
        return Ok();
    }

    #endregion

    #region Admin Endpoints (called from dashboard)

    /// <summary>
    /// Update wallet pass for a customer and send push notifications
    /// POST /api/wallet/update-pass/{customerId}
    /// </summary>
    [HttpPost("update-pass/{customerId}")]
    public async Task<IActionResult> UpdateWalletPass(Guid customerId)
    {
        try
        {
            // Send balance update notifications (works for both Apple and Google Wallet)
            await _walletService.SendBalanceUpdateNotificationsAsync(customerId);

            _logger.LogInformation("Wallet pass updated for customer {CustomerId}", customerId);
            return Ok(new { message = "Wallet pass updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating wallet pass for customer {CustomerId}", customerId);
            return StatusCode(500, new { error = "Failed to update wallet pass" });
        }
    }

    #endregion
}
