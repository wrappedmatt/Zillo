using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<IEnumerable<Transaction>> GetByCustomerIdAsync(Guid customerId);
    Task<Transaction> CreateAsync(Transaction transaction);
}
