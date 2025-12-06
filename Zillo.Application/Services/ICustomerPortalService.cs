using Zillo.Application.DTOs;

namespace Zillo.Application.Services;

public interface ICustomerPortalService
{
    /// <summary>
    /// Register a new customer and claim all unclaimed points associated with the card fingerprint
    /// </summary>
    Task<CustomerDto> RegisterCustomerAsync(Guid accountId, string cardFingerprint, string name, string? email, string? phone);

    /// <summary>
    /// Get preview data for signup page (account info, unclaimed points)
    /// </summary>
    Task<CustomerPortalPreviewDto> GetSignupPreviewAsync(string accountSlug, string cardFingerprint);

    /// <summary>
    /// Generate a time-limited portal access token for a customer
    /// </summary>
    Task<string> GeneratePortalTokenAsync(Guid customerId, Guid accountId);

    /// <summary>
    /// Get customer portal data using a portal token
    /// </summary>
    Task<CustomerPortalDto> GetPortalDataAsync(string token);

    /// <summary>
    /// Validate and refresh a portal token if it's close to expiring
    /// </summary>
    Task<bool> ValidatePortalTokenAsync(string token);
}
