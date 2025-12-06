using Zillo.Application.DTOs;

namespace Zillo.Application.Services;

public interface IPaymentService
{
    Task<PaymentDto?> GetPaymentByIdAsync(Guid id, Guid accountId);
    Task<PaymentDto?> GetPaymentByStripePaymentIntentIdAsync(string stripePaymentIntentId);
    Task<IEnumerable<PaymentDto>> GetPaymentsByAccountIdAsync(Guid accountId);
    Task<IEnumerable<PaymentDto>> GetPaymentsByCustomerIdAsync(Guid customerId, Guid accountId);
    Task<IEnumerable<PaymentDto>> GetPaymentsByTerminalIdAsync(string terminalId, Guid accountId);
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request);
    Task<PaymentDto> UpdatePaymentAsync(Guid id, UpdatePaymentRequest request, Guid accountId);
    Task<PaymentDto> RefundPaymentAsync(Guid id, RefundPaymentRequest request, Guid accountId);
    Task DeletePaymentAsync(Guid id, Guid accountId);
}
