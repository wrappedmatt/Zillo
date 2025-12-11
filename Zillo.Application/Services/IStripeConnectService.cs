using Zillo.Application.DTOs;

namespace Zillo.Application.Services;

/// <summary>
/// Service for managing Stripe Connect accounts
/// </summary>
public interface IStripeConnectService
{
    /// <summary>
    /// Create a new Stripe Connect Express account for a merchant
    /// </summary>
    /// <param name="accountId">The Zillo account ID</param>
    /// <param name="email">The merchant's email</param>
    /// <param name="country">Country code (default: NZ)</param>
    /// <param name="businessType">Business type: "company" or "individual"</param>
    /// <returns>The Stripe account ID (acct_xxx)</returns>
    Task<string> CreateConnectedAccountAsync(Guid accountId, string email, string country = "NZ", string businessType = "company");

    /// <summary>
    /// Generate an onboarding link for a merchant to complete Stripe Connect setup
    /// </summary>
    /// <param name="accountId">The Zillo account ID</param>
    /// <param name="returnUrl">URL to redirect after successful onboarding</param>
    /// <param name="refreshUrl">URL to redirect if the link expires</param>
    /// <returns>Onboarding link details</returns>
    Task<StripeOnboardingLinkResponse> CreateOnboardingLinkAsync(Guid accountId, string returnUrl, string refreshUrl);

    /// <summary>
    /// Generate a Stripe Express dashboard login link for a merchant
    /// </summary>
    /// <param name="accountId">The Zillo account ID</param>
    /// <returns>Dashboard login URL</returns>
    Task<string> CreateDashboardLinkAsync(Guid accountId);

    /// <summary>
    /// Get the current Stripe Connect status for an account
    /// </summary>
    /// <param name="accountId">The Zillo account ID</param>
    /// <returns>Current status</returns>
    Task<StripeAccountStatusDto> GetAccountStatusAsync(Guid accountId);

    /// <summary>
    /// Update account status from Stripe webhook (account.updated event)
    /// </summary>
    /// <param name="stripeAccountId">The Stripe account ID</param>
    /// <param name="chargesEnabled">Whether charges are enabled</param>
    /// <param name="payoutsEnabled">Whether payouts are enabled</param>
    Task UpdateAccountFromWebhookAsync(string stripeAccountId, bool chargesEnabled, bool payoutsEnabled);

    /// <summary>
    /// Check if an account can accept payments via Stripe Connect
    /// </summary>
    /// <param name="accountId">The Zillo account ID</param>
    /// <returns>True if the account can accept payments</returns>
    Task<bool> CanAcceptPaymentsAsync(Guid accountId);

    /// <summary>
    /// Handle account deauthorization (disconnect from platform)
    /// </summary>
    /// <param name="stripeAccountId">The Stripe account ID</param>
    Task HandleAccountDeauthorizedAsync(string stripeAccountId);
}
