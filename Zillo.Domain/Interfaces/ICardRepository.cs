using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface ICardRepository
{
    Task<Card?> GetByFingerprintAsync(string fingerprint);
    Task<Card?> GetByIdAsync(Guid id);
    Task<IEnumerable<Card>> GetByCustomerIdAsync(Guid customerId);
    Task<Card> CreateAsync(Card card);
    Task<Card> UpdateAsync(Card card);
    Task DeleteAsync(Guid id);
}
