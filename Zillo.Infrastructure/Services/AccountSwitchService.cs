using Microsoft.Extensions.Logging;
using Zillo.Application.DTOs;
using Zillo.Application.Services;
using Zillo.Domain.Interfaces;

namespace Zillo.Infrastructure.Services;

public class AccountSwitchService : IAccountSwitchService
{
    private readonly IAccountUserRepository _accountUserRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<AccountSwitchService> _logger;

    public AccountSwitchService(
        IAccountUserRepository accountUserRepository,
        IAccountRepository accountRepository,
        ILogger<AccountSwitchService> logger)
    {
        _accountUserRepository = accountUserRepository;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    public async Task<UserAccountsDto> GetAccessibleAccountsAsync(string supabaseUserId)
    {
        _logger.LogInformation("Getting accessible accounts for user {UserId}", supabaseUserId);

        var memberships = await _accountUserRepository.GetBySupabaseUserIdAsync(supabaseUserId);

        if (!memberships.Any())
        {
            _logger.LogWarning("No account memberships found for user {UserId}", supabaseUserId);
            return new UserAccountsDto(supabaseUserId, string.Empty, new List<AccountSummaryDto>(), null);
        }

        var accounts = new List<AccountSummaryDto>();
        string? email = null;

        foreach (var membership in memberships)
        {
            var account = await _accountRepository.GetByIdAsync(membership.AccountId);
            if (account != null)
            {
                accounts.Add(new AccountSummaryDto(
                    account.Id,
                    account.CompanyName,
                    account.Slug,
                    membership.Role,
                    account.StripeOnboardingStatus,
                    account.StripeChargesEnabled
                ));

                email ??= membership.Email;
            }
        }

        _logger.LogInformation("Found {Count} accessible accounts for user {UserId}", accounts.Count, supabaseUserId);

        // Default to first account if user has accounts
        var defaultAccountId = accounts.FirstOrDefault()?.Id;

        return new UserAccountsDto(supabaseUserId, email ?? string.Empty, accounts, defaultAccountId);
    }

    public async Task<SwitchAccountResponse> SwitchAccountAsync(string supabaseUserId, Guid accountId)
    {
        _logger.LogInformation("User {UserId} switching to account {AccountId}", supabaseUserId, accountId);

        var membership = await _accountUserRepository.GetByUserAndAccountAsync(supabaseUserId, accountId);

        if (membership == null)
        {
            _logger.LogWarning("User {UserId} does not have access to account {AccountId}", supabaseUserId, accountId);
            throw new UnauthorizedAccessException($"User does not have access to account {accountId}");
        }

        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
        {
            _logger.LogError("Account {AccountId} not found", accountId);
            throw new InvalidOperationException($"Account {accountId} not found");
        }

        _logger.LogInformation("User {UserId} switched to account {AccountId} ({CompanyName}) with role {Role}",
            supabaseUserId, accountId, account.CompanyName, membership.Role);

        return new SwitchAccountResponse(
            account.Id,
            account.CompanyName,
            account.Slug,
            membership.Role
        );
    }

    public async Task<bool> HasAccessAsync(string supabaseUserId, Guid accountId)
    {
        return await _accountUserRepository.HasAccessAsync(supabaseUserId, accountId);
    }

    public async Task<string?> GetRoleAsync(string supabaseUserId, Guid accountId)
    {
        var membership = await _accountUserRepository.GetByUserAndAccountAsync(supabaseUserId, accountId);
        return membership?.Role;
    }
}
