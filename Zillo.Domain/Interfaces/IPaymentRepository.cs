using Zillo.Domain.Entities;

namespace Zillo.Domain.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id);
    Task<Payment?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId);
    Task<IEnumerable<Payment>> GetByAccountIdAsync(Guid accountId);
    Task<IEnumerable<Payment>> GetByCustomerIdAsync(Guid customerId);
    Task<IEnumerable<Payment>> GetByTerminalIdAsync(string terminalId);
    Task<Payment> CreateAsync(Payment payment);
    Task<Payment> UpdateAsync(Payment payment);
    Task DeleteAsync(Guid id);
}
