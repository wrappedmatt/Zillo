using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Supabase;

namespace Zillo.Infrastructure.Repositories;

public class UnclaimedTransactionRepository : IUnclaimedTransactionRepository
{
    private readonly Client _supabase;

    public UnclaimedTransactionRepository(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<UnclaimedTransaction> CreateAsync(UnclaimedTransaction transaction)
    {
        var response = await _supabase
            .From<UnclaimedTransaction>()
            .Insert(transaction);

        return response.Models.First();
    }

    public async Task<IEnumerable<UnclaimedTransaction>> GetByCardFingerprintAsync(string fingerprint, Guid accountId)
    {
        var response = await _supabase
            .From<UnclaimedTransaction>()
            .Where(t => t.CardFingerprint == fingerprint)
            .Where(t => t.AccountId == accountId)
            .Filter("claimed_at", Postgrest.Constants.Operator.Is, "null")
            .Order(t => t.CreatedAt, Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models;
    }

    public async Task<IEnumerable<UnclaimedTransaction>> GetUnclaimedByAccountAsync(Guid accountId)
    {
        var response = await _supabase
            .From<UnclaimedTransaction>()
            .Where(t => t.AccountId == accountId)
            .Filter("claimed_at", Postgrest.Constants.Operator.Is, "null")
            .Order(t => t.CreatedAt, Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models;
    }

    public async Task<UnclaimedTransaction> UpdateAsync(UnclaimedTransaction transaction)
    {
        var response = await _supabase
            .From<UnclaimedTransaction>()
            .Update(transaction);

        return response.Models.First();
    }

    public async Task<int> GetTotalUnclaimedPointsByFingerprintAsync(string fingerprint, Guid accountId)
    {
        var transactions = await GetByCardFingerprintAsync(fingerprint, accountId);
        return transactions.Sum(t => t.Points);
    }

    public async Task<long> GetTotalUnclaimedCashbackByFingerprintAsync(string fingerprint, Guid accountId)
    {
        var transactions = await GetByCardFingerprintAsync(fingerprint, accountId);
        return transactions.Sum(t => t.CashbackAmount);
    }
}
