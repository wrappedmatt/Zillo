using System.Security.Cryptography;
using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Dashboard.Server.Controllers;

/// <summary>
/// Controller for managing external locations - Stripe Terminal Location IDs from partner platforms.
/// Used for the "external" terminal integration mode where a partner (e.g., Lightspeed)
/// deploys the Zillo app to their merchants' terminals.
/// </summary>
[ApiController]
[Route("api/external-locations")]
public class ExternalLocationsController : ControllerBase
{
    private readonly IExternalLocationRepository _externalLocationRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<ExternalLocationsController> _logger;

    public ExternalLocationsController(
        IExternalLocationRepository externalLocationRepository,
        IAccountRepository accountRepository,
        ILogger<ExternalLocationsController> logger)
    {
        _externalLocationRepository = externalLocationRepository;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all external locations for the current account
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetExternalLocations()
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var locations = await _externalLocationRepository.GetByAccountIdAsync(account.Id);

            return Ok(locations.Select(l => new
            {
                id = l.Id,
                stripeLocationId = l.StripeLocationId,
                platformName = l.PlatformName,
                label = l.Label,
                pairingCode = l.PairingCode,
                isActive = l.IsActive,
                lastSeenAt = l.LastSeenAt,
                createdAt = l.CreatedAt,
                updatedAt = l.UpdatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting external locations");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific external location
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetExternalLocation(Guid id)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var location = await _externalLocationRepository.GetByIdAsync(id);
            if (location == null || location.AccountId != account.Id)
                return NotFound(new { error = "External location not found" });

            return Ok(new
            {
                id = location.Id,
                stripeLocationId = location.StripeLocationId,
                platformName = location.PlatformName,
                label = location.Label,
                pairingCode = location.PairingCode,
                isActive = location.IsActive,
                lastSeenAt = location.LastSeenAt,
                createdAt = location.CreatedAt,
                updatedAt = location.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting external location {LocationId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Link a new external Stripe Terminal Location ID to this account
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateExternalLocation([FromBody] CreateExternalLocationRequest request)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            // Validate the request
            if (string.IsNullOrWhiteSpace(request.StripeLocationId))
                return BadRequest(new { error = "Stripe location ID is required" });

            // Check if this location ID is already linked to any account
            var existing = await _externalLocationRepository.GetByStripeLocationIdAsync(request.StripeLocationId);
            if (existing != null)
            {
                if (existing.AccountId == account.Id)
                    return BadRequest(new { error = "This location is already linked to your account" });
                else
                    return BadRequest(new { error = "This location is already linked to another account" });
            }

            // Generate unique pairing code
            var pairingCode = await GenerateUniquePairingCodeAsync();

            var location = new ExternalLocation
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                StripeLocationId = request.StripeLocationId.Trim(),
                PlatformName = request.PlatformName?.Trim(),
                Label = request.Label?.Trim(),
                PairingCode = pairingCode,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _externalLocationRepository.CreateAsync(location);

            _logger.LogInformation("External location {StripeLocationId} linked to account {AccountId} with pairing code {PairingCode}",
                created.StripeLocationId, account.Id, created.PairingCode);

            return Ok(new
            {
                id = created.Id,
                stripeLocationId = created.StripeLocationId,
                platformName = created.PlatformName,
                label = created.Label,
                pairingCode = created.PairingCode,
                isActive = created.IsActive,
                lastSeenAt = created.LastSeenAt,
                createdAt = created.CreatedAt,
                updatedAt = created.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating external location");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an external location
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExternalLocation(Guid id, [FromBody] UpdateExternalLocationRequest request)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var location = await _externalLocationRepository.GetByIdAsync(id);
            if (location == null || location.AccountId != account.Id)
                return NotFound(new { error = "External location not found" });

            // If changing the Stripe location ID, check for duplicates
            if (!string.IsNullOrWhiteSpace(request.StripeLocationId) &&
                request.StripeLocationId.Trim() != location.StripeLocationId)
            {
                var existing = await _externalLocationRepository.GetByStripeLocationIdAsync(request.StripeLocationId.Trim());
                if (existing != null && existing.Id != id)
                    return BadRequest(new { error = "This location ID is already in use" });

                location.StripeLocationId = request.StripeLocationId.Trim();
            }

            if (request.PlatformName != null)
                location.PlatformName = request.PlatformName.Trim();

            if (request.Label != null)
                location.Label = request.Label.Trim();

            if (request.IsActive.HasValue)
                location.IsActive = request.IsActive.Value;

            location.UpdatedAt = DateTime.UtcNow;

            var updated = await _externalLocationRepository.UpdateAsync(location);

            _logger.LogInformation("External location {LocationId} updated", id);

            return Ok(new
            {
                id = updated.Id,
                stripeLocationId = updated.StripeLocationId,
                platformName = updated.PlatformName,
                label = updated.Label,
                pairingCode = updated.PairingCode,
                isActive = updated.IsActive,
                lastSeenAt = updated.LastSeenAt,
                createdAt = updated.CreatedAt,
                updatedAt = updated.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating external location {LocationId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Regenerate the pairing code for an external location
    /// </summary>
    [HttpPost("{id}/regenerate-pairing-code")]
    public async Task<IActionResult> RegeneratePairingCode(Guid id)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var location = await _externalLocationRepository.GetByIdAsync(id);
            if (location == null || location.AccountId != account.Id)
                return NotFound(new { error = "External location not found" });

            // Generate new unique pairing code
            var newPairingCode = await GenerateUniquePairingCodeAsync();
            location.PairingCode = newPairingCode;
            location.UpdatedAt = DateTime.UtcNow;

            var updated = await _externalLocationRepository.UpdateAsync(location);

            _logger.LogInformation("Pairing code regenerated for external location {LocationId}", id);

            return Ok(new
            {
                id = updated.Id,
                stripeLocationId = updated.StripeLocationId,
                platformName = updated.PlatformName,
                label = updated.Label,
                pairingCode = updated.PairingCode,
                isActive = updated.IsActive,
                lastSeenAt = updated.LastSeenAt,
                createdAt = updated.CreatedAt,
                updatedAt = updated.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating pairing code for external location {LocationId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete (unlink) an external location
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExternalLocation(Guid id)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var location = await _externalLocationRepository.GetByIdAsync(id);
            if (location == null || location.AccountId != account.Id)
                return NotFound(new { error = "External location not found" });

            await _externalLocationRepository.DeleteAsync(id);

            _logger.LogInformation("External location {StripeLocationId} unlinked from account {AccountId}",
                location.StripeLocationId, account.Id);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting external location {LocationId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Transfer an external location to another Zillo account using an invite code.
    /// The receiving account must have generated an invite code first.
    /// </summary>
    [HttpPost("{id}/transfer")]
    public async Task<IActionResult> TransferExternalLocation(Guid id, [FromBody] TransferExternalLocationRequest request)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var location = await _externalLocationRepository.GetByIdAsync(id);
            if (location == null || location.AccountId != account.Id)
                return NotFound(new { error = "External location not found" });

            // Find the target account by its slug
            if (string.IsNullOrWhiteSpace(request.TargetAccountSlug))
                return BadRequest(new { error = "Target account slug is required" });

            var targetAccount = await _accountRepository.GetBySlugAsync(request.TargetAccountSlug.Trim().ToLower());
            if (targetAccount == null)
                return NotFound(new { error = "Target account not found. Please check the account slug." });

            if (targetAccount.Id == account.Id)
                return BadRequest(new { error = "Cannot transfer to the same account" });

            // Transfer the external location
            location.AccountId = targetAccount.Id;
            location.UpdatedAt = DateTime.UtcNow;

            // Generate a new pairing code for the new owner
            location.PairingCode = await GenerateUniquePairingCodeAsync();

            var updated = await _externalLocationRepository.UpdateAsync(location);

            _logger.LogInformation("External location {LocationId} transferred from account {FromAccountId} to {ToAccountId}",
                id, account.Id, targetAccount.Id);

            return Ok(new
            {
                success = true,
                message = $"External location transferred to {targetAccount.CompanyName ?? targetAccount.Slug}",
                newPairingCode = updated.PairingCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring external location {LocationId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<Account?> GetAccountFromToken()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var token = authHeader["Bearer ".Length..];

        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var supabaseUserId = jwtToken.Subject;

            if (string.IsNullOrEmpty(supabaseUserId))
                return null;

            return await _accountRepository.GetBySupabaseUserIdAsync(supabaseUserId);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> GenerateUniquePairingCodeAsync()
    {
        const int maxAttempts = 100;
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excludes confusing chars: I, O, 0, 1

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GeneratePairingCode(chars, 6);

            // Check if this code already exists
            var existingLocation = await _externalLocationRepository.GetByPairingCodeAsync(code);
            if (existingLocation == null)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique pairing code. Please try again.");
    }

    private static string GeneratePairingCode(string chars, int length)
    {
        var randomBytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(result);
    }
}

public record CreateExternalLocationRequest(
    string StripeLocationId,
    string? PlatformName,
    string? Label
);

public record UpdateExternalLocationRequest(
    string? StripeLocationId,
    string? PlatformName,
    string? Label,
    bool? IsActive
);

public record TransferExternalLocationRequest(
    string TargetAccountSlug
);
