using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface IAccountUserRepository
{
    /// <summary>
    /// Get all accounts a user has access to
    /// </summary>
    Task<List<AccountUser>> GetBySupabaseUserIdAsync(string supabaseUserId);

    /// <summary>
    /// Get a specific user-account link
    /// </summary>
    Task<AccountUser?> GetByUserAndAccountAsync(string supabaseUserId, Guid accountId);

    /// <summary>
    /// Get all users of an account
    /// </summary>
    Task<List<AccountUser>> GetByAccountIdAsync(Guid accountId);

    /// <summary>
    /// Check if user has access to an account
    /// </summary>
    Task<bool> HasAccessAsync(string supabaseUserId, Guid accountId);

    /// <summary>
    /// Check if user is owner of an account
    /// </summary>
    Task<bool> IsOwnerAsync(string supabaseUserId, Guid accountId);

    /// <summary>
    /// Create a new user-account link
    /// </summary>
    Task<AccountUser> CreateAsync(AccountUser accountUser);

    /// <summary>
    /// Update an existing user-account link
    /// </summary>
    Task<AccountUser> UpdateAsync(AccountUser accountUser);

    /// <summary>
    /// Delete a user-account link
    /// </summary>
    Task DeleteAsync(Guid id);
}
