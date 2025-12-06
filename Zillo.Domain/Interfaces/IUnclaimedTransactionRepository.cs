using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface IUnclaimedTransactionRepository
{
    Task<UnclaimedTransaction> CreateAsync(UnclaimedTransaction transaction);
    Task<IEnumerable<UnclaimedTransaction>> GetByCardFingerprintAsync(string fingerprint, Guid accountId);
    Task<IEnumerable<UnclaimedTransaction>> GetUnclaimedByAccountAsync(Guid accountId);
    Task<UnclaimedTransaction> UpdateAsync(UnclaimedTransaction transaction);
    Task<int> GetTotalUnclaimedPointsByFingerprintAsync(string fingerprint, Guid accountId);
    Task<long> GetTotalUnclaimedCashbackByFingerprintAsync(string fingerprint, Guid accountId);
}
