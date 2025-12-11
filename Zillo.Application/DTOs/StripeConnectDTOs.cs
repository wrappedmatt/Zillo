namespace Zillo.Application.DTOs;

/// <summary>
/// Response when creating an onboarding link for Stripe Connect
/// </summary>
public record StripeOnboardingLinkResponse(
    string Url,
    DateTime ExpiresAt
);

/// <summary>
/// Current Stripe Connect status for an account
/// </summary>
public record StripeAccountStatusDto(
    string? StripeAccountId,
    string OnboardingStatus,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    DateTime? LastUpdated
);

/// <summary>
/// Request to create a new connected Stripe account
/// </summary>
public record CreateConnectedAccountRequest(
    string? BusinessType = "company", // "company" or "individual"
    string? Country = "NZ"
);

/// <summary>
/// Request for generating an onboarding link
/// </summary>
public record OnboardingLinkRequest(
    string ReturnUrl,
    string RefreshUrl
);
