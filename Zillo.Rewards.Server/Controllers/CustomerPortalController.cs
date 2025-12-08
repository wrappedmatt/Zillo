using Zillo.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Rewards.Server.Controllers;

[ApiController]
[Route("api/customer-portal")]
public class CustomerPortalController : ControllerBase
{
    private readonly ICustomerPortalService _portalService;
    private readonly ILogger<CustomerPortalController> _logger;

    public CustomerPortalController(
        ICustomerPortalService portalService,
        ILogger<CustomerPortalController> logger)
    {
        _portalService = portalService;
        _logger = logger;
    }

    /// <summary>
    /// Get signup preview data (company name, signup bonus, unclaimed points)
    /// Public endpoint - no authentication required
    /// </summary>
    [HttpGet("preview/{slug}")]
    public async Task<IActionResult> GetSignupPreview(string slug, [FromQuery] string fingerprint)
    {
        try
        {
            if (string.IsNullOrEmpty(fingerprint))
            {
                return BadRequest(new { error = "Card fingerprint is required" });
            }

            var preview = await _portalService.GetSignupPreviewAsync(slug, fingerprint);

            return Ok(preview);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Account not found for slug: {Slug}", slug);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting signup preview for slug: {Slug}", slug);
            return StatusCode(500, new { error = "An error occurred while retrieving signup preview" });
        }
    }

    /// <summary>
    /// Register a new customer and claim unclaimed points
    /// Public endpoint - no authentication required
    /// </summary>
    [HttpPost("register/{slug}")]
    public async Task<IActionResult> RegisterCustomer(
        string slug,
        [FromBody] RegisterCustomerRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.CardFingerprint))
            {
                return BadRequest(new { error = "Card fingerprint is required" });
            }

            if (string.IsNullOrEmpty(request.Name))
            {
                return BadRequest(new { error = "Name is required" });
            }

            // Get account by slug to get account ID
            var preview = await _portalService.GetSignupPreviewAsync(slug, request.CardFingerprint);

            // Find account ID from preview (we need to add a method to get account by slug)
            // For now, we'll use a workaround - the preview validates the slug exists
            var accountRepository = HttpContext.RequestServices.GetRequiredService<Zillo.Domain.Interfaces.IAccountRepository>();
            var account = await accountRepository.GetBySlugAsync(slug);

            if (account == null)
            {
                return NotFound(new { error = "Account not found" });
            }

            var customer = await _portalService.RegisterCustomerAsync(
                account.Id,
                request.CardFingerprint,
                request.Name,
                request.Email,
                request.Phone
            );

            // Generate portal token
            var portalToken = await _portalService.GeneratePortalTokenAsync(customer.Id, account.Id);

            _logger.LogInformation("Customer {CustomerId} registered for account {AccountId}", customer.Id, account.Id);

            return Ok(new
            {
                customer,
                portalToken,
                portalUrl = $"/portal/{portalToken}"
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid registration request for slug: {Slug}", slug);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering customer for slug: {Slug}", slug);
            return StatusCode(500, new { error = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Get customer portal data using portal token
    /// Public endpoint - authenticated via portal token
    /// </summary>
    [HttpGet("{token}")]
    public async Task<IActionResult> GetPortalData(string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new { error = "Portal token is required" });
            }

            var portalData = await _portalService.GetPortalDataAsync(token);

            return Ok(portalData);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized portal access attempt with token");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting portal data");
            return StatusCode(500, new { error = "An error occurred while retrieving portal data" });
        }
    }

    /// <summary>
    /// Validate a portal token
    /// Public endpoint - returns true if token is valid and not expired
    /// </summary>
    [HttpGet("validate/{token}")]
    public async Task<IActionResult> ValidateToken(string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new { error = "Portal token is required" });
            }

            var isValid = await _portalService.ValidatePortalTokenAsync(token);

            return Ok(new { valid = isValid });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating portal token");
            return StatusCode(500, new { error = "An error occurred while validating token" });
        }
    }
}

public record RegisterCustomerRequest(
    string CardFingerprint,
    string Name,
    string? Email,
    string? Phone
);
