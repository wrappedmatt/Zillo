using Zillo.Application.DTOs;

namespace Zillo.Application.Services;

/// <summary>
/// Service for managing multi-account access and switching
/// </summary>
public interface IAccountSwitchService
{
    /// <summary>
    /// Get all accounts a user has access to
    /// </summary>
    Task<UserAccountsDto> GetAccessibleAccountsAsync(string supabaseUserId);

    /// <summary>
    /// Switch to a different account (validates access)
    /// </summary>
    Task<SwitchAccountResponse> SwitchAccountAsync(string supabaseUserId, Guid accountId);

    /// <summary>
    /// Check if user has access to an account
    /// </summary>
    Task<bool> HasAccessAsync(string supabaseUserId, Guid accountId);

    /// <summary>
    /// Get user's role in an account
    /// </summary>
    Task<string?> GetRoleAsync(string supabaseUserId, Guid accountId);
}
