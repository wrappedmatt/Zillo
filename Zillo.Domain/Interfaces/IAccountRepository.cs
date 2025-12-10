using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id);
    Task<Account?> GetBySupabaseUserIdAsync(string supabaseUserId);
    Task<Account?> GetByEmailAsync(string email);
    Task<Account?> GetBySlugAsync(string slug);
    Task<Account?> GetByStripeAccountIdAsync(string stripeAccountId);
    Task<List<Account>> GetAllAsync();
    Task<Account> CreateAsync(Account account);
    Task<Account> UpdateAsync(Account account);
}
