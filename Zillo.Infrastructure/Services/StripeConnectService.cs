using Microsoft.Extensions.Logging;
using Stripe;
using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Interfaces;

namespace Zillo.Infrastructure.Services;

/// <summary>
/// Service for managing Stripe Connect accounts
/// </summary>
public class StripeConnectService : IStripeConnectService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<StripeConnectService> _logger;

    public StripeConnectService(
        IAccountRepository accountRepository,
        ILogger<StripeConnectService> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    public async Task<string> CreateConnectedAccountAsync(
        Guid accountId,
        string email,
        string country = "NZ",
        string businessType = "company")
    {
        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
        {
            throw new InvalidOperationException($"Account not found: {accountId}");
        }

        // If account already has a Stripe account, return it
        if (!string.IsNullOrEmpty(account.StripeAccountId))
        {
            _logger.LogInformation("Account {AccountId} already has Stripe account {StripeAccountId}",
                accountId, account.StripeAccountId);
            return account.StripeAccountId;
        }

        _logger.LogInformation("Creating Stripe Connect account for {AccountId}, email: {Email}, country: {Country}",
            accountId, email, country);

        var options = new AccountCreateOptions
        {
            Type = "express",
            Country = country,
            Email = email,
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
            },
            BusinessType = businessType,
            Metadata = new Dictionary<string, string>
            {
                { "zillo_account_id", accountId.ToString() }
            }
        };

        var service = new AccountService();
        var stripeAccount = await service.CreateAsync(options);

        _logger.LogInformation("Created Stripe account {StripeAccountId} for {AccountId}",
            stripeAccount.Id, accountId);

        // Update our account with the Stripe account ID
        account.StripeAccountId = stripeAccount.Id;
        account.StripeOnboardingStatus = "pending";
        account.StripeAccountUpdatedAt = DateTime.UtcNow;
        await _accountRepository.UpdateAsync(account);

        return stripeAccount.Id;
    }

    public async Task<StripeOnboardingLinkResponse> CreateOnboardingLinkAsync(
        Guid accountId,
        string returnUrl,
        string refreshUrl)
    {
        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
        {
            throw new InvalidOperationException($"Account not found: {accountId}");
        }

        if (string.IsNullOrEmpty(account.StripeAccountId))
        {
            throw new InvalidOperationException(
                "Stripe account not created. Call CreateConnectedAccount first.");
        }

        _logger.LogInformation("Creating onboarding link for {AccountId}, Stripe account {StripeAccountId}",
            accountId, account.StripeAccountId);

        var options = new AccountLinkCreateOptions
        {
            Account = account.StripeAccountId,
            RefreshUrl = refreshUrl,
            ReturnUrl = returnUrl,
            Type = "account_onboarding",
        };

        var service = new AccountLinkService();
        var accountLink = await service.CreateAsync(options);

        return new StripeOnboardingLinkResponse(
            accountLink.Url,
            accountLink.ExpiresAt
        );
    }

    public async Task<string> CreateDashboardLinkAsync(Guid accountId)
    {
        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
        {
            throw new InvalidOperationException($"Account not found: {accountId}");
        }

        if (string.IsNullOrEmpty(account.StripeAccountId))
        {
            throw new InvalidOperationException("Stripe account not configured.");
        }

        _logger.LogInformation("Creating dashboard link for {AccountId}", accountId);

        var service = new AccountLoginLinkService();
        var loginLink = await service.CreateAsync(account.StripeAccountId);

        return loginLink.Url;
    }

    public async Task<StripeAccountStatusDto> GetAccountStatusAsync(Guid accountId)
    {
        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
        {
            throw new InvalidOperationException($"Account not found: {accountId}");
        }

        return new StripeAccountStatusDto(
            account.StripeAccountId,
            account.StripeOnboardingStatus,
            account.StripeChargesEnabled,
            account.StripePayoutsEnabled,
            account.StripeAccountUpdatedAt
        );
    }

    public async Task UpdateAccountFromWebhookAsync(
        string stripeAccountId,
        bool chargesEnabled,
        bool payoutsEnabled)
    {
        _logger.LogInformation(
            "Updating account from webhook: {StripeAccountId}, charges: {ChargesEnabled}, payouts: {PayoutsEnabled}",
            stripeAccountId, chargesEnabled, payoutsEnabled);

        var account = await _accountRepository.GetByStripeAccountIdAsync(stripeAccountId);
        if (account == null)
        {
            _logger.LogWarning("Account not found for Stripe account {StripeAccountId}", stripeAccountId);
            return;
        }

        account.StripeChargesEnabled = chargesEnabled;
        account.StripePayoutsEnabled = payoutsEnabled;
        account.StripeAccountUpdatedAt = DateTime.UtcNow;

        // Update onboarding status based on capabilities
        if (chargesEnabled && payoutsEnabled)
        {
            account.StripeOnboardingStatus = "complete";
        }
        else if (chargesEnabled || payoutsEnabled)
        {
            // Partially enabled - might have restrictions
            account.StripeOnboardingStatus = "restricted";
        }
        // If neither enabled, keep existing status (pending)

        await _accountRepository.UpdateAsync(account);

        _logger.LogInformation("Updated account {AccountId} Stripe status to {Status}",
            account.Id, account.StripeOnboardingStatus);
    }

    public async Task<bool> CanAcceptPaymentsAsync(Guid accountId)
    {
        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
        {
            return false;
        }

        return !string.IsNullOrEmpty(account.StripeAccountId) && account.StripeChargesEnabled;
    }

    public async Task HandleAccountDeauthorizedAsync(string stripeAccountId)
    {
        _logger.LogWarning(
            "Handling deauthorization for Stripe account {StripeAccountId}",
            stripeAccountId);

        var account = await _accountRepository.GetByStripeAccountIdAsync(stripeAccountId);
        if (account == null)
        {
            _logger.LogWarning("Account not found for Stripe account {StripeAccountId}", stripeAccountId);
            return;
        }

        // Mark account as deauthorized - keep the ID for reference but disable capabilities
        account.StripeOnboardingStatus = "deauthorized";
        account.StripeChargesEnabled = false;
        account.StripePayoutsEnabled = false;
        account.StripeAccountUpdatedAt = DateTime.UtcNow;

        await _accountRepository.UpdateAsync(account);

        _logger.LogWarning(
            "Account {AccountId} marked as deauthorized - Stripe account disconnected",
            account.Id);
    }
}
